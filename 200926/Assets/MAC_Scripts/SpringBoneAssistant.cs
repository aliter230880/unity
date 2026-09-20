using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
public class SpringBoneAssistant : MonoBehaviour
{
    public UnityChan.SpringManager mSpringManager;

    public Vector3 mTargetBoneAxis = new Vector3(0f, 1f, 0f);

    public void MarkChildren()
    {
        SpringBoneMarker[] boneMarkers = FindObjectsOfType<SpringBoneMarker>();

        for (int i = 0; i < boneMarkers.Length; i++)
        {
            boneMarkers[i].MarkChildren();
        }

        boneMarkers = FindObjectsOfType<SpringBoneMarker>();
        List<UnityChan.SpringBone> springBones = new List<UnityChan.SpringBone> { };
        for (int i = 0; i < boneMarkers.Length; i++)
        {
            springBones.Add(boneMarkers[i].AddSpringBone());
            springBones[i].boneAxis = mTargetBoneAxis;
            boneMarkers[i].UnmarkSelf();
        }

        mSpringManager.springBones = springBones.ToArray();
    }

    public void CleanUp()
    {
        SpringBoneMarker[] boneMarkers = FindObjectsOfType<SpringBoneMarker>();
        UnityChan.SpringBone[] springBones = FindObjectsOfType<UnityChan.SpringBone>();

        for (int i = 0; i < boneMarkers.Length; i++)
        {
            DestroyImmediate(boneMarkers[i]);
        }
        for (int i = 0; i < springBones.Length; i++)
        {
            DestroyImmediate(springBones[i]);
        }
    }
}
}
