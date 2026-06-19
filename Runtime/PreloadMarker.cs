namespace Vex.Preload
{
    using UnityEngine;

    public enum PreloadKind
    {
        Host,
        Content,
    }

    [DisallowMultipleComponent]
    public sealed class PreloadMarker : MonoBehaviour
    {
        public PreloadKind Kind;
    }
}
