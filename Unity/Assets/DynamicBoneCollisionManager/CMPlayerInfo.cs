using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

class CMPlayerInfo
{
    public GameObject gameObject;
    public bool       isLocalPlayer;
    public string     name;
    public float      eyeHeight;
    
    Vector3 localCenter;  // collider center
    float   localRadius;  // collider radius
    float   localHeight;  // height of the capsule collider

    public float radius;  // collider radius
    public float height;  // height of the capsule collider

    public List<CMBoneChain> boneChains;

    public List<DynamicBoneCollider> allColliders;
    public List<DynamicBoneCollider> sharedColliders;
    public List<DynamicBoneCollider> upperBodyColliders;
    public List<DynamicBoneCollider> handsColliders;

    bool _enableDynamicBones = true;

    GameObject _collider;
    Material   _colliderMaterial;
    bool       _showCollider;

    public CMPlayerInfo(GameObject gameObject, bool isLocalPlayer, string name, float eyeHeight) {

        this.gameObject    = gameObject;
        this.isLocalPlayer = isLocalPlayer;
        this.name          = name;
        this.eyeHeight     = eyeHeight;

        // Get Dynamic Bones

        var bones = gameObject.GetComponentsInChildren<DynamicBone>(true);

        boneChains = new List<CMBoneChain>(bones.Length);

        foreach (DynamicBone b in bones) {
            var boneInfo = new CMBoneChain(b, gameObject);
            boneChains.Add(boneInfo);
        }

        // Get Colliders

        float maxColliderRadius = (eyeHeight > 0 ? eyeHeight * 0.175f : GetDynamicBonesBounds(false).size.magnitude * 0.25f);

        Animator animator = gameObject.GetComponent<Animator>();

        Transform chest = (animator != null ? animator.GetBoneTransform(HumanBodyBones.Chest) : null);
        bool countForUpperBody = false;

        Transform[] allChildren   = gameObject.GetComponentsInChildren<Transform>();
        Transform[] handsChildren = GetArmatureBoneChildren(animator, HumanBodyBones.LeftHand).Concat(GetArmatureBoneChildren(animator, HumanBodyBones.RightHand)).ToArray();
        
        allColliders       = new List<DynamicBoneCollider>(allChildren.Length);
        sharedColliders    = new List<DynamicBoneCollider>(allChildren.Length);
        upperBodyColliders = new List<DynamicBoneCollider>(allChildren.Length);
        handsColliders     = new List<DynamicBoneCollider>(handsChildren.Length);

        foreach (var t in allChildren) {
            if (Equals(t, chest))
                countForUpperBody = true;
            var col = t.GetComponent<DynamicBoneCollider>();
            if (col != null) {
                allColliders.Add(col);
                // we'll ignore 'inside' type of colliders and those that are too big (bigger than the bones total radius)
                if ((int)col.m_Bound == 0 && col.GetRadius() <= maxColliderRadius) {
                    sharedColliders.Add(col);
                    if (countForUpperBody)
                        upperBodyColliders.Add(col);
                }
            }
        }

        foreach (var t in handsChildren) {
            var col = t.GetComponent<DynamicBoneCollider>();
            if (col != null && (int)col.m_Bound == 0 && col.GetRadius() <= maxColliderRadius)
                handsColliders.Add(col);
        }
        
        if (boneChains.Count + sharedColliders.Count > 0)
            RecalculateCollisionBounds(false);
    }

    Transform[] GetArmatureBoneChildren(Animator animator, HumanBodyBones bone)
    {
        if (animator != null) {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
                return t.GetComponentsInChildren<Transform>();
        }
        return new Transform[0];
    }

    public Vector3 center {
        get { return gameObject.transform.TransformPoint(localCenter); }
        set { localCenter = gameObject.transform.InverseTransformPoint(value); }
    }
    
    public bool enableDynamicBones {
        get { return _enableDynamicBones; }
        set {
            if (_enableDynamicBones != value) {
                _enableDynamicBones = value;
                foreach (CMBoneChain chain in boneChains)
                    chain.enabled = value;
            }
        }
    }
    
    public Bounds GetDynamicBonesBounds(bool includeInactiveJoints) {

        var bounds = new Bounds(this.gameObject.transform.position, Vector3.zero);
        int i = 0;
        foreach (CMBoneChain boneInfo in boneChains) {
            if (boneInfo.bone.isActiveAndEnabled || includeInactiveJoints) {
                boneInfo.bone.EnumerateJoints((Vector3 p, float r) => {
                    bounds = (i == 0 ? new Bounds(p, new Vector3(r*2, r*2, r*2)) : bounds.EncapsulateSphere(p, r));
                    i++;
                });
            }
        }
        return bounds;
    }

    public void RecalculateCollisionBounds(bool includeInactiveJoints) {
        
        Bounds bounds = GetDynamicBonesBounds(includeInactiveJoints);
        
        foreach (DynamicBoneCollider col in sharedColliders) {
            if (col.isActiveAndEnabled || includeInactiveJoints)
                bounds = bounds.EncapsulateSphere(col.GetPosition(), col.GetRadius());
        }

        if (eyeHeight > 0) {
            Vector3 p = this.gameObject.transform.position;
            p.y += eyeHeight * 0.5f;
            float x = eyeHeight * 0.35f;
            bounds.Encapsulate(new Bounds(p, new Vector3(x, eyeHeight, x)));
        }

        float scale = 1.0f / this.gameObject.transform.lossyScale.x;
        float size = bounds.size.magnitude;
        if (eyeHeight > 0)
            size = Mathf.Max(eyeHeight, Mathf.Max(bounds.size.y, new Vector3(bounds.size.x, 0, bounds.size.z).magnitude));

        radius = size * 0.5f;
        localRadius = radius * scale;

        height      = eyeHeight * 0.5f;  // height of the capsule collider
        localHeight = height * scale;

        localCenter = gameObject.transform.InverseTransformPoint(bounds.center);
        localCenter.y = (localHeight > 0 ? localHeight : localCenter.y);

        AddDebugCollider();
    }
        
    public bool CheckPlayerCollision(CMPlayerInfo otherPlayer) {
        
        // Check collision between two capsules or spheres
        Vector3 p1 = this.center;
        Vector3 p2 = otherPlayer.center;
        float rSum  = this.radius + otherPlayer.radius;
        float hhSum = this.height + otherPlayer.height;

        if (p1.y - p2.y > hhSum)
            p1.y -= hhSum;
        else if (p2.y - p1.y > hhSum)
            p2.y -= hhSum;
        else
            p2.y = p1.y;

        float distSQ = (p2 - p1).sqrMagnitude;
        return (distSQ <= rSum * rSum);
    }

    public void AddDebugCollider()
    {
        if (gameObject == null)
            return;

        float size  = localRadius * 2.0f;
        float sizeY = (eyeHeight > 0 ? localHeight + localRadius : size);

        if (gameObject == null || _collider != null) {
            _collider.transform.position = this.center;
            _collider.transform.localScale = new Vector3(size, sizeY, size);
            return;
        }
        
        _collider = GameObject.CreatePrimitive(eyeHeight > 0 ? PrimitiveType.Capsule : PrimitiveType.Sphere);
        _collider.name = "DebugCollider";
        _collider.transform.parent = gameObject.transform;
        _collider.transform.position = this.center;
        _collider.transform.localScale = new Vector3(size, sizeY, size);
        if (eyeHeight > 0)
            _collider.GetComponent<CapsuleCollider>().enabled = false;
        else
            _collider.GetComponent<SphereCollider>().enabled = false;
        _collider.SetActive(false);

        var material = _collider.GetComponent<Renderer>().material;
        material.shader = Shader.Find("Standard");
        material.SetVector("_Color", new Vector4(1, 1, 1, 0.35f));
        material.SetFloat("_Mode", 2);
        material.SetFloat("_Glossiness", 0);
        material.SetFloat("_SpecularHighlights", 0);
        material.SetFloat("_GlossyReflections", 0);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;

        _colliderMaterial = material;
    }

    public bool showDebugCollider {
        get { return _showCollider; }
        set {
            if (_showCollider != value && _collider != null) {
                _showCollider = value;
                _collider.SetActive(value);
            }
        }
    }
    
    public void SetDebugColliderColor(Vector4 color)
    {
        if (_colliderMaterial != null)
            _colliderMaterial.SetVector("_Color", color);
    }
}
