using System;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    public static class SpikeArt
    {
        public const string CatalogResource = "SpikeArtCatalog";
        private static SpikeArtCatalog _catalog;

        public static SpikeArtCatalog Catalog => _catalog != null ? _catalog : (_catalog = Resources.Load<SpikeArtCatalog>(CatalogResource));

        public static bool Has(string key) => Catalog != null && Catalog.Get(key) != null;

        /// <summary>Instantiates the prefab <paramref name="key"/> under <paramref name="parent"/> (local pose = identity), or returns null.</summary>
        public static GameObject Spawn(string key, Transform parent, bool stripColliders = true)
        {
            var prefab = Catalog != null ? Catalog.Get(key) : null;
            if (prefab == null) return null;
            var go = UnityEngine.Object.Instantiate(prefab, parent, false);
            go.name = key;
            if (stripColliders) StripColliders(go);
            return go;
        }

        /// <summary>The art prefabs carry colliders for the physics of their own scenes; the spike has none (the simulation decides) except for aiming.</summary>
        public static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        }

        /// <summary>The first descendant named <paramref name="name"/> (breadth first), or null.</summary>
        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            var queue = new System.Collections.Generic.Queue<Transform>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (t.name == name) return t;
                foreach (Transform c in t) queue.Enqueue(c);
            }
            return null;
        }

        /// <summary>
        /// Makes <paramref name="part"/> aimable with the server id <paramref name="id"/>: a box collider fitted to its mesh (so it moves with the part)
        /// and the <see cref="InteractableTarget"/> marker. The part's own renderer is lit by the aim view.
        /// </summary>
        public static void MakeAimable(Transform part, string id, float grow = 0.04f)
        {
            if (part == null) return;
            var filter = part.GetComponent<MeshFilter>();
            var box = part.gameObject.AddComponent<BoxCollider>();
            if (filter != null && filter.sharedMesh != null)
            {
                var b = filter.sharedMesh.bounds;
                box.center = b.center;
                box.size = b.size + Vector3.one * grow;
            }
            part.gameObject.AddComponent<InteractableTarget>().Id = id;
        }

        /// <summary>Like <see cref="MakeAimable"/> for every mesh in the subtree of <paramref name="root"/>.</summary>
        public static void MakeAllAimable(Transform root, string id, float grow = 0.04f)
        {
            if (root == null) return;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
                if (f.GetComponent<Collider>() == null) MakeAimable(f.transform, id, grow);
        }

        /// <summary>The art prefab key of a cargo item, by the kind the server sends (null: no art for it, the view draws a block).</summary>
        public static string KeyForCargo(string kind, bool heavy)
        {
            switch (kind)
            {
                case "fuel": return "FuelDrum";
                case "patch": return "HullPatch";
                case "bucket": return "Bucket";
                case "flashlight": return "Flashlight";
                case "manual": return "ManualOK114OpenBinder";
                default: return heavy ? "FuelDrum" : "CargoCrate_M";
            }
        }

        /// <summary>Lights every renderer under <paramref name="root"/> with a faint emission (waiting for a second carrier, aimed...), or switches it off.</summary>
        public static void GlowAll(Renderer[] renderers, Color color, bool on)
        {
            foreach (var r in renderers) Glow(r, color, on);
        }

        /// <summary>Lights an art part like a lamp: the VertexColorLit shader has an HDR emission colour (black = off).</summary>
        public static void Glow(Renderer renderer, Color color, bool on)
        {
            if (renderer == null) return;
            renderer.material.SetColor("_EmissionColor", on ? color * 2.5f : Color.black);
        }

        /// <summary>The point of the mesh of <paramref name="part"/> at the normalised position <paramref name="n"/> (each component -1..1 of its bounds), in the part's PARENT space.</summary>
        public static Vector3 BoundsPoint(Transform part, Vector3 n)
        {
            var filter = part.GetComponent<MeshFilter>();
            var b = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
            var local = b.center + Vector3.Scale(b.extents, n);
            return part.parent != null ? part.parent.InverseTransformPoint(part.TransformPoint(local)) : part.TransformPoint(local);
        }
    }

    /// <summary>A part of an art prefab that turns about a pivot (a lever, a flap, a handle): remembers its rest pose and applies a rotation about an axis of its parent space.</summary>
    public sealed class PartRig
    {
        private readonly Transform _part;
        private readonly Vector3 _basePosition, _pivot;
        private readonly Quaternion _baseRotation;

        public Transform Part => _part;
        public bool Valid => _part != null;

        /// <param name="pivotInParent">the pivot, in the part's parent space (see <see cref="SpikeArt.BoundsPoint"/>)</param>
        public PartRig(Transform part, Vector3 pivotInParent)
        {
            _part = part;
            if (part == null) return;
            _basePosition = part.localPosition; _baseRotation = part.localRotation; _pivot = pivotInParent;
        }

        /// <summary>A rig that turns about the part's own origin.</summary>
        public PartRig(Transform part) : this(part, part != null ? part.localPosition : Vector3.zero) { }

        public void Set(Vector3 axisInParent, float degrees)
        {
            if (_part == null) return;
            var q = Quaternion.AngleAxis(degrees, axisInParent);
            _part.localPosition = _pivot + q * (_basePosition - _pivot);
            _part.localRotation = q * _baseRotation;
        }
    }
}
