// Everything that belongs to the boat and its systems (a new game rebuilds it, the players stay): see worldReset.
function newWorld(faultsEnabled) {
  return { cp: newCoupled(), cargo: newCargo(), reactor: newReactor(REACTOR_SEED, "veille"), lever: newLever(), boat: newBoat(), restart: newRestart(), water: newWater(),
           leaks: newLeaks(), bilge: newBilge(), power: newPower(), prop: newPropulsion(), manual: newManual(), alarms: newAlarms(), faults: newFaults(faultsEnabled) };
}

// A new game (mission.js): a fresh boat for the same crew, back at the spawn point with empty hands.
function worldReset(state) {
  var world = newWorld(state.faults.enabled);
  for (var k in world) if (Object.prototype.hasOwnProperty.call(world, k)) state[k] = world[k];
  for (var i = 0; i < state.order.length; i++) {
    var p = state.players[state.order[i]];
    if (!p) continue;
    p.x = -1.5 + i; p.z = 2; p.hands = newHands(); p.queue = []; p.yaw = 0;
  }
}

var matchInit = function (ctx, logger, nk, params) {
  logger.info("moving_frame match init");
  var state = { tick: 0, players: {}, order: [], mission: newMission(!!(params && params.lobby === true)), debug: newDebug(debugEnabled(ctx, params)) };
  var world = newWorld(!!(params && params.faults === true));
  for (var key in world) if (Object.prototype.hasOwnProperty.call(world, key)) state[key] = world[key];
  return {
    state: state,
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
      state.players[id] = { x: -1.5 + slot, z: 2, seq: 0, lastQueued: 0, allowance: 0, applied: 0, queue: [], hands: newHands(), yaw: 0, presence: presences[i] };
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
    releasePlayerItems(state, id);
    var idx = state.order.indexOf(id);
    if (idx >= 0) state.order.splice(idx, 1);
  }
  // Nobody real is left (only the debug ghost, or nobody): the next crew must find a fresh match, not the wreck of the last game
  // (a run in progress, a debrief, the ghost partner still in a seat).
  var humans = 0;
  for (var h = 0; h < state.order.length; h++) if (state.order[h] !== GHOST_ID) humans++;
  if (humans === 0) {
    if (state.players[GHOST_ID]) ghostRemove(state);
    state.mission = newMission(state.mission.mode);
    worldReset(state);
  }
  return { state: state };
};

var matchLoop = function (ctx, logger, nk, dispatcher, tick, state, messages) {
  // 1. Collect inputs (ordered by arrival). Drop the oldest if a client floods the queue. Voice packets are relayed untouched (the game does not read them).
  var voiceCount = {};
  for (var i = 0; i < messages.length; i++) {
    var m = messages[i];
    if (m.opCode === OP_VOICE) {
      var from = m.sender.userId;
      if (state.players[from] && (voiceCount[from] || 0) < VOICE_MAX_PER_TICK) {
        voiceCount[from] = (voiceCount[from] || 0) + 1;
        dispatcher.broadcastMessage(OP_VOICE, m.data, null, m.sender, false);
      }
      continue;
    }
    if (m.opCode !== OP_INPUT) continue;
    var p = state.players[m.sender.userId];
    if (!p) continue;
    var input;
    try { input = JSON.parse(nk.binaryToString(m.data)); } catch (e) { continue; }
    queueInput(p, input);
  }

  // 2. Advance the whole simulation by one fixed step (server/src/sim.js: no Nakama, no clock, deterministic). `t` is the match tick plus the
  //    time skipped by the debug command ff (E1-09), so every timer of the game keeps one consistent clock.
  var t = tick + state.debug.skew;
  simStep(state, t);
  while (state.debug.pending > 0) { state.debug.pending--; state.debug.skew++; t++; simStep(state, t); }

  // 3. Broadcast authoritative state.
  var out = [];
  for (var j = 0; j < state.order.length; j++) {
    var id = state.order[j], q = state.players[id];
    out.push({ id: id, x: q.x, z: q.z, seq: q.seq, hd: handsView(q.hands) });
  }
  dispatcher.broadcastMessage(OP_STATE, JSON.stringify({ tick: t, t: t * DT, players: out, il: coupledView(state.cp, tick)[0], cp: coupledView(state.cp, tick), cargo: cargoView(state.cargo), rx: reactorView(state.reactor), sc: leverView(state.lever, state.reactor), rs: restartView(state), bw: waterView(state.water), lk: leaksView(state.leaks), bp: bilgeView(state.bilge), pw: powerView(state), pr: propulsionView(state.prop), mn: manualView(state.manual), al: alarmsView(state.alarms), ms: missionView(state, t), boat: boatView(state.boat) }), null, null, true);
  // 4. Debug replies (E1-09): each sender gets the lines of their commands as one message, to them only.
  if (state.debug.out.length > 0) {
    var byPlayer = {}, order = [];
    for (var o = 0; o < state.debug.out.length; o++) {
      var rep = state.debug.out[o];
      if (!byPlayer[rep.to]) { byPlayer[rep.to] = []; order.push(rep.to); }
      byPlayer[rep.to].push(rep.text);
    }
    for (var d = 0; d < order.length; d++) {
      var target = state.players[order[d]];
      if (target) dispatcher.broadcastMessage(OP_DEBUG, JSON.stringify({ lines: byPlayer[order[d]] }), [target.presence], null, true);
    }
    state.debug.out = [];
  }
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
  var matchId = nk.matchCreate(MATCH_NAME, { faults: true, lobby: true });   // the real server runs the fault generator (E3-09d) and the lobby / briefing / debrief flow
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

// RPC (debug mode only): a fresh bare match, apart from the real one, for tests and measures (the voice spike) that must not disturb a game.
var rpcCreateTestMatch = function (ctx, logger, nk, payload) {
  if (!debugEnabled(ctx, null)) throw Error("debug mode only");
  return JSON.stringify({ matchId: nk.matchCreate(MATCH_NAME, {}) });
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
  initializer.registerRpc("create_test_match", rpcCreateTestMatch);
  publishMatch(nk); // fresh match (and fresh id) at every server start
  logger.info("moving_frame module loaded (tick rate %d Hz)", TICK_RATE);
}

// Allow tests in Node to import the handlers (ignored by the Nakama runtime).
if (typeof module !== "undefined" && module.exports) {
  module.exports = {
    InitModule: InitModule, stepPlayer: stepPlayer, simStep: simStep, queueInput: queueInput, stateDigest: stateDigest,
    TICK_RATE: TICK_RATE, DT: DT, STATIONS: STATIONS, STATION_REACH: STATION_REACH, INTERLOCK_WINDOW_TICKS: INTERLOCK_WINDOW_TICKS, MAX_ALLOWANCE: MAX_ALLOWANCE, MOVE_SPEED: MOVE_SPEED, HALF_X: HALF_X, HALF_Z: HALF_Z,
    OP_INPUT: OP_INPUT, OP_VOICE: OP_VOICE, VOICE_MAX_PER_TICK: VOICE_MAX_PER_TICK, OP_STATE: OP_STATE, MIN_PLAYERS: MIN_PLAYERS, MISSION_GOAL_M: MISSION_GOAL_M, MISSION_BRIEFING_SECONDS: MISSION_BRIEFING_SECONDS, MISSION_FLOOD_FAIL_SECONDS: MISSION_FLOOD_FAIL_SECONDS,
    MISSION_CRUSH_DEPTH_M: MISSION_CRUSH_DEPTH_M, MISSION_FAST_SECONDS: MISSION_FAST_SECONDS, newMission: newMission, missionStep: missionStep, missionView: missionView,
    missionSetReady: missionSetReady, missionFinish: missionFinish, missionRunning: missionRunning, worldReset: worldReset, newWorld: newWorld,
    FAULTS: FAULTS, newFaults: newFaults, faultsStep: faultsStep, faultBudget: faultBudget, faultUnresolved: faultUnresolved, ALARMS: ALARMS, ANNUNCIATOR: ANNUNCIATOR, newAlarms: newAlarms, alarmsStep: alarmsStep, alarmAck: alarmAck, alarmsView: alarmsView,
    PLAYER_RADIUS: PLAYER_RADIUS, BULKHEAD_Z: BULKHEAD_Z, HATCH_X: HATCH_X, HATCH_HALF: HATCH_HALF, WALL_HALF: WALL_HALF, BAFFLE_HALF_X: BAFFLE_HALF_X, BAFFLE_HALF_Z: BAFFLE_HALF_Z, BAFFLE_OFFSET: BAFFLE_OFFSET,
    LAYOUT_OBSTACLES: LAYOUT_OBSTACLES, cargoBlocked: cargoBlocked, CARGO_RADIUS: CARGO_RADIUS, layoutBlocked: layoutBlocked, hatchToggle: hatchToggle, hatchOccupied: hatchOccupied, CONTROLS: CONTROLS,
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
    carrySpeedFactor: carrySpeedFactor, carriedMass: carriedMass, throwItem: throwItem, THROW_SPEED: THROW_SPEED, THROW_UP: THROW_UP, THROW_HEIGHT: THROW_HEIGHT, THROW_BOUNCE: THROW_BOUNCE,
    MANUAL_PAGES: MANUAL_PAGES, MANUAL_MAX_STEPS: MANUAL_MAX_STEPS, manualFlip: manualFlip, manualGoto: manualGoto, manualPageIndex: manualPageIndex,
    ITEM_KINDS: ITEM_KINDS, newHands: newHands, takeItem: takeItem, dropItem: dropItem, stowItem: stowItem, canUseHands: canUseHands, handsView: handsView,
    GHOST_ID: GHOST_ID, GHOST_DELAY_TICKS: GHOST_DELAY_TICKS, OP_DEBUG: OP_DEBUG, DEBUG_PARAMS: DEBUG_PARAMS, DEBUG_FF_MAX_SECONDS: DEBUG_FF_MAX_SECONDS, debugEnabled: debugEnabled,
    AIM_REACH: AIM_REACH, interactableIds: interactableIds,
    CONTROLS: CONTROLS, REGIME_ORDER: REGIME_ORDER, tryAct: tryAct,
    reactorNoise: reactorNoise, REACTOR_K: REACTOR_K, newReactor: newReactor, reactorStep: reactorStep, reactorView: reactorView, reactorScram: reactorScram,
    reactorRestart: reactorRestart, reactorSetRegime: reactorSetRegime, reactorSetValve: reactorSetValve, reactorSetPump: reactorSetPump,
    GRAB_REACH: GRAB_REACH, GRAVITY: GRAVITY, FRICTION: FRICTION, boatUpHorizontal: boatUpHorizontal,
    handlers: { matchInit: matchInit, matchJoinAttempt: matchJoinAttempt, matchJoin: matchJoin, matchLeave: matchLeave, matchLoop: matchLoop }
  };
}
