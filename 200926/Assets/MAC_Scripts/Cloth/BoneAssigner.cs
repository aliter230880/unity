using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
public class BoneAssigner : MonoBehaviour
{
    public void GenerateBones()
    {
        var targetSkinnedMesh = GetComponentInParent<AvatarObject>().rootSkinnedMeshRenderer;
        
        // 1. Создаем словарь для быстрого поиска костей по именам [citation:9]
        Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();
        foreach (Transform bone in targetSkinnedMesh.bones)
        {
            if (bone != null && !boneMap.ContainsKey(bone.name))
                boneMap[bone.name] = bone;
        }

        // 2. Получаем компонент футболки
        SkinnedMeshRenderer myRenderer = GetComponent<SkinnedMeshRenderer>();

        // 3. Создаем новый массив костей ТОГО ЖЕ РАЗМЕРА
        Transform[] newBones = new Transform[myRenderer.bones.Length];

        // 4. Для каждой позиции в массиве находим кость с таким же именем [citation:3]
        for (int i = 0; i < myRenderer.bones.Length; i++)
        {
            if (myRenderer.bones[i] == null)
                continue;
            
            string boneName = myRenderer.bones[i].name;

            if (boneMap.ContainsKey(boneName))
            {
                newBones[i] = boneMap[boneName]; // Подставляем кость из скелета персонажа
            }
            else
            {
                Debug.LogWarning("Кость не найдена: " + boneName);
                newBones[i] = myRenderer.bones[i]; // Оставляем старую (но она не будет работать)
            }
        }

        // 5. Назначаем новые кости и корневую кость
        myRenderer.bones = newBones;
        myRenderer.rootBone = targetSkinnedMesh.rootBone; // Используем rootBone от тела
    }
}
}
