// ---- Alarms (E3-09a, docs/design/alarm-system.md) -------------------------------------------------------------------------------
// One alarm = one DATA entry of ALARMS: adding an alarm adds no logic. The engine below evaluates the catalogue once per tick (simStep step 12), in
// catalogue order, from the game state only (deterministic, no randomness). An alarm turns ON when its `when` is true and OFF when its `clear` is true;
// `clear` defaults to "not when" and is given explicitly with a margin (hysteresis) where the condition sits on a threshold, so a value hovering at the
// threshold does not make the lamp flicker. The active alarms are broadcast every tick (`al`), so a client that reconnects gets them (rule 6).
//   system   : "reactor" | "power" | "water" | "propulsion" ...
//   severity : "warn" (amber) | "crit" (red: reserved for real emergencies, GDD design rules)
//   page     : the OK-114 manual page that handles it (E5-04 will open the manual there; ids of manual.js, placeholders until E5-05 writes the procedures)
// Acknowledging (silencing) an alarm is alarmAck(); it never changes the simulation.
function reactorMinValve(r) { return Math.min(r.valves[0], r.valves[1], r.valves[2], r.valves[3]); }

var ALARMS = [
  { id: "rx_temp_warn", system: "reactor", severity: "warn", page: "scram",
    when: function (s) { return s.reactor.T >= REACTOR_K.tWarn; }, clear: function (s) { return s.reactor.T < REACTOR_K.tWarn - 3; } },
  { id: "rx_temp_crit", system: "reactor", severity: "crit", page: "scram",
    when: function (s) { return s.reactor.T >= REACTOR_K.tCrit; }, clear: function (s) { return s.reactor.T < REACTOR_K.tCrit - 3; } },
  { id: "rx_temp_low", system: "reactor", severity: "warn", page: "index",                     // the core runs colder than its inlet allows: the primary is over-cooled
    when: function (s) { return !s.reactor.scram && s.reactor.T < REACTOR_K.tIn + 8; }, clear: function (s) { return s.reactor.scram || s.reactor.T >= REACTOR_K.tIn + 12; } },
  { id: "rx_valve_low", system: "reactor", severity: "warn", page: "restart",                  // a valve has drifted shut (any one): less cooling
    when: function (s) { return !s.reactor.scram && reactorMinValve(s.reactor) < 0.4; }, clear: function (s) { return s.reactor.scram || reactorMinValve(s.reactor) >= 0.5; } },
  { id: "rx_flow_low", system: "reactor", severity: "warn", page: "restart",
    when: function (s) { return !s.reactor.scram && s.reactor.flow < 0.6; }, clear: function (s) { return s.reactor.scram || s.reactor.flow >= 0.7; } },
  { id: "rx_pump0_broken", system: "reactor", severity: "warn", page: "restart", when: function (s) { return s.reactor.pumpBroken[0]; } },
  { id: "rx_pump1_broken", system: "reactor", severity: "warn", page: "restart", when: function (s) { return s.reactor.pumpBroken[1]; } },
  { id: "rx_scram_auto", system: "reactor", severity: "crit", page: "restart", when: function (s) { return s.reactor.autoScram; } }
];

// ---- Electricity (E3-09b): "low" alarms and their symmetric "high" ones (an over-correction rings too, GDD 3.2) ----------------------------------
function anyTripped(s) { for (var i = 0; i < s.power.br.length; i++) if (s.power.br[i].tripped) return true; return false; }
ALARMS.push(
  { id: "pw_v_low", system: "power", severity: "warn", page: "power",
    when: function (s) { return s.power.V < 0.5; }, clear: function (s) { return s.power.V >= 0.6; } },
  { id: "pw_v_dark", system: "power", severity: "crit", page: "power",                             // the grid is dark: the lights are out
    when: function (s) { return s.power.V < DARK_V; }, clear: function (s) { return s.power.V >= DARK_V + 0.1; } },
  { id: "pw_emergency_bus", system: "power", severity: "warn", page: "power",                     // dark grid, the battery feeds the red lamps
    when: function (s) { return s.power.V < DARK_V && s.power.B > 0; }, clear: function (s) { return s.power.V >= DARK_V + 0.1 || s.power.B <= 0; } },
  { id: "pw_battery_low", system: "power", severity: "warn", page: "power",
    when: function (s) { return s.power.B < 0.2; }, clear: function (s) { return s.power.B >= 0.3; } },
  { id: "pw_breaker_tripped", system: "power", severity: "warn", page: "power", when: anyTripped },
  { id: "pw_overload", system: "power", severity: "warn", page: "power",                           // the closed breakers ask for more than the turbine leaves
    when: function (s) { return !s.reactor.scram && s.power.V < 0.95 && gridDemand(s.power) > gridSurplus(s.reactor) + 0.05; },
    clear: function (s) { return s.reactor.scram || s.power.V >= 0.97 || gridDemand(s.power) <= gridSurplus(s.reactor); } },
  { id: "pw_essential_short", system: "power", severity: "warn", page: "power",                    // the primary pumps do not get their electricity: the cooling suffers
    when: function (s) { return !s.reactor.scram && s.reactor.eta < 0.9; }, clear: function (s) { return s.reactor.scram || s.reactor.eta >= 0.95; } },
  { id: "pw_surplus_high", system: "power", severity: "warn", page: "power",                        // far more electricity than the boat uses: the plant runs too hot for the load
    when: function (s) { return s.power.V > 0.99 && gridSurplus(s.reactor) > gridDemand(s.power) * 1.5; },
    clear: function (s) { return s.power.V <= 0.99 || gridSurplus(s.reactor) <= gridDemand(s.power) * 1.3; } }
);

// ---- Water and hull (E3-09b): one alarm per compartment for a leak, a rising level and a flooded compartment; then trim, list and the bilge pumps ----
(function waterAlarms() {
  function fill(s, i) { return s.water.comps[i].w / WATER_COMPARTMENTS[i].volume; }
  function leakIn(s, i) { for (var k = 0; k < s.leaks.list.length; k++) if (s.leaks.list[k].comp === i) return true; return false; }
  for (var c = 0; c < WATER_COMPARTMENTS.length; c++) {
    (function (i) {
      ALARMS.push({ id: "wt_leak_" + (i + 1), system: "water", severity: "warn", page: "leak", compartment: i, when: function (s) { return leakIn(s, i); } });
      ALARMS.push({ id: "wt_level_" + (i + 1), system: "water", severity: "warn", page: "leak", compartment: i,
        when: function (s) { return fill(s, i) >= 0.25; }, clear: function (s) { return fill(s, i) < 0.2; } });
      ALARMS.push({ id: "wt_flood_" + (i + 1), system: "water", severity: "crit", page: "leak", compartment: i,
        when: function (s) { return fill(s, i) >= 0.9; }, clear: function (s) { return fill(s, i) < 0.8; } });
    })(c);
  }
  ALARMS.push(
    { id: "wt_trim_high", system: "water", severity: "warn", page: "leak",
      when: function (s) { return Math.abs(waterTilt(s.water).trim) >= 2; }, clear: function (s) { return Math.abs(waterTilt(s.water).trim) < 1.5; } },
    { id: "wt_list_high", system: "water", severity: "warn", page: "leak",
      when: function (s) { return Math.abs(waterTilt(s.water).list) >= 2.5; }, clear: function (s) { return Math.abs(waterTilt(s.water).list) < 2; } },
    { id: "wt_bilge0_broken", system: "water", severity: "warn", page: "leak", when: function (s) { return s.bilge.pumps[0].broken; } },
    { id: "wt_bilge1_broken", system: "water", severity: "warn", page: "leak", when: function (s) { return s.bilge.pumps[1].broken; } }
  );
})();

// The annunciator: the lamp panel on the right wall of the central post (E3-09c). Pressing a lamp acknowledges that alarm; the player must be at the panel.
var ANNUNCIATOR = { x: 2.7, z: 6.6, reach: 2.0 };

function newAlarms() {
  var list = [];
  for (var i = 0; i < ALARMS.length; i++) list.push({ on: false, since: 0, ack: false, count: 0 });
  return { list: list };
}

// One tick: raise and clear alarms. `tick` is the simulation tick (stored in `since` so views can show how long an alarm has been ringing).
function alarmsStep(state, tick) {
  var al = state.alarms.list, show = -1;
  for (var i = 0; i < ALARMS.length; i++) {
    var a = al[i], def = ALARMS[i];
    if (!a.on) {
      if (def.when(state)) {
        a.on = true; a.since = tick; a.ack = false; a.count += 1;
        if (show < 0 || (def.severity === "crit" && ALARMS[show].severity !== "crit")) show = i;      // a red alarm beats an amber one raised in the same tick
      }
    } else if (def.clear ? def.clear(state) : !def.when(state)) {
      a.on = false; a.ack = false;
    }
  }
  if (show >= 0) manualGoto(state, ALARMS[show].page);              // E5-04: the Manual opens by itself on the page of the alarm that just rang (GDD 3.5)
}

// A player silences an alarm (one click, no game effect). Returns true when something was acknowledged.
function alarmAck(state, id) {
  for (var i = 0; i < ALARMS.length; i++) {
    if (ALARMS[i].id === id && state.alarms.list[i].on && !state.alarms.list[i].ack) { state.alarms.list[i].ack = true; return true; }
  }
  return false;
}

// Broadcast (and resync): the ids of the active alarms in catalogue order, '!' prefix = acknowledged. [] when all is quiet.
function alarmsView(alarms) {
  var out = [];
  for (var i = 0; i < ALARMS.length; i++) if (alarms.list[i].on) out.push((alarms.list[i].ack ? "!" : "") + ALARMS[i].id);
  return out;
}
