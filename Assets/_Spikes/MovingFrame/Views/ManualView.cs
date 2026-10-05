using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The OK-114 Operating Manual in the local player's hands (E5-02), diegetic: while the player holds the binder, a page is drawn in front of
    /// the camera (a quad with text), as the book they hold: no HUD. The page comes from the authoritative state (everybody reads the same page);
    /// the arrow keys turn it. The texts below are PLACEHOLDERS in French under localization keys (manual.&lt;page&gt;.title, manual.&lt;page&gt;.step&lt;k&gt;):
    /// the real 9-language localization is another epic. The page list MUST stay equal to MANUAL_PAGES of server/src/manual.js (a server test
    /// compares the two between the markers below). Built from primitives for the spike; the binder art and typography are E11 / E15.
    /// </summary>
    public sealed class ManualView : MonoBehaviour
    {
        // PAGES-BEGIN
        public static readonly (string id, int steps)[] Pages =
        {
            ("index", 0), ("scram", 2), ("restart", 4), ("leak", 4), ("power", 3), ("propulsion", 2),
        };
        // PAGES-END

        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>
        {
            { "manual.index.title", "Sommaire" },
            { "manual.scram.title", "Arrêt d'urgence (SCRAM)" },
            { "manual.scram.step1", "Soulever le capot plombé." },
            { "manual.scram.step2", "Tirer le gros levier rouge : toute l'électricité est coupée." },
            { "manual.restart.title", "Redémarrage du réacteur" },
            { "manual.restart.step1", "Remettre le levier SCRAM en place." },
            { "manual.restart.step2", "Rouvrir les quatre vannes (tenir le volant)." },
            { "manual.restart.step3", "Mettre les deux pompes en marche." },
            { "manual.restart.step4", "À deux : tourner les deux clés éloignées en moins de 3 secondes." },
            { "manual.leak.title", "Fuite de coque" },
            { "manual.leak.step1", "Prendre un patch dans la caisse à outils." },
            { "manual.leak.step2", "Le porter jusqu'à la fuite." },
            { "manual.leak.step3", "Cliquer sur la fuite : un patch par cran de taille." },
            { "manual.leak.step4", "Écoper au seau si l'eau monte." },
            { "manual.power.title", "Disjoncteurs" },
            { "manual.power.step1", "Repérer le disjoncteur sauté (rouge) au tableau arrière." },
            { "manual.power.step2", "Le réarmer d'un appui." },
            { "manual.power.step3", "Si la tension chute, couper les consommateurs inutiles." },
            { "manual.propulsion.title", "Télégraphe machine" },
            { "manual.propulsion.step1", "Monter ou descendre le levier d'un cran." },
            { "manual.propulsion.step2", "Plus de vitesse, moins de lumière : surveiller la tension." },
        };

        /// <summary>The text for a localization key (the key itself when it is missing, so a gap is visible).</summary>
        public static string Text(string key) => Strings.TryGetValue(key, out var s) ? s : key;

        /// <summary>The whole page as plain text: title, then its steps numbered (the index lists the other pages).</summary>
        public static string PageText(int page)
        {
            if (page < 0 || page >= Pages.Length) return "";
            var (id, steps) = Pages[page];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Text("manual." + id + ".title"));
            sb.AppendLine();
            if (id == "index")
                for (int i = 1; i < Pages.Length; i++) sb.AppendLine(i + ". " + Text("manual." + Pages[i].id + ".title"));
            else
                for (int k = 1; k <= steps; k++) sb.AppendLine(k + ". " + Text("manual." + id + ".step" + k));
            sb.AppendLine();
            sb.Append((page + 1) + " / " + Pages.Length);
            return sb.ToString();
        }

        private MovingFrameModel _model;
        private Transform _book;
        private TextMesh _text;

        public void Bind(MovingFrameModel model, Camera camera)
        {
            _model = model;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ManualInHands";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0f, -0.06f, 0.6f);
            go.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            go.transform.localScale = new Vector3(0.55f, 0.4f, 0.01f);
            go.GetComponent<Renderer>().material.color = new Color(0.93f, 0.9f, 0.78f);
            _book = go.transform;

            var textGo = new GameObject("Page");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(-0.46f, 0.44f, -0.6f);
            textGo.transform.localScale = new Vector3(1f / 0.55f, 1f / 0.4f, 1f / 0.01f) * 0.55f * 0.01f * 3f;   // undo the book's squash
            _text = textGo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            textGo.GetComponent<MeshRenderer>().material = font.material;
            _text.fontSize = 48; _text.characterSize = 0.1f; _text.color = new Color(0.1f, 0.08f, 0.05f);
            _text.anchor = TextAnchor.UpperLeft;
            go.SetActive(false);
        }

        private int _shownPage = -1;

        private void Update()
        {
            if (_model == null) return;
            bool reading = _model.IsLocalReading();
            if (_book.gameObject.activeSelf != reading) _book.gameObject.SetActive(reading);
            if (!reading) { _shownPage = -1; return; }
            int page = _model.Manual.Valid ? _model.Manual.Page : 0;
            if (page != _shownPage) { _shownPage = page; _text.text = PageText(page); }
        }
    }
}
