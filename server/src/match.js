var matchInit = function (ctx, logger, nk, params) {
  logger.info("moving_frame match init");
  return {
    state: { tick: 0, players: {}, order: [], cp: newCoupled(), cargo: newCargo(), reactor: newReactor(REACTOR_SEED, "veille"), lever: newLever(), boat: newBoat(), restart: newRestart(), water: newWater(), leaks: newLeaks(), bilge: newBilge(), power: newPower(), prop: newPropulsion() },
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
    releaseCoupled(state.cp, id);
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
    queueInput(p, input);
  }

  // 2. Advance the whole simulation by one fixed step (server/src/sim.js: no Nakama, no clock, deterministic).
  simStep(state, tick);

  // 3. Broadcast authoritative state.
  var out = [];
  for (var j = 0; j < state.order.length; j++) {
    var id = state.order[j], q = state.players[id];
    out.push({ id: id, x: q.x, z: q.z, seq: q.seq });
  }
  dispatcher.broadcastMessage(OP_STATE, JSON.stringify({ tick: tick, t: tick * DT, players: out, il: coupledView(state.cp, tick)[0], cp: coupledView(state.cp, tick), cargo: cargoView(state.cargo), rx: reactorView(state.reactor), sc: leverView(state.lever, state.reactor), rs: restartView(state), bw: waterView(state.water), lk: leaksView(state.leaks), bp: bilgeView(state.bilge), pw: powerView(state), pr: propulsionView(state.prop), boat: boatView(state.boat) }), null, null, true);
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
    InitModule: InitModule, stepPlayer: stepPlayer, simStep: simStep, queueInput: queueInput, stateDigest: stateDigest,
    TICK_RATE: TICK_RATE, DT: DT, STATIONS: STATIONS, STATION_REACH: STATION_REACH, INTERLOCK_WINDOW_TICKS: INTERLOCK_WINDOW_TICKS, MAX_ALLOWANCE: MAX_ALLOWANCE, MOVE_SPEED: MOVE_SPEED, HALF_X: HALF_X, HALF_Z: HALF_Z,
    OP_INPUT: OP_INPUT, OP_STATE: OP_STATE,
    LEVER_COVER_TICKS: LEVER_COVER_TICKS, SINK_RATE_MAX: SINK_RATE_MAX, SINK_RAMP_SECONDS: SINK_RAMP_SECONDS, newBoat: newBoat, boatStep: boatStep,
    VALVE_TURN_RATE: VALVE_TURN_RATE, reactorBreakPump: reactorBreakPump, reactorRepairPump: reactorRepairPump,
    RESTART_VALVE_MIN: RESTART_VALVE_MIN,
    BILGE_PUMPS: BILGE_PUMPS, BILGE_CAPACITY: BILGE_CAPACITY, BILGE_MIN_V: BILGE_MIN_V,
    PROP_NAMES: PROP_NAMES, PROP_DEMAND: PROP_DEMAND, PROP_SPEED: PROP_SPEED, PROP_VMAX: PROP_VMAX, PROP_TAU: PROP_TAU, newPropulsion: newPropulsion, telegraphStep: telegraphStep,
    propulsionStep: propulsionStep, propulsionNoise: propulsionNoise, powerDemand: powerDemand,
    BREAKERS: BREAKERS, TAU_UP: TAU_UP, TAU_DOWN: TAU_DOWN, TRIP_V: TRIP_V, TRIP_TICKS: TRIP_TICKS, DARK_V: DARK_V, BATTERY_DRAIN_S: BATTERY_DRAIN_S, BATTERY_CHARGE_S: BATTERY_CHARGE_S,
    newPower: newPower, powerStep: powerStep, powerView: powerView, breakerToggle: breakerToggle, breakerTrip: breakerTrip, gridDemand: gridDemand, gridSurplus: gridSurplus, lightLevel: lightLevel, lightBand: lightBand, BUCKET_VOLUME: BUCKET_VOLUME, BUCKET_COOLDOWN_TICKS: BUCKET_COOLDOWN_TICKS,
    bilgeBreak: bilgeBreak, bilgeRepair: bilgeRepair, bilgeToggle: bilgeToggle,
    LEAK_RATES: LEAK_RATES, LEAK_REACH: LEAK_REACH, LEAK_FIRST_TICK: LEAK_FIRST_TICK, PATCH_RESPAWN_TICKS: PATCH_RESPAWN_TICKS, leakCreate: leakCreate, leakRate: leakRate,
    WATER_COMPARTMENTS: WATER_COMPARTMENTS, WATER_FLOW: WATER_FLOW, MAX_TRIM_DEG: MAX_TRIM_DEG, MAX_LIST_DEG: MAX_LIST_DEG, newWater: newWater, waterAdd: waterAdd,
    waterRemove: waterRemove, waterSetDoor: waterSetDoor, waterStep: waterStep, waterTotal: waterTotal, waterTilt: waterTilt, waterView: waterView, waterLevel: waterLevel, compartmentAt: compartmentAt,
    newCoupled: newCoupled, COUPLED_ACTIONS: COUPLED_ACTIONS, COUPLED_EFFECTS: COUPLED_EFFECTS, COUPLED_GRACE_TICKS: COUPLED_GRACE_TICKS,
    CONTROLS: CONTROLS, REGIME_ORDER: REGIME_ORDER, tryAct: tryAct,
    reactorNoise: reactorNoise, REACTOR_K: REACTOR_K, newReactor: newReactor, reactorStep: reactorStep, reactorView: reactorView, reactorScram: reactorScram,
    reactorRestart: reactorRestart, reactorSetRegime: reactorSetRegime, reactorSetValve: reactorSetValve, reactorSetPump: reactorSetPump,
    GRAB_REACH: GRAB_REACH, GRAVITY: GRAVITY, FRICTION: FRICTION, boatUpHorizontal: boatUpHorizontal,
    handlers: { matchInit: matchInit, matchJoinAttempt: matchJoinAttempt, matchJoin: matchJoin, matchLeave: matchLeave, matchLoop: matchLoop }
  };
}
