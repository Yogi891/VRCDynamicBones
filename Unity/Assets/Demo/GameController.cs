using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;

public class GameController : MonoBehaviour
{
    public GameObject player;

    public float eyeHeight    = 1.5f;
    public int   clonesNumber = 18;
    public float cloneRadius  = 5.0f;
    public int   cloneRings   = 2;

    int   prevClonesNumber;
    float prevCloneRadius;
    int   prevCloneRings;
    
    public CMDynamicBonesMode  dynamicBonesMode = CMDynamicBonesMode.Local;   // Local for everyone / Between you and other players / Between all players
    [Range(0, 100)] public int workingDistance  = 10;                         // Maximum distance from you to dynamic bones at which they will stay enabled
    public CMUpdateRateMode    updateRateMode   = CMUpdateRateMode.Constant;  // Constant / Distance Dependent
    [Range(0, 120)] public int maxUpdateRate    = 60;                         // Update rate for dynamic bones that are local or that are very close to you when Distance Dependent mode is enabled
    [Range(0, 120)] public int minUpdateRate    = 15;                         // Update rate for dynamic bones that are far away
    
    public bool enableOptimizations = true;

    public bool showDebugColliders;

    List<GameObject> players = new List<GameObject>();

    CollisionsManager collisionManager = new CollisionsManager();

    void OnValidate()
    {
        if (Application.isEditor && Application.isPlaying)
        {
            bool needRestart = (clonesNumber != prevClonesNumber || cloneRadius != prevCloneRadius || cloneRings != prevCloneRings);
            
            collisionManager.dynamicBonesMode = dynamicBonesMode;
            collisionManager.workingDistance  = workingDistance;
            collisionManager.updateRateMode   = updateRateMode;
            collisionManager.maxUpdateRate    = maxUpdateRate;
            collisionManager.minUpdateRate    = minUpdateRate;

            collisionManager.enableOptimizations = enableOptimizations;

            collisionManager.showDebugColliders = showDebugColliders;

            if (needRestart) {
                prevClonesNumber = clonesNumber;
                prevCloneRadius  = cloneRadius;
                prevCloneRings   = cloneRings;
                Restart();
            }
        }
    }
    
    void Restart()
    {
        Debug.Log("Restart");

        foreach (var p in players) {
            collisionManager.Remove(p);
            if (p != player)
                Destroy(p);
        }
        players.Clear();

        if (player == null)
            return;

        if (clonesNumber < 0 || cloneRadius <= 0) {
            player.SetActive(true);
            return;
        }

        player.SetActive(false);

        AddPlayerClone(player, player.transform.position, true);

        int rings = Math.Min(Math.Max(cloneRings, 1), clonesNumber);
        int sum = 1;
        for (int ri = 1; ri < rings; ri++) {
            sum += sum * 2;
        }
        float nk = 1.0f / (float)sum;  // 1.0 for 1 ring, 0.333 for 2 rings, 0.143 for 3 rings

        for (int ri = 1; ri <= rings; ri++) {
            int nPerRing = Mathf.RoundToInt((float)clonesNumber * ri * nk);
            float r = cloneRadius * (float)ri / (float)rings;
            for (int i = 0; i < nPerRing; i++) {
                float a = 0.5f * Mathf.PI - (float)i / (float)nPerRing * (Mathf.PI * 2f);
                Vector3 pos = player.transform.position + new Vector3(r * Mathf.Sin(a), 0, r * Mathf.Cos(a));
                AddPlayerClone(player, pos, false);
            }
        }
    }

    void AddPlayerClone(GameObject player, Vector3 pos, bool isLocalPlayer) {

        GameObject playerClone = Instantiate(player);
        playerClone.SetActive(true);
        playerClone.transform.position = pos;
        players.Add(playerClone);
        collisionManager.AddPlayer(playerClone, isLocalPlayer, player.name, eyeHeight);
    }

    void Update()
    {
        collisionManager.Update();
    }
}
