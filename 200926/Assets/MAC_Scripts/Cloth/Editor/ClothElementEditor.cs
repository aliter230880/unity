using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator
{
    [CustomEditor(typeof(ClothElement))] 
    public class ClothElementEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ClothElement clothElement = (ClothElement)target;

            DrawDefaultInspector();

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Cloth Guid");
                GUILayout.Label(clothElement.clothGuid);
            }

            if (GUILayout.Button("Generate Guid"))
            {
                clothElement.GenerateGuid();
            }
        }
    }
}

