// ---- The mission flow of a game: lobby -> briefing -> run -> debrief (MVP loop; GDD 3.1 simplified, issues E4-04, E9-02, E9-09) -----------------------
// One mission for the MVP, the TRANSIT: bring the boat MISSION_GOAL_M metres on while the faults pile up (faults.js).
//   lobby    the crew waits; players walk around; nothing is simulated. The game starts when at least MIN_PLAYERS (2: no solo game, GDD rule 5) are
//            there and EVERYONE has said "ready" (input field rdy).
//   briefing MISSION_BRIEFING_SECONDS: the objective is shown to everybody (the client draws it), then the run starts.
//   run      the simulation runs. The run ends on
//              success   dist >= MISSION_GOAL_M
//              failure   a compartment stays flooded (>= 90 %) for MISSION_FLOOD_FAIL_SECONDS, or the boat sinks below MISSION_CRUSH_DEPTH_M
//            If fewer than MIN_PLAYERS remain the run is PAUSED (the world is frozen, `pz`), it never continues with a lone player.
//   debrief  result, stats and a grade (stars: 3 clean, 2 hurt or slow, 1 saved from a flooded compartment, 0 failure); a new game starts when
//            everybody is ready again (the world is rebuilt: the damage of a run does not carry over yet, E6-09 / E1-08).
// Matches created by the tests and the tools start directly in "run", with no minimum of players and no objective (no lobby, the run never ends); the real server asks for the lobby (match parameter lobby:true), which brings the two-player minimum.
var MIN_PLAYERS = 2;
var MISSION_GOAL_M = 1500;
var MISSION_BRIEFING_SECONDS = 10;
var MISSION_FLOOD_FAIL_SECONDS = 60;
var MISSION_CRUSH_DEPTH_M = 40;
var MISSION_FAST_SECONDS = 600;          // a run slower than this loses a star

function newMission(lobby) {
  return { phase: lobby ? "lobby" : "run", mode: !!lobby, minPlayers: lobby ? MIN_PLAYERS : 0, phaseTick: 0, ready: {}, goal: MISSION_GOAL_M, runTicks: 0, floodTicks: 0, flooded: false, maxFill: 0,
           scrams: 0, wasScram: false, result: "", stars: 0, why: "", stats: { t: 0, d: 0, f: 0, a: 0, mf: 0, sc: 0 }, games: 0 };
}

function missionRunning(state) {
  return state.mission.phase === "run" && state.order.length >= state.mission.minPlayers;
}

function missionSetReady(state, id, ready) {
  var ms = state.mission;
  if (ms.phase !== "lobby" && ms.phase !== "debrief") return false;
  if (ready) ms.ready[id] = true; else delete ms.ready[id];
  return true;
}

function missionAllReady(state) {
  if (state.order.length < state.mission.minPlayers) return false;
  for (var i = 0; i < state.order.length; i++) if (!state.mission.ready[state.order[i]]) return false;
  return true;
}

function missionFinish(state, result, why) {
  var ms = state.mission, fill = 0;
  for (var i = 0; i < state.water.comps.length; i++) fill = Math.max(fill, state.water.comps[i].w / WATER_COMPARTMENTS[i].volume);
  var rung = 0;
  for (var a = 0; a < state.alarms.list.length; a++) rung += state.alarms.list[a].count;
  ms.result = result; ms.why = why; ms.phase = "debrief"; ms.ready = {};
  ms.stats = { t: Math.round(ms.runTicks / TICK_RATE), d: Math.round(state.prop.dist), f: state.faults.started, a: rung, mf: Math.round(ms.maxFill * 100) / 100, sc: ms.scrams };
  var stars = 0;
  if (result === "success") {
    stars = 3;
    if (ms.maxFill >= 0.5) stars--;
    if (ms.runTicks > MISSION_FAST_SECONDS * TICK_RATE) stars--;
    if (ms.flooded) stars = Math.min(stars, 1);
    stars = Math.max(1, stars);
  }
  ms.stars = stars; ms.games += 1;
}

// A new world for a new game: the same players, a fresh boat (reset by match.js).
function missionBeginBriefing(state, tick) {
  var ms = state.mission;
  ms.phase = "briefing"; ms.phaseTick = tick; ms.ready = {};
  ms.runTicks = 0; ms.floodTicks = 0; ms.flooded = false; ms.maxFill = 0; ms.scrams = 0; ms.wasScram = false; ms.result = ""; ms.why = ""; ms.stars = 0;
}

function missionStep(state, tick) {
  var ms = state.mission;
  if (ms.phase === "lobby" || ms.phase === "debrief") {
    if (state.players[GHOST_ID]) ms.ready[GHOST_ID] = true;               // the debug ghost partner (E1-12) is always ready: one tester can start a game alone
    if (missionAllReady(state)) {
      if (ms.phase === "debrief") worldReset(state);
      missionBeginBriefing(state, tick);
    }
    return;
  }
  if (ms.phase === "briefing") {
    if (state.order.length < ms.minPlayers) { ms.phase = "lobby"; ms.ready = {}; return; }       // somebody left during the briefing
    if (tick - ms.phaseTick >= MISSION_BRIEFING_SECONDS * TICK_RATE) ms.phase = "run";
    return;
  }
  if (!missionRunning(state)) return;                                                            // run paused: fewer than two players
  ms.runTicks += 1;
  var fill = 0;
  for (var i = 0; i < state.water.comps.length; i++) fill = Math.max(fill, state.water.comps[i].w / WATER_COMPARTMENTS[i].volume);
  ms.maxFill = Math.max(ms.maxFill, fill);
  if (fill >= 0.9) { ms.floodTicks += 1; ms.flooded = true; } else ms.floodTicks = 0;
  if (state.reactor.scram && !ms.wasScram) ms.scrams += 1;
  ms.wasScram = !!state.reactor.scram;
  if (!ms.mode) return;                                           // a bare match (tests, tools) has no objective: it never ends
  if (state.prop.dist >= ms.goal) missionFinish(state, "success", "arrived");
  else if (ms.floodTicks >= MISSION_FLOOD_FAIL_SECONDS * TICK_RATE) missionFinish(state, "failure", "flooded");
  else if (state.boat.depth >= MISSION_CRUSH_DEPTH_M) missionFinish(state, "failure", "depth");
}

// Broadcast (and resync): ph = phase, g = goal (m), d = distance (m), t = seconds of run, r = ids that are ready, n = players, pz = 1 when paused,
// res / why / stars / st = the outcome once the run is over, bt = seconds left of the briefing.
function missionView(state, tick) {
  var ms = state.mission, ready = [];
  for (var i = 0; i < state.order.length; i++) if (ms.ready[state.order[i]]) ready.push(state.order[i]);
  var v = { ph: ms.phase, g: ms.goal, d: Math.round(state.prop.dist), t: Math.round(ms.runTicks / TICK_RATE), r: ready, n: state.order.length, mp: ms.minPlayers,
            pz: ms.phase === "run" && state.order.length < ms.minPlayers ? 1 : 0 };
  if (ms.phase === "briefing") v.bt = Math.max(0, Math.ceil(MISSION_BRIEFING_SECONDS - (tick - ms.phaseTick) / TICK_RATE));
  if (ms.phase === "debrief") { v.res = ms.result; v.why = ms.why; v.stars = ms.stars; v.st = ms.stats; }
  return v;
}
