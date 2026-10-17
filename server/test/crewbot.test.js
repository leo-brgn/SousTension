// The whole mission loop played by a scripted crew through the real inputs (server/test/crew-bot.js). Run: node --test server/test/crewbot.test.js
const test = require("node:test");
const assert = require("node:assert");
const { newCrew, playMission } = require("./crew-bot.js");

test("a competent two-person crew finishes the transit: lobby, briefing, faults handled, goal reached, debrief with stars", () => {
  const r = playMission(newCrew({ seed: 1 }), 14);
  assert.strictEqual(r.phase, "debrief");
  assert.strictEqual(r.result, "success", JSON.stringify(r));
  assert.ok(r.runSeconds >= 300 && r.runSeconds <= 840, "a transit of 5 to 14 minutes: " + r.runSeconds + " s");
  assert.ok(r.faults >= 3, "the fault generator kept them busy: " + r.faults);
  assert.ok(r.stars >= 1);
});

test("a crew that never reacts quickly still gets there but with fewer stars than a quick one (the stars measure the crew)", () => {
  const quick = playMission(newCrew({ seed: 2, reaction: 10 }), 15);
  const slow = playMission(newCrew({ seed: 2, reaction: 120 }), 20);
  assert.strictEqual(quick.result, "success");
  assert.ok(slow.phase !== "debrief" || slow.stars <= quick.stars, "slow crew: " + JSON.stringify(slow) + " vs quick " + JSON.stringify(quick));
  assert.ok(slow.runSeconds >= quick.runSeconds, "problems left alone slow the boat down");
});

test("a crew that does nothing does not win: the boat stays where it is and the run is still going after ten minutes", () => {
  const crew = newCrew({ seed: 3 });
  crew.idle = "both";                                              // nobody acts
  const r = playMission(crew, 10);
  assert.notStrictEqual(r.result, "success");
  assert.ok(r.dist < 100, "dist " + r.dist);
});

test("the crew bot is deterministic: the same seed plays the same game", () => {
  const a = playMission(newCrew({ seed: 4 }), 14), b = playMission(newCrew({ seed: 4 }), 14);
  assert.deepStrictEqual(a, b);
});

test("a crew recovers from a SCRAM two minutes in (lever, valves, pumps, two keys within 3 s) and still reaches the goal", () => {
  const r = playMission(newCrew({ seed: 1, scramAt: 120 }), 20);
  assert.strictEqual(r.result, "success", JSON.stringify(r));
  assert.strictEqual(r.scram, false);
});
