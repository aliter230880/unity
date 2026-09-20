using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MagicAvatarCreator
{
    public class BlendShapeController
    {
        private const string StartBlendShapeName = "Muscular";
        
        public Dictionary<string, int> BlendShapesMap { get; set; } = new();
        
        private Dictionary<SkinnedMeshRenderer, Dictionary<string, int>> _dependentBlendShapesMaps { get; set; } = new();
        private readonly AvatarObject _avatarObject;

        public BlendShapeController(AvatarObject avatarObject)
        {
            _avatarObject = avatarObject;
            
            Init();
        }

        private void Init()
        {
            BuildBlendShapesMap();
        }
        
        private void BuildBlendShapesMap()
        {
            var foundStart = false;
            BlendShapesMap.Clear();
            _dependentBlendShapesMaps.Clear();
            
            for (var i = 0; i < _avatarObject.rootSkinnedMeshRenderer.sharedMesh.blendShapeCount; i++)
            {
                var rootBlendShapeName = _avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeName(i);
                
                if (!foundStart)
                {
                    if (rootBlendShapeName != StartBlendShapeName) continue;
                    foundStart = true;
                }

                BlendShapesMap[_avatarObject.rootSkinnedMeshRenderer.sharedMesh.GetBlendShapeName(i)] = i;

                foreach (var dependentMesh in _avatarObject.dependentSkinnedMeshRenderers)
                {
                    _dependentBlendShapesMaps.TryGetValue(dependentMesh, out var dependentBlendShapesMap);

                    dependentBlendShapesMap ??= new Dictionary<string, int>();

                    for (var j = 0; j < dependentMesh.sharedMesh.blendShapeCount; j++)
                    {
                        var currentName = dependentMesh.sharedMesh.GetBlendShapeName(j);

                        if (currentName != rootBlendShapeName) continue;
                        
                        dependentBlendShapesMap[currentName] = j;
                        break;
                    }
                    
                    _dependentBlendShapesMaps[dependentMesh] = dependentBlendShapesMap;
                }
            }
        }
        
        public void SynchronizeBlendShapes()
        {
            foreach (var (blendShapeName, blendShapeIndex) in BlendShapesMap)
            {
                var avatarBlendShapeValue = _avatarObject.rootSkinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);
            
                foreach (var attachedClothElement in _avatarObject.AvatarClothManager.AttachedClothElements)
                {
                    var clothSkinnedMesh = attachedClothElement.Value.clothSkinnedMesh;
                    var clothBlendShapeIndex = clothSkinnedMesh.sharedMesh.GetBlendShapeIndex(blendShapeName);

                    if (clothBlendShapeIndex >= 0)
                    {
                        clothSkinnedMesh.SetBlendShapeWeight(clothBlendShapeIndex, avatarBlendShapeValue);
                    }
                }
            }
        }
    } 
}
