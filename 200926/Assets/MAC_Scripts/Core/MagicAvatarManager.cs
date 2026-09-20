using UnityEngine;
using UnityEngine.Events;

namespace  MagicAvatarCreator
{
    public class MagicAvatarManager : MonoBehaviour
    {
        public enum AvatarType
        {
            Male,
            Female
        }
        public enum BlendShapeType
        {
            Body,
            Head,
            Face
        }

        public AvatarDataManager.DataStoringType dataStoringType;
        public GameObject femaleAvatarBase;
        public GameObject maleAvatarBase;
        
        public AvatarType CurrentAvatarType { get; set; }
        public AvatarObject CurrentAvatar { get; set; }
        public SkinnedMeshRenderer CurrentMeshRenderer { get; set; }
        public CameraFocusController CameraFocusController { get; set; }
        
        public UnityAction<AvatarType> onAvatarTypeChanged;

        public void InitializeAvatar(string avatarName)
        {
            if (string.IsNullOrEmpty(avatarName))
            {
                Debug.LogError("Avatar name is empty");
                return;
            }
  
            LoadOrDefaultAvatar(avatarName);
        }

        private void LoadOrDefaultAvatar(string avatarName)
        {
            SetActiveAvatar(AvatarDataManager.TryLoadData(dataStoringType, avatarName));
        }

        private void SetActiveAvatar(AvatarType avatarType)
        {
            var emptyAvatarData = ScriptableObject.CreateInstance<AvatarData>();

            emptyAvatarData.avatarType = avatarType;
            
            SetActiveAvatar(emptyAvatarData);
        }

        private void SetActiveAvatar(AvatarData avatarData)
        { 
            // NUCLEAR CLEANUP: destroy ALL AvatarObject instances and any leftover avatar meshes
            var allAvatars = FindObjectsByType<AvatarObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var avatar in allAvatars)
            {
                Destroy(avatar.gameObject);
            }
            
            // Also destroy any orphan "Avatar Base" objects (scene-placed prefabs)
            var avatarBases = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var obj in avatarBases)
            {
                if (obj.name.Contains("Avatar Base") || obj.name.Contains("AvatarBase"))
                {
                    Destroy(obj);
                }
            }

            var avatarType = avatarData ? avatarData.avatarType : AvatarType.Male;
            
            GameObject avatarGameObject;
            AvatarObject avatarObject;

            switch (avatarType)
            {
                case AvatarType.Male:
                    avatarGameObject = Instantiate(maleAvatarBase);
                    avatarObject = avatarGameObject.GetComponent<AvatarObject>();

                    if (!avatarObject)
                    {
                        avatarObject = avatarGameObject.GetComponentInChildren<AvatarObject>();
                        
                        if (!avatarObject)
                        {
                            Debug.LogError("AvatarObject component is missing!");
                            return;
                        }
                    }
                    
                    CurrentAvatar = avatarObject;
                    break;
                case AvatarType.Female:
                    avatarGameObject = Instantiate(femaleAvatarBase);
                    avatarObject = avatarGameObject.GetComponent<AvatarObject>();

                    if (!avatarObject)
                    {
                        avatarObject = avatarGameObject.GetComponentInChildren<AvatarObject>();
                        
                        if (!avatarObject)
                        {
                            Debug.LogError("AvatarObject component is missing!");
                            return;
                        }
                    }
                    
                    CurrentAvatar = avatarObject;
                    break;
                default:
                    return;
            }

            CurrentAvatarType = avatarType;
            CurrentMeshRenderer = CurrentAvatar.GetComponentInChildren<SkinnedMeshRenderer>();
            CameraFocusController = CurrentAvatar.GetComponentInChildren<CameraFocusController>();

            CurrentAvatar.Init(avatarData);
            CurrentAvatar.gameObject.SetActive(true);
            
            onAvatarTypeChanged?.Invoke(CurrentAvatarType);
        }

        public void ChangeAvatarType(int type)
        {
            switch (type)
            {
                case 0:
                    SetActiveAvatar(AvatarType.Male);
                    break;
                case 1:
                    SetActiveAvatar(AvatarType.Female);
                    break;
            }
        }
        
        public void ApplyBlendShape(int parameterIndex, float value)
        {
            CurrentMeshRenderer.SetBlendShapeWeight(parameterIndex, value);

            var avatarBlendShapeName = CurrentMeshRenderer.sharedMesh.GetBlendShapeName(parameterIndex);

            foreach (var attachedClothElement in CurrentAvatar.AvatarClothManager.AttachedClothElements)
            {
                var clothSkinnedMesh = attachedClothElement.Value.clothSkinnedMesh;
                var clothBlendShapeIndex = clothSkinnedMesh.sharedMesh.GetBlendShapeIndex(avatarBlendShapeName);

                if (clothBlendShapeIndex >= 0)
                {
                    clothSkinnedMesh.SetBlendShapeWeight(clothBlendShapeIndex, value);
                }
            }
        }
    }
}
