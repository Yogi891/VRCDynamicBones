using UnityEngine;
using System;

public static class CMExtensions
{
    // Bounds: Encapsulate Sphere
    public static Bounds EncapsulateSphere(this Bounds source, Vector3 point, float radius)
    {
        Vector3 min = source.min;
        Vector3 max = source.max;
        min.x = Mathf.Min(min.x, point.x - radius);
        min.y = Mathf.Min(min.y, point.y - radius);
        min.z = Mathf.Min(min.z, point.z - radius);
        max.x = Mathf.Max(max.x, point.x + radius);
        max.y = Mathf.Max(max.y, point.y + radius);
        max.z = Mathf.Max(max.z, point.z + radius);
        source.SetMinMax(min, max);
        return source;
    }

    // DynamicBone: Get World Radius
    public static float GetRadius(this DynamicBone source)
    {
        float scale = Mathf.Abs(source.transform.lossyScale.x);
        return source.m_Radius * scale;
    }

    // DynamicBoneCollider: Get World Radius
    public static float GetRadius(this DynamicBoneCollider source)
    {
        float scale = Mathf.Abs(source.transform.lossyScale.x);
        float r = source.m_Radius * scale;
        float h = 0.5f * source.m_Height - r;
        return (h <= 0 ? r : h * scale + r);
    }

    // DynamicBoneCollider: Get World Position
    public static Vector3 GetPosition(this DynamicBoneCollider source)
    {
        return source.transform.TransformPoint(source.m_Center);
    }

    public static int GetJointsCount(this DynamicBone bone)
    {
        int n = 0;
        if (bone.m_Root != null)
            bone.CountNextJoint(bone.m_Root, ref n);
        return n;
    }

    private static void CountNextJoint(this DynamicBone bone, Transform t, ref int n)
    {
        n++;
        
        for (int i = 0; i < t.childCount; i++) {
            bool exclude = false;
            if (bone.m_Exclusions != null) {
                for (int j = 0; j < bone.m_Exclusions.Count; ++j) {
                    Transform e = bone.m_Exclusions[j];
                    if (e == t.GetChild(i)) {
                        exclude = true;
                        break;
                    }
                }
            }
            if (!exclude)
                bone.CountNextJoint(t.GetChild(i), ref n);
        }

        if (t.childCount == 0 && (bone.m_EndLength > 0 || bone.m_EndOffset != Vector3.zero))
            n++;
    }

    // DynamicBone: Iterate Joints
    // usage example:
    //   bone.IterateJoints((Vector3 p, float r) => {
    //       Debug.Log("joint position = " + p.ToString() + ", radius = " + r);
    //   });
    public static void EnumerateJoints(this DynamicBone bone, Action<Vector3, float> block)
    {
        if (bone.m_Root != null && block != null)
            bone.NextJoint(bone.m_Root, block);
    }

    private static void NextJoint(this DynamicBone bone, Transform t, Action<Vector3, float> block)
    {
        bool variableRadius = (bone.m_RadiusDistrib != null && bone.m_RadiusDistrib.keys.Length > 0);

        float r = bone.m_Radius * (t == bone.m_Root && variableRadius ? bone.m_RadiusDistrib.Evaluate(0f) : 1f) * bone.transform.lossyScale.x;
        block(t.position, r);
            
        for (int i = 0; i < t.childCount; i++) {
            bool exclude = false;
            if (bone.m_Exclusions != null) {
                for (int j = 0; j < bone.m_Exclusions.Count; ++j) {
                    Transform e = bone.m_Exclusions[j];
                    if (e == t.GetChild(i)) {
                        exclude = true;
                        break;
                    }
                }
            }
            if (!exclude)
                bone.NextJoint(t.GetChild(i), block);
        }

        if (t.childCount == 0 && (bone.m_EndLength > 0 || bone.m_EndOffset != Vector3.zero)) {
            // End joint
            Vector3 endOffset = Vector3.zero;
            if (bone.m_EndLength > 0) {
                Transform ppb = t.parent;
                if (ppb != null)
                    endOffset = t.InverseTransformPoint((t.position * 2 - ppb.position)) * bone.m_EndLength;
                else
                    endOffset = new Vector3(bone.m_EndLength, 0, 0);
            }
            else {
                endOffset = t.InverseTransformPoint(bone.transform.TransformDirection(bone.m_EndOffset) + t.position);
            }
            r = bone.m_Radius * (variableRadius ? bone.m_RadiusDistrib.Evaluate(1f) : 1f) * bone.transform.lossyScale.x;
            block(t.TransformPoint(endOffset), r);
        }
    }
}
