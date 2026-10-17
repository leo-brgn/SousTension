// Acceptance tests for the fault generator with an attention budget (E3-09d). Run: node --test server/test/faults.test.js
const test = require("node:test");
const assert = require("node:assert");
const { createMatch, simulate, m } = require("./sim-harness.js");

function game(players, enabled, seed) {
  const s = createMatch(players);
  s.reactor = m.newReactor(seed || 1234, "croisiere");
  s.leaks.nextTick = 1e9;                                       // only the generator makes faults here
  s.faults.enabled = !!enabled;
  return s;
}
const mins = (n) => n * 60 * m.TICK_RATE;

test("the generator is off unless the match asks for it: tests and tools that script their own faults are untouched", () => {
  const s = game(["a", "b"], false);
  simulate(s, mins(10), {});
  assert.strictEqual(s.faults.started, 0);
  const created = m.handlers.matchInit({}, { info() {} }, { binaryToString: (d) => d }, {}).state;
  assert.strictEqual(created.faults.enabled, false);
  assert.strictEqual(m.handlers.matchInit({}, { info() {} }, { binaryToString: (d) => d }, { faults: true }).state.faults.enabled, true);
});

test("the budget starts at one fault, grows every 2 min 30 s and never exceeds the number of players plus one", () => {
  const two = game(["a", "b"], true), four = game(["a", "b", "c", "d"], true);
  const at = (s, sec) => m.faultBudget(s, sec * m.TICK_RATE);
  assert.deepStrictEqual([0, 100, 150, 299, 300, 450, 3000].map((t) => at(two, t)), [1, 1, 2, 2, 3, 3, 3]);
  assert.deepStrictEqual([0, 150, 300, 450, 3000].map((t) => at(four, t)), [1, 2, 3, 4, 5]);
});

test("nothing happens during the grace period, then faults come, never started above the budget, from alternating systems", () => {
  const s = game(["a", "b"], true);
  let firstStart = null, maxOver = -99; const systems = [];
  let started = 0;
  simulate(s, mins(15), {
    onTick: (st, t) => {
      if (st.faults.started > started) {                       // at the moment a fault starts, the crew was under its budget (the reactor's own valve drift may add more later)
        started = st.faults.started; if (firstStart === null) firstStart = t; systems.push(st.faults.last);
        maxOver = Math.max(maxOver, m.faultUnresolved(st) - m.faultBudget(st, t));
      }
    },
  });
  assert.ok(firstStart >= 60 * m.TICK_RATE, "first fault at tick " + firstStart);
  assert.ok(started >= 3, "faults started: " + started);
  assert.ok(maxOver <= 0, "a fault started with the crew already over budget by " + maxOver);
  for (let i = 1; i < systems.length; i++) assert.notStrictEqual(systems[i], systems[i - 1], "two faults in a row from " + systems[i] + ": " + systems.join(","));
});

test("each kind of fault is something the players can resolve and the alarm engine rings for it", () => {
  const alarmOf = { leak: /^wt_leak_/, breaker: /^pw_breaker_tripped$/, valve: /^rx_valve_low$/ };
  for (const fault of m.FAULTS) {
    const s = game(["a", "b"], true);
    assert.ok(fault.can(s), fault.id + " can start on a healthy boat");
    assert.strictEqual(fault.count(s), 0);
    const f = s.faults;
    fault.start(s, f);
    assert.ok(fault.count(s) >= 1, fault.id + " counts as unresolved");
    m.alarmsStep(s, 1);
    assert.ok(m.alarmsView(s.alarms).some((x) => alarmOf[fault.id].test(x)), fault.id + " rings an alarm: " + m.alarmsView(s.alarms));
  }
  // resolving them: patch the leak (leak removed), re-arm the breaker, open the valve
  const s = game(["a", "b"], true);
  m.FAULTS.find((x) => x.id === "leak").start(s, s.faults); s.leaks.list.length = 0;
  assert.strictEqual(m.FAULTS.find((x) => x.id === "leak").count(s), 0);
  m.FAULTS.find((x) => x.id === "breaker").start(s, s.faults);
  const i = s.power.br.findIndex((b) => b.tripped); s.power.br[i].tripped = false; s.power.br[i].closed = true;
  assert.strictEqual(m.FAULTS.find((x) => x.id === "breaker").count(s), 0);
  m.FAULTS.find((x) => x.id === "valve").start(s, s.faults); s.reactor.valves = [1, 1, 1, 1];
  assert.strictEqual(m.FAULTS.find((x) => x.id === "valve").count(s), 0);
});

test("a crew that never reacts: faults pile up but the generator stops adding once the budget is reached", () => {
  const s = game(["a", "b"], true);
  simulate(s, mins(12), {});
  const started = s.faults.started;
  assert.ok(m.faultUnresolved(s) >= m.faultBudget(s, mins(12)), "the crew is saturated: unresolved " + m.faultUnresolved(s));
  simulate(s, mins(3), {});
  assert.ok(s.faults.started <= started + 1, "no new fault while saturated: " + started + " -> " + s.faults.started);
});

test("deterministic: the same seed gives the same faults at the same ticks, another seed gives others", () => {
  const trace = (seed) => { const s = game(["a", "b"], true, seed); s.faults.rng = seed; const out = []; let n = 0; simulate(s, mins(10), { onTick: (st, t) => { if (st.faults.started > n) { n = st.faults.started; out.push(t + ":" + st.faults.last); } } }); return out.join(" "); };
  assert.strictEqual(trace(5), trace(5));
  assert.notStrictEqual(trace(5), trace(6));
});

test("the debug console drives the generator", () => {
  const h = m.handlers, nk = { binaryToString: (d) => d }, logger = { info() {} };
  let { state } = h.matchInit({}, logger, nk, { debug: true });
  state = h.matchJoin({}, logger, nk, null, 0, state, [{ userId: "a", sessionId: "sa" }]).state;
  state.leaks.nextTick = 1e9;
  const send = (line, t) => { state = h.matchLoop({}, logger, nk, { broadcastMessage() {} }, t, state, [{ opCode: m.OP_INPUT, sender: { userId: "a", sessionId: "sa" }, data: JSON.stringify({ seq: t, mx: 0, mz: 0, dbg: line }) }]).state; };
  send("faults now", 1);
  assert.strictEqual(state.faults.enabled, true);
  for (let t = 2; t < 20; t++) send("", t);
  assert.ok(state.faults.started >= 1, "a fault started");
  send("faults off", 30);
  assert.strictEqual(state.faults.enabled, false);
});
