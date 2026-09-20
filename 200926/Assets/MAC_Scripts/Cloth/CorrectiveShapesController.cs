using UnityEngine;

namespace MagicAvatarCreator
{
public class CorrectiveShapesController : MonoBehaviour
{
     // Ссылка на ваш меш
    public Transform bone;                  // Кость, за которой следим
    public string blendShapeName;           // Имя коррективной формы
    public float maxAngle = 45f;             // Угол, при котором форма включается полностью

    private int _blendShapeIndex;
    private float _initialAngle;
    private SkinnedMeshRenderer _skinnedMesh;

    void Start()
    {
        _skinnedMesh = GetComponent<SkinnedMeshRenderer>();

        if (_skinnedMesh == null || _skinnedMesh.sharedMesh == null)
        {
            Debug.LogWarning("[CorrectiveShapesController] SkinnedMeshRenderer not found on " + gameObject.name);
            enabled = false;
            return;
        }

        _blendShapeIndex = _skinnedMesh.sharedMesh.GetBlendShapeIndex(blendShapeName);
        
        if (_blendShapeIndex == -1)
        {
            Debug.LogError($"Blend Shape '{blendShapeName}' not found!");
            enabled = false;
        }
    }

    void Update()
    {
        if (_skinnedMesh == null || _blendShapeIndex < 0 || bone == null) return;

        float currentAngle = bone.localEulerAngles.x;
        // Normalize 0-360 range to signed angle
        if (currentAngle > 180f) currentAngle -= 360f;
        float weight = Mathf.Clamp01(Mathf.Abs(currentAngle) / maxAngle);

        _skinnedMesh.SetBlendShapeWeight(_blendShapeIndex, weight * 100f);
    }
}
}
