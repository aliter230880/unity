using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator.EditorTools
{
[CustomEditor(typeof(SpringBoneAssistant))]
public class SpringBoneAssistantEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpringBoneAssistant obj = (SpringBoneAssistant)target;

        if (GUILayout.Button("Mark Children"))
        {
            obj.MarkChildren();
        }

        if (GUILayout.Button("Clean Up"))
        {
            obj.CleanUp();
        }
    }
}
}
