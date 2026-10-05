var matchInit = function (ctx, logger, nk, params) {
  logger.info("moving_frame match init");
  return {
    state: { tick: 0, players: {}, order: [], il: newInterlock(), cargo: newCargo(), reactor: newReactor(REACTOR_SEED, "veille"), lever: newLever(), boat: newBoat() },
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
                  grab: input.grab === true || input.grab === 1, hold: input.hold === true || input.hold === 1 });
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
      if (next.act) tryAct(state, state.order[k], pl, tick);
      if (next.hold) tryHold(state, pl);
      if (next.grab) tryGrab(state.cargo, state.order[k], pl, tick);
      pl.seq = next.seq;
      pl.applied += 1;
      pl.allowance -= 1;
    }
  }

  evaluateInterlock(state.il, tick);
  updateCargo(state, tick);
  reactorStep(state.reactor);
  leverStep(state.lever);
  boatStep(state.boat, state.reactor);

  // 3. Broadcast authoritative state.
  var out = [];
  for (var j = 0; j < state.order.length; j++) {
    var id = state.order[j], q = state.players[id];
    out.push({ id: id, x: q.x, z: q.z, seq: q.seq });
  }
  state.tick = tick;
  dispatcher.broadcastMessage(OP_STATE, JSON.stringify({ tick: tick, t: tick * DT, players: out, il: interlockView(state.il, tick), cargo: cargoView(state.cargo), rx: reactorView(state.reactor), sc: leverView(state.lever, state.reactor), boat: boatView(state.boat) }), null, null, true);
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
    LEVER_COVER_TICKS: LEVER_COVER_TICKS, SINK_RATE_MAX: SINK_RATE_MAX, SINK_RAMP_SECONDS: SINK_RAMP_SECONDS, newBoat: newBoat, boatStep: boatStep,
    VALVE_TURN_RATE: VALVE_TURN_RATE, reactorBreakPump: reactorBreakPump, reactorRepairPump: reactorRepairPump,
    CONTROLS: CONTROLS, REGIME_ORDER: REGIME_ORDER, tryAct: tryAct,
    reactorNoise: reactorNoise, REACTOR_K: REACTOR_K, newReactor: newReactor, reactorStep: reactorStep, reactorView: reactorView, reactorScram: reactorScram,
    reactorRestart: reactorRestart, reactorSetRegime: reactorSetRegime, reactorSetValve: reactorSetValve, reactorSetPump: reactorSetPump,
    GRAB_REACH: GRAB_REACH, GRAVITY: GRAVITY, FRICTION: FRICTION, boatUpHorizontal: boatUpHorizontal,
    handlers: { matchInit: matchInit, matchJoinAttempt: matchJoinAttempt, matchJoin: matchJoin, matchLeave: matchLeave, matchLoop: matchLoop }
  };
}
