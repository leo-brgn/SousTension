// ---- Coupled actions: the Rule of Two Players framework (E4-02) ----------------------------------------------------
// A critical action needs TWO commands, far apart, activated by two DIFFERENT players within a window of 3 s (GDD 3.4). Actions are
// DATA (COUPLED_ACTIONS); the engine below is generic: it tracks each action independently (several can be pending at once), validates
// on the server only, and calls the action's effect once per successful pair. Nothing critical may ever be executable by one player.
//
// A command is { x, z, reach, kind }. kind "press" (one key press arms the command) is the only kind for now; "hold" (cranks, pedals)
// will be added with the first action that needs it.
//
// Latency tolerance: the official window is 3 s (windowTicks, what the diegetic feedback counts down). The server accepts a second press up
// to COUPLED_GRACE_TICKS later (0.3 s) so that two players with different latency still make the window when they press in time.
var COUPLED_GRACE_TICKS = 3;

var COUPLED_ACTIONS = [
  // Spike demo pair: the two keys at the ends of the boat (kept from the E1-01 interlock).
  { id: "demo", windowTicks: INTERLOCK_WINDOW_TICKS,
    a: { x: STATIONS[0].x, z: STATIONS[0].z, reach: STATION_REACH, kind: "press" },
    b: { x: STATIONS[1].x, z: STATIONS[1].z, reach: STATION_REACH, kind: "press" } },
  // Second demo pair on the opposite diagonal: lets two pairs of players work independently at the same time.
  { id: "demo2", windowTicks: INTERLOCK_WINDOW_TICKS,
    a: { x: -2.5, z: 6.0, reach: STATION_REACH, kind: "press" },
    b: { x: 2.5, z: -7.0, reach: STATION_REACH, kind: "press" } },
  // Restart of the reactor after a SCRAM (E3-05): near the RK-1 panel and at the bow, 13 m apart.
  { id: "reactor_restart", windowTicks: INTERLOCK_WINDOW_TICKS,
    a: { x: -2.5, z: -5.0, reach: STATION_REACH, kind: "press" },
    b: { x: 2.5, z: 8.5, reach: STATION_REACH, kind: "press" } }
];

// Effects of successful actions: id -> function(state, actionId, tick). Registered by the systems that own the action (E3-05 restart...).
var COUPLED_EFFECTS = {};

function newCoupled() {
  var acts = [];
  for (var i = 0; i < COUPLED_ACTIONS.length; i++)
    acts.push({ st: [{ by: "", tick: 0 }, { by: "", tick: 0 }], result: "none", resultTick: 0, count: 0 });
  return { acts: acts };
}

// The nearest command (of any action) within its reach: { ai, side, d } or null.
function nearestCommand(pl) {
  var best = null;
  for (var i = 0; i < COUPLED_ACTIONS.length; i++) {
    for (var s = 0; s < 2; s++) {
      var c = s === 0 ? COUPLED_ACTIONS[i].a : COUPLED_ACTIONS[i].b;
      var dx = pl.x - c.x, dz = pl.z - c.z;
      var d = Math.sqrt(dx * dx + dz * dz);
      if (d <= c.reach && (best === null || d < best.d)) best = { ai: i, side: s, d: d };
    }
  }
  return best;
}

// A player presses the key at a command. Rejected when out of reach, when the command is already armed, or when the same
// player already holds the other command of that action (the rule needs two different players).
function tryActivate(cp, id, pl, tick) {
  var c = nearestCommand(pl);
  if (c === null) return;
  armCommand(cp, id, tick, c.ai, c.side);
}

// Arm command `side` of action `ai` for player `id` (shared by the nearest-command path and the aimed one, E2-02).
function armCommand(cp, id, tick, ai, side) {
  var act = cp.acts[ai];
  if (act.st[side].by !== "") return;
  if (act.st[1 - side].by === id) return;
  act.st[side].by = id; act.st[side].tick = tick;
}

// Success when both commands of an action are armed (by different players, guaranteed by tryActivate): the pair is released and the
// action's effect runs once. An armed command expires once the window plus the latency grace has passed.
function evaluateCoupled(state, tick) {
  var cp = state.cp;
  for (var i = 0; i < COUPLED_ACTIONS.length; i++) {
    var def = COUPLED_ACTIONS[i], act = cp.acts[i];
    var a = act.st[0], b = act.st[1];
    if (a.by !== "" && b.by !== "") {
      act.result = "success"; act.resultTick = tick; act.count++;
      a.by = ""; b.by = "";
      var effect = COUPLED_EFFECTS[def.id];
      if (effect) effect(state, def.id, tick);
      continue;
    }
    for (var s = 0; s < 2; s++) {
      var st = act.st[s];
      if (st.by !== "" && tick - st.tick >= def.windowTicks + COUPLED_GRACE_TICKS) {
        act.result = "timeout"; act.resultTick = tick; st.by = "";
      }
    }
  }
}

// A player left the match: release every command they had armed.
function releaseCoupled(cp, id) {
  for (var i = 0; i < cp.acts.length; i++)
    for (var s = 0; s < 2; s++) if (cp.acts[i].st[s].by === id) cp.acts[i].st[s].by = "";
}

// Broadcast (also the full resync after a reconnection). a / b = ticks left of the official window for each armed command.
function coupledView(cp, tick) {
  var out = [];
  for (var i = 0; i < COUPLED_ACTIONS.length; i++) {
    var def = COUPLED_ACTIONS[i], act = cp.acts[i];
    var rem = function (s) { return s.by === "" ? 0 : Math.max(0, def.windowTicks - (tick - s.tick)); };
    out.push({ id: def.id, a: rem(act.st[0]), b: rem(act.st[1]), ab: act.st[0].by, bb: act.st[1].by, result: act.result, rt: act.resultTick, n: act.count });
  }
  return out;
}
