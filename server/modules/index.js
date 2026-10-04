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
var MAX_PLAYERS = 4;

function clamp(v, lo, hi) { return v < lo ? lo : (v > hi ? hi : v); }

// One fixed-step integration, shared by server and (identically re-implemented) by the client prediction.
function stepPlayer(p, mx, mz) {
  var len = Math.sqrt(mx * mx + mz * mz);
  if (len > 1) { mx /= len; mz /= len; }
  p.x = clamp(p.x + mx * MOVE_SPEED * DT, -HALF_X, HALF_X);
  p.z = clamp(p.z + mz * MOVE_SPEED * DT, -HALF_Z, HALF_Z);
}

var matchInit = function (ctx, logger, nk, params) {
  logger.info("moving_frame match init");
  return {
    state: { tick: 0, players: {}, order: [] },
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
      state.players[id] = { x: -1.5 + slot, z: 0, seq: 0, lastQueued: 0, queue: [], presence: presences[i] };
      state.order.push(id);
    }
  }
  return { state: state };
};

var matchLeave = function (ctx, logger, nk, dispatcher, tick, state, presences) {
  for (var i = 0; i < presences.length; i++) {
    var id = presences[i].userId;
    delete state.players[id];
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
    p.queue.push({ seq: input.seq, mx: +input.mx || 0, mz: +input.mz || 0 });
    while (p.queue.length > MAX_QUEUED_INPUTS) p.queue.shift();
  }

  // 2. Apply exactly one queued input per player per tick (same cadence as the client's input tick).
  for (var k = 0; k < state.order.length; k++) {
    var pl = state.players[state.order[k]];
    var next = pl.queue.shift();
    if (next) { stepPlayer(pl, next.mx, next.mz); pl.seq = next.seq; }
  }

  // 3. Broadcast authoritative state.
  var out = [];
  for (var j = 0; j < state.order.length; j++) {
    var id = state.order[j], q = state.players[id];
    out.push({ id: id, x: q.x, z: q.z, seq: q.seq });
  }
  state.tick = tick;
  dispatcher.broadcastMessage(OP_STATE, JSON.stringify({ tick: tick, t: tick * DT, players: out }), null, null, true);
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
    TICK_RATE: TICK_RATE, DT: DT, MOVE_SPEED: MOVE_SPEED, HALF_X: HALF_X, HALF_Z: HALF_Z,
    OP_INPUT: OP_INPUT, OP_STATE: OP_STATE,
    handlers: { matchInit: matchInit, matchJoinAttempt: matchJoinAttempt, matchJoin: matchJoin, matchLeave: matchLeave, matchLoop: matchLoop }
  };
}
