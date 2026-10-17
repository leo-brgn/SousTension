// A scripted two-person crew that PLAYS the mission through the real inputs (walking through the hatches, aimed presses, held valve wheels, patches):
// a playtest without humans (balance, regressions of the whole loop). Used by tools/playtest-bot.js and server/test/crewbot.test.js.
// The bots read the state (they know what is broken); everything they DO goes through the same inputs a client sends.
const { route } = require("./route.js");
const m = require("../modules/index.js");

const h = m.handlers, nk = { binaryToString: (d) => d }, logger = { info() {} };
const pr = (id) => ({ userId: id, sessionId: "s" + id });

// options: { scramAt } = seconds into the run at which the reactor SCRAMs (the crew must restart it); { reaction } = seconds before a sailor takes up a job that appeared; { seed } for the reactor drift and the fault generator; { idle: "b" } makes one of the two sailors do nothing (a crew that is not at its best).
function newCrew(params) {
  const opts = params || {};
  let { state } = h.matchInit({}, logger, nk, { lobby: true, faults: true });
  if (opts.seed) { state.reactor = m.newReactor(opts.seed, "veille"); state.faults.rng = opts.seed | 0; state.leaks.rng = opts.seed | 0; }
  const crew = { scramAt: opts.scramAt ? Math.round(opts.scramAt * m.TICK_RATE) : 0, idle: opts.idle || "", reaction: Math.round((opts.reaction || 0) * m.TICK_RATE), seen: {}, state, tick: 0, seq: { a: 0, b: 0 }, bots: { a: { task: null }, b: { task: null } }, log: [], sent: [], claimed: {} };
  for (const id of ["a", "b"]) crew.state = h.matchJoin({}, logger, nk, null, 0, crew.state, [pr(id)]).state;
  crew.d = { broadcastMessage: (op, data) => { crew.sent.push(data); if (crew.sent.length > 3) crew.sent.shift(); } };
  return crew;
}

const walkInput = (state, id, target) => {
  const p = state.players[id];
  const r = route(m, p, target);
  let w = r[0];
  if (r.length > 1 && Math.hypot(w.x - p.x, w.z - p.z) < 0.12) w = r[1];
  const d = Math.hypot(w.x - p.x, w.z - p.z);
  return d < 0.03 ? { mx: 0, mz: 0 } : { mx: (w.x - p.x) / d, mz: (w.z - p.z) / d };
};
const heldCount = (p) => [p.hands.l, p.hands.r].filter((x) => x).length;
const near = (p, t, r) => Math.hypot(p.x - t.x, p.z - t.z) <= r;
const control = (id) => m.CONTROLS.find((c) => c.id === id);

// ---- what needs doing ------------------------------------------------------------------------------------------------------------------
function jobs(state) {
  const out = [];
  const r = state.reactor;
  if (state.mission.phase !== "run") return out;
  if (r.scram) return out;                                                      // the restart procedure takes both sailors (restartScript)
  if (r.regime === "veille") out.push({ key: "regime", kind: "regime" });
  if (state.prop.pos < 4 && !r.scram) out.push({ key: "telegraph", kind: "telegraph" });
  for (let i = 0; i < 4; i++) if (!r.scram && r.valves[i] < 0.6) out.push({ key: "valve" + i, kind: "valve", index: i });
  for (let i = 0; i < state.power.br.length; i++) if (state.power.br[i].tripped) out.push({ key: "breaker" + i, kind: "breaker", index: i });
  for (const leak of state.leaks.list) out.push({ key: "leak" + leak.id, kind: "leak", leak });
  return out;
}

// One tick of one bot: returns the input fields to send.
// The restart procedure after a SCRAM (restart.js): lever back, the four valves, both pumps, then the two keys within 3 s from two far posts.
// Sailor "a" does the first three steps then goes to key A; sailor "b" waits at key B and answers a second after a has armed its key.
function restartScript(crew, id) {
  const state = crew.state, p = state.players[id];
  if (heldCount(p) >= 2) return { drop: true };                                // controls need a free hand
  const action = m.COUPLED_ACTIONS.findIndex((x) => x.id === "reactor_restart");
  const keyA = m.COUPLED_ACTIONS[action].a, keyB = m.COUPLED_ACTIONS[action].b;
  if (id === "b") {
    const armedByA = state.cp.acts[action].st[0].by === "a";
    if (!near(p, keyB, 1.2)) return walkInput(state, id, { x: keyB.x, z: keyB.z });
    return state.tick % 5 === 0 ? { act: true, use: "cc:reactor_restart:1" } : {};
  }
  const r = state.reactor;
  if (!state.lever.reset) {
    const c = control("scram");
    const stand = { x: c.x + 0.3, z: c.z + 0.35 };                              // in the pocket behind the hatch, clear of the baffle
    if (!near(p, stand, 0.3)) return walkInput(state, id, stand);
    return state.tick % 3 === 0 ? { act: true, use: "scram" } : {};
  }
  for (let i = 0; i < 4; i++) if (r.valves[i] < m.RESTART_VALVE_MIN) {
    const c = control("valve" + i);
    if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x - 1.0, z: c.z });
    return { hold: true, use: "valve" + i };
  }
  for (let i = 0; i < 2; i++) if (!r.pumps[i]) {
    const c = control("pump" + i);
    if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x - 1.0, z: c.z });
    return state.tick % 3 === 0 ? { act: true, use: "pump" + i } : {};
  }
  if (!near(p, keyA, 1.2)) return walkInput(state, id, { x: keyA.x, z: keyA.z });
  return state.tick % 5 === 0 ? { act: true, use: "cc:reactor_restart:0" } : {};
}

function think(crew, id) {
  const state = crew.state, bot = crew.bots[id], p = state.players[id];
  if (state.reactor.scram) { if (bot.task) { delete crew.claimed[bot.task.key]; bot.task = null; } return restartScript(crew, id); }
  if (bot.task && !jobs(state).some((j) => j.key === bot.task.key)) { delete crew.claimed[bot.task.key]; delete crew.seen[bot.task.key]; bot.task = null; bot.step = 0; }
  for (const k of Object.keys(crew.seen)) if (!jobs(state).some((j) => j.key === k)) delete crew.seen[k];            // a job that went away is forgotten
  if (!bot.task) {
    for (const j of jobs(state)) {
      if (crew.seen[j.key] === undefined) crew.seen[j.key] = state.tick;                  // a human needs time to notice and to decide: the job is only taken after the reaction time
      if (!crew.claimed[j.key] && state.tick - crew.seen[j.key] >= crew.reaction) { crew.claimed[j.key] = id; bot.task = j; bot.step = 0; break; }
    }
  }
  const t = bot.task;
  if (!t) return walkTowardsRest(state, id);
  const hands = p.hands;
  if (t.kind !== "leak" && heldCount(p) >= 2) return { drop: true };            // a control needs a free hand
  const holds = (kind) => [hands.l, hands.r, hands.p].some((x) => x && state.cargo.find((c) => c.id === x && c.kind === kind));
  switch (t.kind) {
    case "regime": {
      const c = control("regime");
      if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x + 1.2, z: c.z });
      return { act: true, use: "regime" };
    }
    case "telegraph": {
      const c = control("tele_up");
      if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x + 1.2, z: c.z });
      return state.tick % 3 === 0 ? { act: true, use: "tele_up" } : {};
    }
    case "valve": {
      const c = control("valve" + t.index);
      if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x - 1.0, z: c.z });
      return { hold: true, use: "valve" + t.index };
    }
    case "breaker": {
      const c = control("breaker_" + m.BREAKERS[t.index].id);
      if (!near(p, c, 1.6)) return walkInput(state, id, { x: c.x + 1.0, z: c.z });
      return state.tick % 3 === 0 ? { act: true, use: "breaker_" + m.BREAKERS[t.index].id } : {};
    }
    case "leak": {
      const leak = state.leaks.list.find((l) => l.id === t.leak.id);
      if (!holds("patch")) {                                                   // fetch a patch from the toolbox
        const patch = state.cargo.find((c) => c.kind === "patch" && c.active && c.carriers.length === 0);
        if (!patch) return {};
        if (!near(p, patch, 0.25)) return walkInput(state, id, { x: patch.x, z: patch.z });          // stand ON it: the nearest item is the one taken
        return heldCount(p) >= 2 ? { drop: true } : { take: true };
      }
      const target = { x: leak.x - Math.sign(leak.x) * 1.0, z: leak.z };
      if (!near(p, target, 0.4)) return walkInput(state, id, target);
      return { act: true, use: "leak:" + leak.id };
    }
  }
  return {};
}

function walkTowardsRest(state, id) {
  const rest = id === "a" ? { x: 0, z: 7.5 } : { x: 0, z: -2.5 };
  const p = state.players[id];
  return Math.hypot(p.x - rest.x, p.z - rest.z) < 0.3 ? {} : walkInput(state, id, rest);
}

function step(crew) {
  const msgs = [];
  if (crew.scramAt && crew.state.mission.runTicks === crew.scramAt) m.reactorScram(crew.state.reactor);         // a SCRAM on purpose (a test of the recovery)
  for (const id of ["a", "b"]) {
    if (!crew.state.players[id]) continue;
    const act = crew.state.mission.phase === "run" && crew.idle !== id && crew.idle !== "both" ? think(crew, id) : {};
    crew.seq[id] += 1;
    msgs.push({ opCode: m.OP_INPUT, sender: pr(id), data: JSON.stringify(Object.assign({ seq: crew.seq[id], mx: 0, mz: 0 }, act)) });
  }
  crew.state = h.matchLoop({}, logger, nk, crew.d, ++crew.tick, crew.state, msgs).state;
}

// Play one mission: ready up, run until the debrief or `maxMinutes`. Returns what happened.
function playMission(crew, maxMinutes) {
  const ids = ["a", "b"];
  const send = (extra) => crew.state = h.matchLoop({}, logger, nk, crew.d, ++crew.tick, crew.state, ids.map((id) => ({ opCode: m.OP_INPUT, sender: pr(id), data: JSON.stringify(Object.assign({ seq: ++crew.seq[id], mx: 0, mz: 0 }, extra)) }))).state;
  send({ rdy: true });
  const limit = crew.tick + Math.round(maxMinutes * 60 * m.TICK_RATE) + m.MISSION_BRIEFING_SECONDS * m.TICK_RATE;
  while (crew.tick < limit && crew.state.mission.phase !== "debrief") step(crew);
  const ms = crew.state.mission;
  return { phase: ms.phase, result: ms.result, why: ms.why, stars: ms.stars, stats: ms.stats, runSeconds: Math.round(ms.runTicks / m.TICK_RATE), dist: Math.round(crew.state.prop.dist),
           faults: crew.state.faults.started, scram: crew.state.reactor.scram };
}

module.exports = { newCrew, playMission, step, jobs };
