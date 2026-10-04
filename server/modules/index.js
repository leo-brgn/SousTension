// Nakama authoritative match for the moving-frame spike (E1-01).
// Plain JavaScript (Nakama JS runtime, ES5 style) — no build step.
//
// Model: the boat moves deterministically as a function of time (see Assets/_Project/Scripts/Sim/BoatMotion.cs),
// so the server only simulates character positions in BOAT-LOCAL space (x = right, z = forward).
// Clients send one input per fixed input-tick (10 Hz); the server applies them in order and
// broadcasts the authoritative state each tick together with the last processed input sequence.

var MATCH_NAME = "moving_frame";
var TICK_RATE = 10;
var DT = 1 / TICK_RATE;
var OP_INPUT = 1;      // client -> server : {seq, mx, mz}
var OP_STATE = 2;      // server -> clients: {tick, t, players:[{id, x, z, seq}]}
var MOVE_SPEED = 3.0;  // m/s
var HALF_X = 3.0;      // boat interior half width (m)
var HALF_Z = 10.0;     // boat interior half length (m)
var MAX_QUEUED_INPUTS = 6;
var STATIONS = [{ x: 0, z: -9 }, { x: 0, z: 9 }]; // two-player interlock keys (boat-local), one at each end
var STATION_REACH = 2.0;       // m
var INTERLOCK_WINDOW_TICKS = 30; // 3 s at 10 Hz (GDD: Rule of Two Players)
var MAX_ALLOWANCE = 4; // max inputs a player may apply in one tick to catch up after a network stall
var MAX_PLAYERS = 4;

function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }

// One fixed-step integration, shared by server and (identically re-implemented) by the client prediction.
function stepPlayer(p, mx, mz) {
  var len = Math.sqrt(mx * mx + mz * mz);
  if (len > 1) { mx /= len; mz /= len; }
  p.x = clamp(p.x + mx * MOVE_SPEED * DT, -HALF_X, HALF_X);
  p.z = clamp(p.z + mz * MOVE_SPEED * DT, -HALF_Z, HALF_Z);
}

function newInterlock() {
  return { st: [{ by: "", tick: 0 }, { by: "", tick: 0 }], result: "none", resultTick: 0, count: 0 };
}

// A player at a station presses the key. Rejected when out of reach, when the station is already held, or when
// the same player already holds the other station (the rule needs two different players).
function tryActivate(il, id, pl, tick) {
  var best = -1, bestD = STATION_REACH;
  for (var i = 0; i < STATIONS.length; i++) {
    var dx = pl.x - STATIONS[i].x, dz = pl.z - STATIONS[i].z;
    var d = Math.sqrt(dx * dx + dz * dz);
    if (d <= bestD) { best = i; bestD = d; }
  }
  if (best < 0 || il.st[best].by !== "") return;
  if (il.st[1 - best].by === id) return;
  il.st[best].by = id; il.st[best].tick = tick;
}

// Success when both stations are held (by different players, guaranteed by tryActivate) and each was pressed
// within the window of the first one; otherwise the first press expires after the window.
function evaluateInterlock(il, tick) {
  var a = il.st[0], b = il.st[1];
  if (a.by !== "" && b.by !== "") {
    il.result = "success"; il.resultTick = tick; il.count++;
    a.by = ""; b.by = ""; return;
  }
  for (var i = 0; i < 2; i++) {
    var s = il.st[i];
    if (s.by !== "" && tick - s.tick >= INTERLOCK_WINDOW_TICKS) {
      il.result = "timeout"; il.resultTick = tick; s.by = "";
    }
  }
}

function interlockView(il, tick) {
  function rem(s) { return s.by === "" ? 0 : Math.max(0, INTERLOCK_WINDOW_TICKS - (tick - s.tick)); }
  return { a: rem(il.st[0]), b: rem(il.st[1]), ab: il.st[0].by, bb: il.st[1].by, result: il.result, rt: il.resultTick, n: il.count };
}

// ---- Carried & sliding cargo (boat-local space, no physics engine) -----------------------------------------
// Loose cargo slides on the tilted floor: the boat tilt is a deterministic function of server time (same formulas
// as BoatMotion.cs), so gravity can be projected into boat-local axes. Carried cargo follows its carrier(s).
// Light crates need one carrier; the heavy fuel flask needs two players who both grab within the 3 s window.
var GRAB_REACH = 1.5;       // m
var GRAVITY = 9.81;         // m/s^2
var FRICTION = 0.25;        // Coulomb coefficient: loose cargo starts sliding past ~14 deg of tilt
var CARGO_DEFS = [
  { id: "crate1", heavy: false, x: 2.0, z: -3.0 },
  { id: "crate2", heavy: false, x: -2.0, z: 4.0 },
  { id: "fuel", heavy: true, x: -2.0, z: -5.0 }
];
// Boat tilt (must match BoatMotion.cs defaults: pitch 15 deg / 7 s, roll 20 deg / 5 s + 1 rad phase).
var PITCH_AMP = 15 * Math.PI / 180, PITCH_PERIOD = 7;
var ROLL_AMP = 20 * Math.PI / 180, ROLL_PERIOD = 5;

// Horizontal part of the world "up" vector expressed in boat-local axes: up_local = (cos p sin r, cos p cos r, -sin p).
function boatUpHorizontal(t) {
  var tau = 2 * Math.PI;
  var p = PITCH_AMP * Math.sin(tau * t / PITCH_PERIOD);
  var r = ROLL_AMP * Math.sin(tau * t / ROLL_PERIOD + 1.0);
  return { x: Math.cos(p) * Math.sin(r), z: -Math.sin(p) };
}

function newCargo() {
  var out = [];
  for (var i = 0; i < CARGO_DEFS.length; i++) {
    var d = CARGO_DEFS[i];
    out.push({ id: d.id, heavy: d.heavy, x: d.x, z: d.z, vx: 0, vz: 0, carriers: [], pend: "", pendTick: 0 });
  }
  return out;
}

function heldBy(cargo, playerId) {
  for (var i = 0; i < cargo.length; i++) {
    var c = cargo[i];
    if (c.carriers.indexOf(playerId) >= 0 || c.pend === playerId) return c;
  }
  return null;
}

// F key: drop what you hold, otherwise grab the nearest free cargo within reach.
function tryGrab(cargo, playerId, pl, tick) {
  var held = heldBy(cargo, playerId);
  if (held) {
    if (held.pend === playerId) held.pend = "";
    var idx = held.carriers.indexOf(playerId);
    if (idx >= 0) { held.carriers = []; held.vx = 0; held.vz = 0; } // dropping breaks a shared carry: both release
    return;
  }
  var best = null, bestD = GRAB_REACH;
  for (var i = 0; i < cargo.length; i++) {
    var c = cargo[i];
    if (c.carriers.length >= (c.heavy ? 2 : 1)) continue;
    var dx = pl.x - c.x, dz = pl.z - c.z, d = Math.sqrt(dx * dx + dz * dz);
    if (d <= bestD) { best = c; bestD = d; }
  }
  if (!best) return;
  if (!best.heavy) { best.carriers = [playerId]; return; }
  if (best.pend === "") { best.pend = playerId; best.pendTick = tick; }
  else if (best.pend !== playerId) { best.carriers = [best.pend, playerId]; best.pend = ""; }
}

function slideStep(c, up) {
  var ax = -GRAVITY * up.x, az = -GRAVITY * up.z, amag = Math.sqrt(ax * ax + az * az);
  var vmag = Math.sqrt(c.vx * c.vx + c.vz * c.vz);
  var dirx, dirz;
  if (vmag < 1e-6) {
    if (amag <= FRICTION * GRAVITY) { c.vx = 0; c.vz = 0; return; } // static friction holds it
    dirx = ax / amag; dirz = az / amag;
  } else { dirx = c.vx / vmag; dirz = c.vz / vmag; }
  var nvx = c.vx + (ax - FRICTION * GRAVITY * dirx) * DT;
  var nvz = c.vz + (az - FRICTION * GRAVITY * dirz) * DT;
  if (vmag >= 1e-6 && (nvx * c.vx + nvz * c.vz) <= 0) { nvx = 0; nvz = 0; } // friction stopped it
  c.vx = nvx; c.vz = nvz;
  c.x += c.vx * DT; c.z += c.vz * DT;
  if (c.x < -HALF_X || c.x > HALF_X) { c.x = clamp(c.x, -HALF_X, HALF_X); c.vx = 0; }
  if (c.z < -HALF_Z || c.z > HALF_Z) { c.z = clamp(c.z, -HALF_Z, HALF_Z); c.vz = 0; }
}

function updateCargo(state, tick) {
  var up = boatUpHorizontal(tick * DT);
  for (var i = 0; i < state.cargo.length; i++) {
    var c = state.cargo[i];
    if (c.pend !== "" && tick - c.pendTick >= INTERLOCK_WINDOW_TICKS) c.pend = ""; // second carrier too late
    // A carrier that left the match releases the cargo
    var alive = [];
    for (var k = 0; k < c.carriers.length; k++) if (state.players[c.carriers[k]]) alive.push(c.carriers[k]);
    if (alive.length !== c.carriers.length) { c.carriers = []; c.vx = 0; c.vz = 0; }
    if (c.pend !== "" && !state.players[c.pend]) c.pend = "";
    if (c.carriers.length === 1) {
      var p1 = state.players[c.carriers[0]]; c.x = p1.x; c.z = p1.z; c.vx = 0; c.vz = 0;
    } else if (c.carriers.length === 2) {
      var pa = state.players[c.carriers[0]], pb = state.players[c.carriers[1]];
      c.x = (pa.x + pb.x) / 2; c.z = (pa.z + pb.z) / 2; c.vx = 0; c.vz = 0;
    } else {
      slideStep(c, up);
    }
  }
}

function cargoView(cargo) {
  var out = [];
  for (var i = 0; i < cargo.length; i++) {
    var c = cargo[i];
    out.push({ id: c.id, x: c.x, z: c.z, h: c.heavy ? 1 : 0, c: c.carriers, p: c.pend });
  }
  return out;
}

var matchInit = function (ctx, logger, nk, params) {
  logger.info("moving_frame match init");
  return {
    state: { tick: 0, players: {}, order: [], il: newInterlock(), cargo: newCargo() },
    tickRate: TICK_RATE,
    label: JSON.stringify({ name: MATCH_NAME })
  };
};

var matchJoinAttempt = function (ctx, logger, nk, dispatcher, tick, state, presence, metadata) {
  if (state.order.length >= MAX_PLAYERS && !state.players[presence.userId]) {
    return { state: state, accept: false, rejectMessage: "match full" };
  }
  return { state: state, accept: true };
};

var matchJoin = function (ctx, logger, nk, dispatcher, tick, state, presences) {
  for (var i = 0; i < presences.length; i++) {
    var id = presences[i].userId;
    if (!state.players[id]) {
      var slot = state.order.length;
      state.players[id] = { x: -1.5 + slot, z: 0, seq: 0, lastQueued: 0, allowance: 0, applied: 0, queue: [], presence: presences[i] };
      state.order.push(id);
    }
  }
  return { state: state };
};

var matchLeave = function (ctx, logger, nk, dispatcher, tick, state, presences) {
  for (var i = 0; i < presences.length; i++) {
    var id = presences[i].userId;
    delete state.players[id];
    for (var si = 0; si < 2; si++) if (state.il.st[si].by === id) state.il.st[si].by = "";
    var heldCargo = heldBy(state.cargo, id);
    if (heldCargo) { if (heldCargo.pend === id) heldCargo.pend = ""; heldCargo.carriers = []; heldCargo.vx = 0; heldCargo.vz = 0; }
    var idx = state.order.indexOf(id);
    if (idx >= 0) state.order.splice(idx, 1);
  }
  return { state: state };
};

var matchLoop = function (ctx, logger, nk, dispatcher, tick, state, messages) {
  // 1. Collect inputs (ordered by arrival). Drop the oldest if a client floods the queue.
  for (var i = 0; i < messages.length; i++) {
    var m = messages[i];
    if (m.opCode !== OP_INPUT) continue;
    var p = state.players[m.sender.userId];
    if (!p) continue;
    var input;
    try { input = JSON.parse(nk.binaryToString(m.data)); } catch (e) { continue; }
    // Reject stale or duplicate sequences, including ones already waiting in the queue.
    if (typeof input.seq !== "number" || input.seq <= p.lastQueued) continue;
    p.lastQueued = input.seq;
    p.queue.push({ seq: input.seq, mx: +input.mx || 0, mz: +input.mz || 0, act: input.act === true || input.act === 1,
                  grab: input.grab === true || input.grab === 1 });
    while (p.queue.length > MAX_QUEUED_INPUTS) p.queue.shift();
  }

  // 2. Apply queued inputs under a per-player budget: +1 per tick (the nominal input rate), capped at
  //    MAX_ALLOWANCE. In steady state this is exactly one input per tick; after a network stall (TCP
  //    retransmission) the backlog drains in a few ticks instead of lagging forever. The long-term rate can
  //    never exceed TICK_RATE inputs/s, so flooding the server with inputs does not speed a player up.
  for (var k = 0; k < state.order.length; k++) {
    var pl = state.players[state.order[k]];
    pl.allowance = Math.min(MAX_ALLOWANCE, pl.allowance + 1);
    while (pl.allowance >= 1 && pl.queue.length > 0) {
      var next = pl.queue.shift();
      stepPlayer(pl, next.mx, next.mz);
      if (next.act) tryActivate(state.il, state.order[k], pl, tick);
      if (next.grab) tryGrab(state.cargo, state.order[k], pl, tick);
      pl.seq = next.seq;
      pl.applied += 1;
      pl.allowance -= 1;
    }
  }

  evaluateInterlock(state.il, tick);
  updateCargo(state, tick);

  // 3. Broadcast authoritative state.
  var out = [];
  for (var j = 0; j < state.order.length; j++) {
    var id = state.order[j], q = state.players[id];
    out.push({ id: id, x: q.x, z: q.z, seq: q.seq });
  }
  state.tick = tick;
  dispatcher.broadcastMessage(OP_STATE, JSON.stringify({ tick: tick, t: tick * DT, players: out, il: interlockView(state.il, tick), cargo: cargoView(state.cargo) }), null, null, true);
  return { state: state };
};

var matchTerminate = function (ctx, logger, nk, dispatcher, tick, state, graceSeconds) {
  return { state: state };
};

var matchSignal = function (ctx, logger, nk, dispatcher, tick, state, data) {
  return { state: state, data: data };
};

// The single moving-frame match id is published in storage at startup (match listing is indexed
// asynchronously, so it cannot be relied on right after creation). RPCs run in different JS VMs,
// hence storage instead of a module-level variable.
var SYSTEM_USER = "00000000-0000-0000-0000-000000000000";
var STORAGE = { collection: "system", key: "moving_frame_match" };

function publishMatch(nk) {
  var matchId = nk.matchCreate(MATCH_NAME, {});
  nk.storageWrite([{ collection: STORAGE.collection, key: STORAGE.key, userId: SYSTEM_USER,
                     value: { matchId: matchId }, permissionRead: 2, permissionWrite: 0 }]);
  return matchId;
}

// RPC: returns the id of the moving-frame match (creating it if none is published).
var rpcGetMatch = function (ctx, logger, nk, payload) {
  var objs = nk.storageRead([{ collection: STORAGE.collection, key: STORAGE.key, userId: SYSTEM_USER }]);
  var matchId = (objs && objs.length > 0) ? objs[0].value.matchId : publishMatch(nk);
  return JSON.stringify({ matchId: matchId });
};

function InitModule(ctx, logger, nk, initializer) {
  initializer.registerMatch(MATCH_NAME, {
    matchInit: matchInit,
    matchJoinAttempt: matchJoinAttempt,
    matchJoin: matchJoin,
    matchLeave: matchLeave,
    matchLoop: matchLoop,
    matchTerminate: matchTerminate,
    matchSignal: matchSignal
  });
  initializer.registerRpc("get_moving_frame_match", rpcGetMatch);
  publishMatch(nk); // fresh match (and fresh id) at every server start
  logger.info("moving_frame module loaded (tick rate %d Hz)", TICK_RATE);
}

// Allow tests in Node to import the handlers (ignored by the Nakama runtime).
if (typeof module !== "undefined" && module.exports) {
  module.exports = {
    InitModule: InitModule, stepPlayer: stepPlayer,
    TICK_RATE: TICK_RATE, DT: DT, STATIONS: STATIONS, STATION_REACH: STATION_REACH, INTERLOCK_WINDOW_TICKS: INTERLOCK_WINDOW_TICKS, MAX_ALLOWANCE: MAX_ALLOWANCE, MOVE_SPEED: MOVE_SPEED, HALF_X: HALF_X, HALF_Z: HALF_Z,
    OP_INPUT: OP_INPUT, OP_STATE: OP_STATE,
    GRAB_REACH: GRAB_REACH, GRAVITY: GRAVITY, FRICTION: FRICTION, boatUpHorizontal: boatUpHorizontal,
    handlers: { matchInit: matchInit, matchJoinAttempt: matchJoinAttempt, matchJoin: matchJoin, matchLeave: matchLeave, matchLoop: matchLoop }
  };
}
