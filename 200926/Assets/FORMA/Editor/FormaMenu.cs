using System.IO;
using Forma;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Forma.Editor
{
    public static class FormaMenu
    {
        const string ScenePath = "Assets/FORMA/Scenes/FORMA_Studio.unity";

        [MenuItem("FORMA/Open Studio Scene", false, 0)]
        public static void OpenStudioScene()
        {
            if (!File.Exists(ScenePath))
            {
                CreateStudioScene();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("FORMA/Create Studio Scene", false, 1)]
        public static void CreateStudioScene()
        {
            if (File.Exists(ScenePath))
            {
                bool openExisting = EditorUtility.DisplayDialog(
                    "FORMA",
                    "Сцена уже есть:\n" + ScenePath + "\n\nОткрыть её? Нажмите Cancel, чтобы пересоздать.",
                    "Открыть",
                    "Пересоздать");
                if (openExisting)
                {
                    OpenStudioScene();
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var studio = new GameObject("FORMA Studio");
            studio.AddComponent<FormaStudio>();

            Directory.CreateDirectory("Assets/FORMA/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "FORMA",
                "Сцена студии создана:\n" + ScenePath + "\n\nНажмите Play, чтобы открыть редактор аватара.\nВсе скрипты лежат в Assets/FORMA/Scripts — их можно свободно менять.",
                "OK");
        }

        [MenuItem("FORMA/Select Studio", false, 2)]
        public static void SelectStudio()
        {
            var go = GameObject.Find("FORMA Studio");
            if (go == null)
            {
                EditorUtility.DisplayDialog("FORMA", "Сначала откройте сцену: FORMA → Open Studio Scene", "OK");
                return;
            }
            Selection.activeGameObject = go;
        }

        [MenuItem("FORMA/Documentation", false, 20)]
        public static void OpenReadme()
        {
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FORMA/README.txt");
            if (readme != null) Selection.activeObject = readme;
            else EditorUtility.DisplayDialog("FORMA", "README: Assets/FORMA/README.txt", "OK");
        }
    }
}
