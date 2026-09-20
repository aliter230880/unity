using UnityEditor;
using UnityEngine;

namespace MagicAvatarCreator
{
    [CustomEditor(typeof(BlendShapeBoneSyncData))]
    public class BlendShapeBoneSyncDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var data = target as BlendShapeBoneSyncData;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Настройте синхронизацию костей для срабатывания Blend Shapes.\n" +
                "Для каждой кости укажите позицию при значении 0 и 100 блендшейпа.",
                MessageType.Info);

            EditorGUILayout.Space();

            // Кнопка добавления конфига
            if (GUILayout.Button("Добавить конфиг кости"))
            {
                var newConfig = new BlendShapeBoneSyncData.BoneSyncConfig
                {
                    boneName = "NewBone",
                    positionAt0 = Vector3.zero,
                    positionAt100 = Vector3.zero
                };
                data.boneConfigs.Add(newConfig);
            }

            EditorGUILayout.Space();

            // Редактирование существующих конфигов
            for (int i = 0; i < data.boneConfigs.Count; i++)
            {
                DrawBoneConfig(data, i);
            }
        }

        private void DrawBoneConfig(BlendShapeBoneSyncData data, int index)
        {
            var config = data.boneConfigs[index];

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"Кость #{index + 1}", EditorStyles.boldLabel);

            // Название кости
            string newName = EditorGUILayout.TextField("Имя кости", config.boneName);

            // Позиции в Inspector можно задавать в мировых координатах
            // Но лучше в локальных относительно родителя
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Позиция при 0", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);
            Vector3 newPos0 = EditorGUILayout.Vector3Field("", config.positionAt0);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Позиция при 100", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);
            Vector3 newPos100 = EditorGUILayout.Vector3Field("", config.positionAt100);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Кнопки управления
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Удалить", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                data.boneConfigs.RemoveAt(index);
                return;
            }

            // Кнопка копирования позиций
            if (GUILayout.Button("Сбросить", EditorStyles.miniButton, GUILayout.Width(80)))
            {
                config.positionAt0 = Vector3.zero;
                config.positionAt100 = Vector3.zero;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // Применяем изменения
            if (newName != config.boneName || newPos0 != config.positionAt0 || newPos100 != config.positionAt100)
            {
                Undo.RecordObject(target, "Изменить конфиг синхронизации");
                config.boneName = newName;
                config.positionAt0 = newPos0;
                config.positionAt100 = newPos100;
                data.boneConfigs[index] = config;
                EditorUtility.SetDirty(target);
            }
        }
    }
}
