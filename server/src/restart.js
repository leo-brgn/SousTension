// ---- Reactor restart procedure after a SCRAM (E3-05) ----------------------------------------------------------------
// GDD 3.3: restarting is a two-player procedure, by torchlight, following the manual. Three preparation steps anyone can do, in any
// order (so the two players can split them), then a coupled action (Rule of Two Players, E4-02) that actually restarts the plant:
//   1. lever    : put the SCRAM lever back (one press at the lever while the reactor is SCRAMmed)
//   2. valves   : the four primary valves open to at least RESTART_VALVE_MIN (the wheels are held open, E3-06)
//   3. pumps    : both primary pumps running (not stopped, not broken)
//   4. restart  : the coupled action "reactor_restart" (two commands far apart, two different players, 3 s window)
// Step 4 is refused until 1-3 are done (result "refused", no penalty). Batteries (E3-07) and the manual pages (E5) are not modelled yet.
var RESTART_VALVE_MIN = 0.9;

function newRestart() { return { last: "none", lastTick: 0, count: 0 }; }

function restartSteps(state) {
  var r = state.reactor, v = r.valves, ok = true;
  for (var i = 0; i < 4; i++) if (v[i] < RESTART_VALVE_MIN) ok = false;
  return [
    state.lever.reset === true,
    ok,
    r.pumps[0] === true && r.pumps[1] === true && !r.pumpBroken[0] && !r.pumpBroken[1]
  ];
}

// Effect of the coupled action "reactor_restart": succeeds only on a SCRAMmed reactor whose three preparation steps are done.
function restartAttempt(state, id, tick) {
  var steps = restartSteps(state);
  if (state.reactor.scram && steps[0] && steps[1] && steps[2]) {
    reactorRestart(state.reactor);
    state.lever.reset = false; state.lever.cover = 0;
    state.restart.last = "success"; state.restart.count++;
  } else {
    state.restart.last = "refused";
  }
  state.restart.lastTick = tick;
}
COUPLED_EFFECTS.reactor_restart = restartAttempt;

// Broadcast (and resync): s = the three preparation steps (1 done), last = outcome of the latest attempt, lt = its tick, n = restarts so far.
function restartView(state) {
  var s = restartSteps(state);
  return { s: [s[0] ? 1 : 0, s[1] ? 1 : 0, s[2] ? 1 : 0], last: state.restart.last, lt: state.restart.lastTick, n: state.restart.count };
}
