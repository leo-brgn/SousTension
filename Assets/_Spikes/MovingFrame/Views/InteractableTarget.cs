using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// Marks a collider as an interactable: its <see cref="Id"/> is the id the server understands in the input field <c>use</c> (E2-02,
    /// server/src/controls.js: <c>interactableIds()</c>). The aim view raycasts against these; several parts of one object (a valve's body and its
    /// wheel) share one id and are highlighted together.
    /// </summary>
    public sealed class InteractableTarget : MonoBehaviour
    {
        public string Id;

        public static readonly List<InteractableTarget> All = new List<InteractableTarget>();

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Make <paramref name="go"/> aimable with the given server id: exactly one box collider, plus the marker. Returns the object.</summary>
        public static GameObject Mark(GameObject go, string id)
        {
            var old = go.GetComponent<Collider>();
            if (old != null) DestroyImmediate(old);          // views remove the primitive's collider (Destroy is deferred): replace it now
            go.AddComponent<BoxCollider>();
            go.AddComponent<InteractableTarget>().Id = id;
            return go;
        }
    }

    /// <summary>
    /// Every fixed interactable id of the boat, in the form the server accepts (the leaks, "leak:&lt;n&gt;", are dynamic). The list MUST stay equal to
    /// <c>interactableIds()</c> of server/src/controls.js: a server test compares the two between the markers below.
    /// </summary>
    public static class InteractableIds
    {
        public static readonly string[] Breakers =
        {
            // IDS-BREAKERS-BEGIN
            "breaker_light1", "breaker_light2", "breaker_light3", "breaker_light4", "breaker_light5", "breaker_light6",
            "breaker_bilge0", "breaker_bilge1", "breaker_sonar", "breaker_radio", "breaker_cipher", "breaker_ventilation",
            "breaker_galley", "breaker_samovar", "breaker_coffee", "breaker_shower", "breaker_periscope", "breaker_interphone",
            "breaker_heater", "breaker_pneumatic",
            // IDS-BREAKERS-END
        };

        public static readonly string[] Others =
        {
            // IDS-OTHERS-BEGIN
            "item", "regime", "scram", "valve0", "valve1", "valve2", "valve3", "pump0", "pump1", "bilge0", "bilge1", "tele_up", "tele_down",
            "cc:demo:0", "cc:demo:1", "cc:demo2:0", "cc:demo2:1", "cc:reactor_restart:0", "cc:reactor_restart:1",
            // IDS-OTHERS-END
        };

        public static string Valve(int i) => "valve" + i;
        public static string Pump(int i) => "pump" + i;
        public static string Bilge(int i) => "bilge" + i;
        public static string Leak(int id) => "leak:" + id;
        public static string Command(string action, int side) => "cc:" + action + ":" + side;
    }
}
