using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

class CMBoneChain {

    public readonly DynamicBone bone;
    public readonly GameObject  parentObject;
    
    public readonly float originalUpdateRate;
    public readonly float originalDamping;
    public readonly float originalElasticity;
    public readonly float originalInert;
    public readonly List<DynamicBoneCollider> originalColliders;
    
    bool _boneWasEnabled;
    bool _enabled = true;
    
    public bool enabled {
        get { return (_enabled && bone != null); }
        set {
            if (bone != null) {
                if (_enabled != bone.enabled)  // check if something or someone changed bone active status
                    _boneWasEnabled = bone.enabled;
                if (!value || (value && _boneWasEnabled))  // always switch off, but switch on only if it was enabled originally
                    bone.enabled = value;
                _enabled = bone.enabled;
            }
        }
    }

    public CMBoneChain(DynamicBone bone, GameObject parentObject)
    {
        this.bone = bone;
        this.parentObject = parentObject;

        _enabled = _boneWasEnabled = bone.enabled;

        originalUpdateRate = bone.m_UpdateRate;
        originalDamping    = bone.m_Damping;
        originalElasticity = bone.m_Elasticity;
        originalInert      = bone.m_Inert;

        bone.m_Colliders.RemoveAll(item => item == null);
        originalColliders = new List<DynamicBoneCollider>(bone.m_Colliders);
    }

    public void ChangeUpdateRate(float value)
    {
        if (bone != null) {
            bone.m_UpdateRate = value;

            if (value > 0 && originalUpdateRate > 0) {
                float k = value / originalUpdateRate;
                bone.m_Damping    = originalDamping    * k;
                bone.m_Elasticity = originalElasticity / k;
                bone.m_Inert      = originalInert      / k;
            }
        }
    }

    public void RestoreOriginalState()
    {
        if (bone != null) {
            bone.m_UpdateRate = originalUpdateRate;
            bone.m_Damping    = originalDamping;
            bone.m_Elasticity = originalElasticity;
            bone.m_Inert      = originalInert;
            bone.enabled      = _boneWasEnabled;
            bone.m_Colliders  = new List<DynamicBoneCollider>(originalColliders);
        }
    }

    public void RestoreOriginalColliders()
    {
        if (bone != null)
            bone.m_Colliders = new List<DynamicBoneCollider>(originalColliders);
    }

    public void SetColliders(IEnumerable<DynamicBoneCollider> colliders)
    {
        if (bone != null) {
            if (colliders != null)
                bone.m_Colliders = new List<DynamicBoneCollider>(colliders);
            else {
                int n = (originalColliders != null ? originalColliders.Count * 3 : 0);
                bone.m_Colliders = new List<DynamicBoneCollider>(n);
            }
        }
    }

    public void AddCollider(DynamicBoneCollider collider)
    {
        if (bone != null && collider != null && !bone.m_Colliders.Contains(collider))
            bone.m_Colliders.Add(collider);
    }
}
