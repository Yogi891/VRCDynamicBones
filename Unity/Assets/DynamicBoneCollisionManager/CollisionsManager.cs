using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CMDynamicBonesMode {
    Disabled          = -1,
    Local             = 0,
    GlobalForPlayer   = 1,
    GlobalForEveryone = 2
}

public enum CMUpdateRateMode {
    Constant          = 0,
    DistanceDependent = 1
}

public enum CMCollidersFilter {
    All       = 0,
    UpperBody = 1,
    HandsOnly = 2
}

public class CollisionsManager
{
    CMDynamicBonesMode _dynamicBonesMode = CMDynamicBonesMode.Local;  // Disabled / Local for everyone / Between local user and other players / Between all players

    public float              workingDistance       = 15;                         // Maximum distance from the camera to dynamic bones at which they will stay enabled
    public CMUpdateRateMode   updateRateMode        = CMUpdateRateMode.Constant;  // Constant / Distance Dependent
    public CMCollidersFilter  localCollidersFilter  = CMCollidersFilter.All;      // Enables specific colliders filter mode for local user
    public CMCollidersFilter  othersCollidersFilter = CMCollidersFilter.All;      // Enables specific colliders filter mode for other players
    public float              maxUpdateRate         = 60;                         // Update rate for dynamic bones that are local or that are very close to you when Distance Dependent mode is enabled
    public float              minUpdateRate         = 30;                         // Update rate for dynamic bones that are far away
    
    public float averagePlayerHeight    = 1.6f;  // TODO
    public float minimumWorkingDistance = 1.5f;
    public float collisionSwitchRange   = 0.3f;

    public bool enableOptimizations = true;

    public bool showDebugColliders;
    
    static Vector4 debugColliderColorDefault  = new Vector4(1, 1, 1, 0.35f);
    static Vector4 debugColliderColorCollide  = new Vector4(0.44f, 0, 1, 0.35f);
    static Vector4 debugColliderColorInactive = new Vector4(0.5f, 0.5f, 0.5f, 0.3f);

    List<CMPlayerInfo> playersInfo = new List<CMPlayerInfo>(64);
    
    public CMDynamicBonesMode dynamicBonesMode {
        get { return _dynamicBonesMode; }
        set {
            if (_dynamicBonesMode != value) {
                _dynamicBonesMode = value;
                bool enableBones = (_dynamicBonesMode != CMDynamicBonesMode.Disabled);
                foreach (CMPlayerInfo player in playersInfo) {
                    if (player.gameObject != null) {
                        player.enableDynamicBones = enableBones;
                        if (showDebugColliders && !enableBones)
                            player.SetDebugColliderColor(debugColliderColorInactive);
                    }
                }
            }
        }
    }

    public void Clear()
    {
        foreach (CMPlayerInfo player in playersInfo) {
            if (player.gameObject != null) {
                player.enableDynamicBones = true;
                player.showDebugCollider = false;
                foreach (CMBoneChain boneChain in player.boneChains) {
                    boneChain.RestoreOriginalState();
                    boneChain.RestoreOriginalColliders();
                }
            }
        }
        playersInfo.Clear();
    }

    public void AddPlayer(GameObject gameObject, bool isLocalPlayer, string name, float eyeHeight)
    {
        if (gameObject != null && !Contains(gameObject)) {
            playersInfo.Capacity = Math.Max(playersInfo.Capacity, playersInfo.Count + 1);

            var playerInfo = new CMPlayerInfo(gameObject, isLocalPlayer, name, eyeHeight);
            playersInfo.Add(playerInfo);
        }
    }

    public void Remove(GameObject gameObject)
    {
        foreach (CMPlayerInfo player in playersInfo) {
            if (player.gameObject != null && player.gameObject.Equals(gameObject)) {
                player.enableDynamicBones = true;
                player.showDebugCollider = false;
                foreach (CMBoneChain boneChain in player.boneChains) {
                    boneChain.RestoreOriginalState();
                    boneChain.RestoreOriginalColliders();
                }
                playersInfo.Remove(player);
                break;
            }
        }
    }

    public bool Contains(GameObject gameObject)
    {
        foreach (CMPlayerInfo player in playersInfo) {
            if (player.gameObject != null && player.gameObject.Equals(gameObject))
                return true;
        }
        return false;
    }
    
    public void Update()
    {
        // remove objects that has been destroyed
        for (int i = 0; i < playersInfo.Count; i++) {
            CMPlayerInfo player = playersInfo[i];
            if  (player.gameObject == null) {
                playersInfo.RemoveAt(i);
                i--;
            }
        }

        foreach (CMPlayerInfo player in playersInfo) {
            player.showDebugCollider = showDebugColliders;
            if (player.enableDynamicBones && _dynamicBonesMode == CMDynamicBonesMode.Disabled) {
                player.enableDynamicBones = false;
                player.SetDebugColliderColor(debugColliderColorInactive);
            }
        }

        if (_dynamicBonesMode == CMDynamicBonesMode.Disabled)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 cameraPos = camera.transform.position;

        int mode = (int)dynamicBonesMode;
        bool variableUpdateRate = (updateRateMode == CMUpdateRateMode.DistanceDependent);
        
        int maxBoneChainsCount = 0;
        int maxCollidersCount  = 0;
        foreach (CMPlayerInfo player in playersInfo) {
            maxBoneChainsCount += player.boneChains.Count;
            maxCollidersCount += player.allColliders.Count;
        }
        
        var enabledPlayers = new List<CMPlayerInfo>(playersInfo.Count);
        
        foreach (CMPlayerInfo player in playersInfo) {
            float distanceToCamera = (player.isLocalPlayer || !enableOptimizations ? 0 : (player.center - cameraPos).magnitude);

            // enable/disable collisions for player
            if (player.isLocalPlayer || !enableOptimizations)
                player.enableDynamicBones = true;
            else if (workingDistance <= 0 || distanceToCamera < workingDistance - collisionSwitchRange * 0.5f)
                player.enableDynamicBones = true;
            else if (workingDistance > 0 && distanceToCamera > workingDistance + collisionSwitchRange * 0.5f)
                player.enableDynamicBones = false;

            if (player.enableDynamicBones) {
                // collect all enabled players
                enabledPlayers.Add(player);
                
                float refreshRate = 1.0f / Time.deltaTime;

                foreach (CMBoneChain chain in player.boneChains) {
                    if (chain.enabled) {
                        // set dynamic bones update rate
                        if (!variableUpdateRate || workingDistance <= 0)
                            chain.bone.m_UpdateRate = maxUpdateRate;
                        else {
                            float maxFps  = (maxUpdateRate > 0 ? maxUpdateRate : refreshRate);
                            float minFps  = Mathf.Min((minUpdateRate > 0 ? minUpdateRate : refreshRate), maxFps);
                            float dist    = Mathf.Max(distanceToCamera - minimumWorkingDistance, 0);
                            float maxDist = Mathf.Max(workingDistance - minimumWorkingDistance, dist);
                            chain.bone.m_UpdateRate = Mathf.Lerp(maxFps, minFps, dist/maxDist);
                        }
                    }
                }
            }
        }

        var allCollidingPlayers = new List<CMPlayerInfo>(enabledPlayers.Count);
        var tmpCollidingPlayers = new List<CMPlayerInfo>(enabledPlayers.Count);
        var tmpColliders = new List<DynamicBoneCollider>(maxCollidersCount);
        
        foreach (CMPlayerInfo player in enabledPlayers) {
            if (mode > 0) {
                // add all colliding players to list
                foreach (CMPlayerInfo otherPlayer in enabledPlayers) {
                    if (otherPlayer != player && !tmpCollidingPlayers.Contains(otherPlayer))
                        if (player.isLocalPlayer || mode == 2 || (mode == 1 && otherPlayer.isLocalPlayer))
                            if (!enableOptimizations || player.CheckPlayerCollision(otherPlayer)) {
                                tmpCollidingPlayers.Add(otherPlayer);
                                // collect other player colliders
                                CMCollidersFilter filter = (otherPlayer.isLocalPlayer ? localCollidersFilter : othersCollidersFilter);
                                if (filter == CMCollidersFilter.All)
                                    tmpColliders.AddRange(otherPlayer.sharedColliders);
                                else if (filter == CMCollidersFilter.UpperBody)
                                    tmpColliders.AddRange(otherPlayer.upperBodyColliders);
                                else if (filter == CMCollidersFilter.HandsOnly)
                                    tmpColliders.AddRange(otherPlayer.handsColliders);
                            }
                }
            }

            foreach (CMBoneChain chain in player.boneChains) {
                // set colliders to player dynamic bones
                if (chain.enabled) {
                    if (tmpColliders.Count == 0)
                        chain.RestoreOriginalColliders();
                    else
                        chain.bone.m_Colliders = chain.originalColliders.Concat(tmpColliders).ToList();
                }
            }

            if (showDebugColliders && tmpCollidingPlayers.Count > 0 && !allCollidingPlayers.Contains(player))
                allCollidingPlayers.Add(player);

            tmpCollidingPlayers.Clear();
            tmpColliders.Clear();
        }
        
        if (showDebugColliders) {
            foreach (CMPlayerInfo player in playersInfo)
                player.SetDebugColliderColor(allCollidingPlayers.Contains(player) ? debugColliderColorCollide : (enabledPlayers.Contains(player) ? debugColliderColorDefault : debugColliderColorInactive));
        }
        
        enabledPlayers.Clear();
    }

    // Utilites

    CMPlayerInfo GetPlayerForGameObject(GameObject gameObject)
    {
        if (gameObject != null) {
            foreach (CMPlayerInfo player in playersInfo) {
                if (player.gameObject != null && player.gameObject.Equals(gameObject))
                    return player;
            }
        }
        return null;
    }
    
    float DistanceToObject(GameObject obj) {
        return (Camera.main.transform.position - obj.transform.position).magnitude;
    }

    bool CheckSpheresIntersection(Vector3 p1, float r1, Vector3 p2, float r2) {
        
        float distSQ = (p2 - p1).sqrMagnitude;
        float rr = r1 + r2;
        return (distSQ <= rr * rr);
    }

    float ScreenSpaceTakenByObject(GameObject obj) {

        Bounds bounds = CalculateBounds(obj);
        Vector3 max = bounds.size;
        if (max.x == 0 && max.y == 0 && max.z == 0)
            return 0;

        // Get the radius of a sphere circumscribing the bounds
        float radius = max.magnitude * 0.5f;
        
        Camera camera = Camera.main;
        // Get the horizontal FOV, since it may be the limiting of the two FOVs to properly encapsulate the objects
        float fovHor = 2.0f * Mathf.Atan(Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad) * camera.aspect) * Mathf.Rad2Deg;
        // Use the smaller FOV as it limits what would get cut off by the frustum
        float fov = Mathf.Min(camera.fieldOfView, fovHor);
        float dist = (obj.transform.position - camera.transform.position).magnitude;
        float spaceTaken = radius / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) / dist;
        return spaceTaken;
    }

    float ScreenSpaceTakenByPlayer(CMPlayerInfo player) {
        
        Camera camera = Camera.main;
        // Get the horizontal FOV, since it may be the limiting of the two FOVs to properly encapsulate the objects
        float fovHor = 2.0f * Mathf.Atan(Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad) * camera.aspect) * Mathf.Rad2Deg;
        // Use the smaller FOV as it limits what would get cut off by the frustum
        float fov = Mathf.Min(camera.fieldOfView, fovHor);
        float dist = (player.center - camera.transform.position).magnitude;
        float spaceTaken = player.radius / Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) / dist;
        return spaceTaken;
    }

    Bounds CalculateBounds(GameObject obj) {

        Bounds b = new Bounds(obj.transform.position, Vector3.zero);
        Component[] rList = obj.GetComponentsInChildren(typeof(SkinnedMeshRenderer));
        foreach (SkinnedMeshRenderer r in rList)
            b.Encapsulate(r.bounds);

        return b;
    }
    
    void FindComponents(GameObject rootObject, Type type, bool searchInObject, bool searchInChildren, List<Component> results) {

        if (rootObject == null || type == null || results == null)
            return;

        if (searchInObject) {
            Component[] childComponents = rootObject.GetComponents(type);
            foreach (Component c in childComponents) {
                if (c != null)
                    results.Add(c);
            }
        }
        if (searchInChildren) {
            for (int i = 0; i < rootObject.transform.childCount; i++) {
                GameObject child = rootObject.transform.GetChild(i).gameObject;
                if (child != null)
                    FindComponents(child, type, true, true, results);
            }
        }
    }
}
