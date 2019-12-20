using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

class CMBoneChain {

    public readonly DynamicBone bone;
    public readonly GameObject  parentObject;

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

    public CMBoneChain(DynamicBone bone, GameObject parentObject) {

        this.bone = bone;
        this.parentObject = parentObject;

        _enabled = _boneWasEnabled = bone.enabled;
        
        bone.m_Colliders.RemoveAll(item => item == null);
        originalColliders = new List<DynamicBoneCollider>(bone.m_Colliders);
    }

    public void RestoreOriginalState() {
        if (bone != null)
            bone.enabled = _boneWasEnabled;
    }

    public void RestoreOriginalColliders() {
        if (bone != null)
            bone.m_Colliders = new List<DynamicBoneCollider>(originalColliders);
    }

    public void SetColliders(IEnumerable<DynamicBoneCollider> colliders) {
        
        if (bone != null) {
            if (colliders != null)
                bone.m_Colliders = new List<DynamicBoneCollider>(colliders);
            else {
                int n = (originalColliders != null ? originalColliders.Count * 3 : 0);
                bone.m_Colliders = new List<DynamicBoneCollider>(n);
            }
        }
    }

    public void AddCollider(DynamicBoneCollider collider) {
        
        if (bone != null && collider != null && !bone.m_Colliders.Contains(collider))
            bone.m_Colliders.Add(collider);
    }
}
