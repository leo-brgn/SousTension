using System;
using System.Collections.Generic;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame
{
    /// <summary>
    /// The texts of the screens (menu, lobby, briefing, debrief, pause), by key, in French and English. Every text the player reads goes through
    /// <see cref="Get"/>: nothing is written in the screens themselves. Placeholder for the Localization package (E15-06, 9 languages): same keys,
    /// the tables will move to string tables then. Language: <c>--lang=fr|en</c>, else the system language, else French.
    /// </summary>
    public static class UiText
    {
        public static readonly string[] Languages = { "fr", "en" };
        private static string _language;

        public static string Language
        {
            get
            {
                if (_language != null) return _language;
                string chosen = null;
                foreach (var a in Environment.GetCommandLineArgs()) if (a.StartsWith("--lang=")) chosen = a.Substring(7);
                if (chosen == null) chosen = Application.systemLanguage == SystemLanguage.French ? "fr" : (Application.systemLanguage == SystemLanguage.English ? "en" : "fr");
                return _language = Array.IndexOf(Languages, chosen) >= 0 ? chosen : "fr";
            }
            set { _language = value; }
        }

        public static IReadOnlyDictionary<string, string> Table(string language) => language == "en" ? En : Fr;

        /// <summary>The text of <paramref name="key"/> in the current language; the key itself when it is missing (visible in play, caught by a test).</summary>
        public static string Get(string key) => Table(Language).TryGetValue(key, out var text) ? text : key;

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        private static readonly Dictionary<string, string> Fr = new Dictionary<string, string>
        {
            ["ui.title"] = "SOUS TENSION",
            ["ui.subtitle"] = "Titre provisoire — sous-marin nucléaire, réglementaire et en panne",
            ["ui.play"] = "Jouer",
            ["ui.quit"] = "Quitter",
            ["ui.connecting"] = "Connexion au serveur…",
            ["ui.connected"] = "Connecté",
            ["ui.connectionFailed"] = "Serveur injoignable. Lancez le serveur (docker compose) puis relancez le jeu.",
            ["ui.lobby.title"] = "Salle d'attente",
            ["ui.lobby.crew"] = "Équipage",
            ["ui.lobby.you"] = "vous",
            ["ui.lobby.ready"] = "prêt",
            ["ui.lobby.notReady"] = "pas prêt",
            ["ui.lobby.needMore"] = "Il faut au moins {0} équipiers pour appareiller. Un sous-marin ne part pas seul.",
            ["ui.lobby.waiting"] = "En attente que tout l'équipage soit prêt.",
            ["ui.ready"] = "Je suis prêt",
            ["ui.unready"] = "Pas encore prêt",
            ["ui.controls.title"] = "Commandes",
            ["ui.controls.body"] = "Z Q S D : marcher — Souris : regarder — Clic gauche : manier ce que l'on vise — F : prendre — G : poser — H : poche — Clic droit : jeter — Flèches : pages du Manuel — Échap : pause",
            ["ui.briefing.title"] = "Ordre de mission",
            ["ui.briefing.objective"] = "Transit : amener le sous-marin à {0} m.",
            ["ui.briefing.tip1"] = "Le réacteur alimente tout. Au départ il est en veille : choisissez un régime au tableau RK-1.",
            ["ui.briefing.tip2"] = "Des alarmes sonneront. Le Manuel OK-114, au poste central, s'ouvre à la bonne page.",
            ["ui.briefing.tip3"] = "Les actions critiques se font à deux, depuis deux postes éloignés.",
            ["ui.briefing.countdown"] = "Appareillage dans {0} s",
            ["ui.debrief.success"] = "Mission accomplie",
            ["ui.debrief.failure"] = "Mission échouée",
            ["ui.debrief.why.arrived"] = "Le sous-marin est arrivé à destination.",
            ["ui.debrief.why.flooded"] = "Un compartiment est resté noyé trop longtemps.",
            ["ui.debrief.why.depth"] = "Le sous-marin a coulé sous la profondeur limite.",
            ["ui.debrief.note"] = "Note de patrouille",
            ["ui.debrief.stat.time"] = "Durée",
            ["ui.debrief.stat.distance"] = "Distance",
            ["ui.debrief.stat.faults"] = "Pannes",
            ["ui.debrief.stat.alarms"] = "Alarmes",
            ["ui.debrief.stat.fill"] = "Eau maximale",
            ["ui.debrief.stat.scrams"] = "SCRAM",
            ["ui.debrief.again"] = "Nouvelle patrouille",
            ["ui.debrief.waiting"] = "{0}/{1} prêts",
            ["ui.pause.title"] = "Pause",
            ["ui.pause.note"] = "Le jeu continue pour l'équipage.",
            ["ui.pause.resume"] = "Reprendre",
            ["ui.paused.title"] = "Sous-marin à l'arrêt",
            ["ui.paused.body"] = "Un équipier est parti. Le sous-marin ne navigue pas avec un seul marin : en attente d'un équipier.",
            ["ui.unit.m"] = "m",
            ["ui.unit.s"] = "s",
        };

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["ui.title"] = "SOUS TENSION",
            ["ui.subtitle"] = "Working title — a regulation nuclear submarine, out of order",
            ["ui.play"] = "Play",
            ["ui.quit"] = "Quit",
            ["ui.connecting"] = "Connecting to the server…",
            ["ui.connected"] = "Connected",
            ["ui.connectionFailed"] = "Server unreachable. Start the server (docker compose) and restart the game.",
            ["ui.lobby.title"] = "Waiting room",
            ["ui.lobby.crew"] = "Crew",
            ["ui.lobby.you"] = "you",
            ["ui.lobby.ready"] = "ready",
            ["ui.lobby.notReady"] = "not ready",
            ["ui.lobby.needMore"] = "At least {0} crew members are needed to sail. A submarine never leaves alone.",
            ["ui.lobby.waiting"] = "Waiting for the whole crew to be ready.",
            ["ui.ready"] = "I'm ready",
            ["ui.unready"] = "Not ready yet",
            ["ui.controls.title"] = "Controls",
            ["ui.controls.body"] = "W A S D: walk — Mouse: look — Left click: use what you aim at — F: take — G: put down — H: pocket — Right click: throw — Arrows: Manual pages — Esc: pause",
            ["ui.briefing.title"] = "Mission order",
            ["ui.briefing.objective"] = "Transit: bring the submarine {0} m on.",
            ["ui.briefing.tip1"] = "The reactor powers everything. It starts idling: pick a regime at the RK-1 panel.",
            ["ui.briefing.tip2"] = "Alarms will ring. The OK-114 Manual, in the central post, opens at the right page.",
            ["ui.briefing.tip3"] = "Critical actions take two people at two distant stations.",
            ["ui.briefing.countdown"] = "Casting off in {0} s",
            ["ui.debrief.success"] = "Mission accomplished",
            ["ui.debrief.failure"] = "Mission failed",
            ["ui.debrief.why.arrived"] = "The submarine reached its destination.",
            ["ui.debrief.why.flooded"] = "A compartment stayed flooded for too long.",
            ["ui.debrief.why.depth"] = "The submarine sank below its crush depth.",
            ["ui.debrief.note"] = "Patrol grade",
            ["ui.debrief.stat.time"] = "Time",
            ["ui.debrief.stat.distance"] = "Distance",
            ["ui.debrief.stat.faults"] = "Faults",
            ["ui.debrief.stat.alarms"] = "Alarms",
            ["ui.debrief.stat.fill"] = "Peak water",
            ["ui.debrief.stat.scrams"] = "SCRAMs",
            ["ui.debrief.again"] = "New patrol",
            ["ui.debrief.waiting"] = "{0}/{1} ready",
            ["ui.pause.title"] = "Pause",
            ["ui.pause.note"] = "The game goes on for the crew.",
            ["ui.pause.resume"] = "Resume",
            ["ui.paused.title"] = "Submarine stopped",
            ["ui.paused.body"] = "A crew member left. The submarine does not sail with one sailor: waiting for a crew member.",
            ["ui.unit.m"] = "m",
            ["ui.unit.s"] = "s",
        };
    }
}
