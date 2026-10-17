// The balance tool (E3-10, tools/balance.js) runs the real simulation headless: it must work, be deterministic and fail when an invariant breaks.
// Run: node --test server/test/balance.test.js
const test = require("node:test");
const assert = require("node:assert");
const path = require("node:path");
const { spawnSync } = require("node:child_process");

const tool = path.join(__dirname, "..", "..", "tools", "balance.js");
const run = (...args) => spawnSync(process.execPath, [tool, ...args], { encoding: "utf8" });

test("the balance tool runs every scenario on several seeds, reports them and finds the invariants intact", () => {
  const r = run("--seeds", "2", "--minutes", "3");
  assert.strictEqual(r.status, 0, r.stdout + r.stderr);
  for (const name of ["idle-veille", "cruise", "full-power", "unattended-leak", "scram-restart", "scheduled-leaks"]) assert.ok(r.stdout.includes("== " + name), name);
  assert.match(r.stdout, /All invariants hold, every run is deterministic/);
});

test("the report is machine-readable and identical from one run to the next (same seeds, same numbers)", () => {
  const a = run("--seeds", "2", "--minutes", "2", "--json", "--scenario", "full-power");
  const b = run("--seeds", "2", "--minutes", "2", "--json", "--scenario", "full-power");
  assert.strictEqual(a.status, 0, a.stderr);
  const ra = JSON.parse(a.stdout);
  assert.strictEqual(ra.length, 2);
  assert.deepStrictEqual(ra, JSON.parse(b.stdout));
  assert.notStrictEqual(ra[0].digest, ra[1].digest, "different reactor seeds drift differently");
  assert.ok(ra.every((x) => x.violations.length === 0 && x.maxT > 300));
});

test("an unknown scenario is refused", () => {
  assert.strictEqual(run("--scenario", "nope").status, 2);
});
