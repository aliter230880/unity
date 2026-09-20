namespace Forma
{
    /// <summary>
    /// IAvatarBackend-обёртка над эталонными GLB/OBJ аватарами
    /// (ReferenceAvatarLoader + ReferenceAvatarCustomizer). Capture для эталонов
    /// недоступен (нет блендшейпов) — возвращаются исходные параметры.
    /// </summary>
    public class ReferenceBackend : IAvatarBackend
    {
        readonly ReferenceAvatarLoader _loader;
        readonly ReferenceAvatarCustomizer _customizer;
        AvatarParams _last;

        public ReferenceBackend(ReferenceAvatarLoader loader, ReferenceAvatarCustomizer customizer)
        {
            _loader = loader;
            _customizer = customizer;
        }

        public string DebugName => "Reference (GLB/OBJ)";
        public bool IsReady => _loader != null && _loader.Current != null;

        public void SetSex(bool male)
        {
            if (male) _loader?.SelectMale();
            else _loader?.SelectFemale();
        }

        public void Apply(AvatarParams p)
        {
            _last = p;
            _customizer?.ApplyFromParameters(p);
        }

        public AvatarParams Capture() => _last;
        public void Hide() => _loader?.HideCurrent();
    }
}
