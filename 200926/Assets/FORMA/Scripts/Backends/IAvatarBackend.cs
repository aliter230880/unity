using UnityEngine;

namespace Forma
{
    /// <summary>
    /// Единый контракт источника аватаров для панели FORMA. Реализации:
    /// MacAvatarAdapter (MAC-аватары) и ReferenceAvatarLoader+Customizer (GLB/OBJ эталоны).
    /// Apply — параметры → аватар; Capture — аватар → параметры (обратная связь UI).
    /// </summary>
    public interface IAvatarBackend
    {
        string DebugName { get; }
        bool IsReady { get; }
        void SetSex(bool male);
        void Apply(AvatarParams p);
        AvatarParams Capture();
        void Hide();
    }
}
