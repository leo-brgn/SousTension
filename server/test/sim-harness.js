// Headless driver of the fixed-step simulation (E1-06): no Nakama, no network, no clock. Reused by the determinism tests and, later, by the
// balance tool (E3-10). A match is created with the real handlers, then advanced tick by tick with scripted inputs through the same
// queueInput + simStep the live match loop uses.
const m = require("../modules/index.js");

const h = m.handlers;
const nk = { binaryToString: (d) => d };
const logger = { info() {} };

function createMatch(playerIds) {
  let { state } = h.matchInit({}, logger, nk, {});
  playerIds.forEach((id) => { state = h.matchJoin({}, logger, nk, null, 0, state, [{ userId: id, sessionId: "s" + id }]).state; });
  return state;
}

// script = { inputs(tick, state) -> [{ id, input }], events: { [tick]: (state) => void }, onTick(state, tick) }
// Advances from state.tick + 1 to state.tick + ticks.
function simulate(state, ticks, script) {
  const first = state.tick + 1, last = state.tick + ticks;
  for (let tick = first; tick <= last; tick++) {
    if (script.events && script.events[tick]) script.events[tick](state);
    if (script.inputs) for (const { id, input } of script.inputs(tick, state)) if (state.players[id]) m.queueInput(state.players[id], input);
    m.simStep(state, tick);
    if (script.onTick) script.onTick(state, tick);
  }
  return state;
}

module.exports = { createMatch, simulate, m };
