using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator.EditorTools
{
[CustomEditor(typeof(BoneAssigner))]    
public class BoneAssignerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        BoneAssigner boneAssigner = (BoneAssigner)target;

        if (GUILayout.Button("Generate Bones"))
        {
            boneAssigner.GenerateBones();
        }
    }
}
}
