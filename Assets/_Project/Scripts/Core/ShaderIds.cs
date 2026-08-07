using UnityEngine;

namespace Overdraft.Core
{
    /// <summary>
    /// Cached shader property ids. Shader.PropertyToID does a string hash every call, and this
    /// project sets per-instance colour on every pooled actor — the string lookups add up fast
    /// at 300 enemies.
    /// </summary>
    public static class ShaderIds
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        public static readonly int Fill = Shader.PropertyToID("_Fill");
        public static readonly int GridColor = Shader.PropertyToID("_GridColor");
        public static readonly int GridSpacing = Shader.PropertyToID("_GridSpacing");
        public static readonly int GridLineWidth = Shader.PropertyToID("_GridLineWidth");
        public static readonly int Radius = Shader.PropertyToID("_Radius");
    }
}
