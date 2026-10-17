// Generates Assets/_Spikes/MovingFrame/Tests/motion_parity.txt: a long pseudo-random walk through the boat computed by the SERVER's stepPlayer.
// The Unity test BoatLayoutTests.TheClientWalksLikeTheServer replays the same inputs with CharacterMotion.Step and compares the positions.
// Regenerate after changing the layout or the movement rules:  node tools/gen-motion-parity.js
const fs = require("fs");
const path = require("path");
const m = require("../server/modules/index.js");

let seed = 12345;
const rnd = () => { seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0; return seed / 4294967296; };
const round4 = (v) => Math.round(v * 1e4) / 1e4;
const doorsSets = [
  [true, true, true, true, true],
  [true, false, true, true, true],
  [false, true, false, true, false],
];

const lines = [];
for (let run = 0; run < doorsSets.length; run++) {
  const doors = doorsSets[run].map((o) => ({ open: o }));
  const p = { x: -1.5 + run, z: 2 };
  lines.push("run " + p.x + " " + p.z + " " + doorsSets[run].map((o) => (o ? 1 : 0)).join(""));
  let mx = 0, mz = 0;
  for (let i = 0; i < 3000; i++) {
    if (i % 12 === 0) {                                             // a new heading every 1.2 s, sometimes standing still
      const a = rnd() * 2 * Math.PI, f = rnd() < 0.2 ? 0 : 1;
      mx = round4(Math.cos(a) * f); mz = round4(Math.sin(a) * f);
    }
    const factor = i % 500 < 100 ? 0.8 : 1;
    m.stepPlayer(p, mx, mz, factor, doors);
    lines.push([mx, mz, factor, p.x.toFixed(4), p.z.toFixed(4)].join(" "));
  }
}
fs.writeFileSync(path.join(__dirname, "..", "Assets", "_Spikes", "MovingFrame", "Tests", "motion_parity.txt"), lines.join("\n") + "\n");
console.log("wrote", lines.length, "lines");
