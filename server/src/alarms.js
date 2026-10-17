// ---- Alarms (E3-09a, docs/design/alarm-system.md) -------------------------------------------------------------------------------
// One alarm = one DATA entry of ALARMS: adding an alarm adds no logic. The engine below evaluates the catalogue once per tick (simStep step 12), in
// catalogue order, from the game state only (deterministic, no randomness). An alarm turns ON when its `when` is true and OFF when its `clear` is true;
// `clear` defaults to "not when" and is given explicitly with a margin (hysteresis) where the condition sits on a threshold, so a value hovering at the
// threshold does not make the lamp flicker. The active alarms are broadcast every tick (`al`), so a client that reconnects gets them (rule 6).
//   system   : "reactor" | "power" | "water" | "propulsion" ...
//   severity : "warn" (amber) | "crit" (red: reserved for real emergencies, GDD design rules)
//   page     : the OK-114 manual page that handles it (E5-04 will open the manual there; ids of manual.js, placeholders until E5-05 writes the procedures)
// Acknowledging (silencing) an alarm is alarmAck(); it never changes the simulation.
function reactorMeanValve(r) { return (r.valves[0] + r.valves[1] + r.valves[2] + r.valves[3]) / 4; }

var ALARMS = [
  { id: "rx_temp_warn", system: "reactor", severity: "warn", page: "scram",
    when: function (s) { return s.reactor.T >= REACTOR_K.tWarn; }, clear: function (s) { return s.reactor.T < REACTOR_K.tWarn - 3; } },
  { id: "rx_temp_crit", system: "reactor", severity: "crit", page: "scram",
    when: function (s) { return s.reactor.T >= REACTOR_K.tCrit; }, clear: function (s) { return s.reactor.T < REACTOR_K.tCrit - 3; } },
  { id: "rx_temp_low", system: "reactor", severity: "warn", page: "index",                     // the core runs colder than its inlet allows: the primary is over-cooled
    when: function (s) { return !s.reactor.scram && s.reactor.T < REACTOR_K.tIn + 8; }, clear: function (s) { return s.reactor.scram || s.reactor.T >= REACTOR_K.tIn + 12; } },
  { id: "rx_valve_low", system: "reactor", severity: "warn", page: "restart",                  // the valves have drifted shut: less cooling
    when: function (s) { return !s.reactor.scram && reactorMeanValve(s.reactor) < 0.5; }, clear: function (s) { return s.reactor.scram || reactorMeanValve(s.reactor) >= 0.6; } },
  { id: "rx_flow_low", system: "reactor", severity: "warn", page: "restart",
    when: function (s) { return !s.reactor.scram && s.reactor.flow < 0.6; }, clear: function (s) { return s.reactor.scram || s.reactor.flow >= 0.7; } },
  { id: "rx_pump0_broken", system: "reactor", severity: "warn", page: "restart", when: function (s) { return s.reactor.pumpBroken[0]; } },
  { id: "rx_pump1_broken", system: "reactor", severity: "warn", page: "restart", when: function (s) { return s.reactor.pumpBroken[1]; } },
  { id: "rx_scram_auto", system: "reactor", severity: "crit", page: "restart", when: function (s) { return s.reactor.autoScram; } }
];

function newAlarms() {
  var list = [];
  for (var i = 0; i < ALARMS.length; i++) list.push({ on: false, since: 0, ack: false, count: 0 });
  return { list: list };
}

// One tick: raise and clear alarms. `tick` is the simulation tick (stored in `since` so views can show how long an alarm has been ringing).
function alarmsStep(state, tick) {
  var al = state.alarms.list;
  for (var i = 0; i < ALARMS.length; i++) {
    var a = al[i], def = ALARMS[i];
    if (!a.on) {
      if (def.when(state)) { a.on = true; a.since = tick; a.ack = false; a.count += 1; }
    } else if (def.clear ? def.clear(state) : !def.when(state)) {
      a.on = false; a.ack = false;
    }
  }
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
