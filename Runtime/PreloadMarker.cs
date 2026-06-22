using UnityEngine;

namespace Vex.Preload
{
    public enum PreloadKind
    {
        Host,
        Content
    }

    [DisallowMultipleComponent]
    public sealed class PreloadMarker : MonoBehaviour
    {
        public PreloadKind Kind;
    }
}