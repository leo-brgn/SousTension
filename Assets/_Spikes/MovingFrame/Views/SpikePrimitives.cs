using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Primitives for the spike with a URP material. <c>GameObject.CreatePrimitive</c> gives every object the built-in "Standard" material, which the
    /// Universal Render Pipeline cannot draw: the whole scene turns PINK. Every view creates its cubes, cylinders and quads through this factory,
    /// which assigns the URP Lit material stored in Resources (created by the editor menu SousTension &gt; Spike, see PlayLocal.EnsureMaterials);
    /// if the asset is missing it falls back to a URP Lit material built from the shader.
    /// </summary>
    public static class SpikePrimitives
    {
        public const string LitResource = "SpikeLit";
        public const string WaterResource = "SpikeWater";

        public static GameObject Create(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var lit = Lit();
            if (lit != null) go.GetComponent<Renderer>().sharedMaterial = lit;
            return go;
        }

        /// <summary>URP Lit material for opaque objects (callers set <c>renderer.material.color</c>: it becomes their own copy).</summary>
        public static Material Lit() => Resources.Load<Material>(LitResource) ?? FromShader("Universal Render Pipeline/Lit");

        /// <summary>URP Unlit transparent material for the water.</summary>
        public static Material Water() => Resources.Load<Material>(WaterResource) ?? FromShader("Universal Render Pipeline/Unlit");

        private static Material FromShader(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            return shader != null ? new Material(shader) : null;
        }
    }
}
