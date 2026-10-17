// Plays the mission with a scripted two-person crew through the real inputs (server/test/crew-bot.js): a playtest without humans.
//   node tools/playtest-bot.js                 10 seeds, both sailors working
//   node tools/playtest-bot.js --seeds 30 --idle b      a crew where one sailor does nothing
//   node tools/playtest-bot.js --reaction 45             sailors that take 45 s to notice and start on a problem
//   node tools/playtest-bot.js --minutes 20
const { newCrew, playMission } = require("../server/test/crew-bot.js");
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf("--" + name); return i >= 0 && i + 1 < args.length ? args[i + 1] : def; };
const REACTION = parseFloat(opt("reaction", "0"));
const SEEDS = parseInt(opt("seeds", "10"), 10), IDLE = opt("idle", ""), MINUTES = parseFloat(opt("minutes", "15"));
const rows = [];
for (let seed = 1; seed <= SEEDS; seed++) rows.push(Object.assign({ seed }, playMission(newCrew({ seed, idle: IDLE, reaction: REACTION }), MINUTES)));
console.log("seed  result    why      stars  run(s)  dist  faults  alarms  maxfill");
for (const r of rows) console.log([r.seed, r.phase === "debrief" ? r.result : "(timeout)", r.why || "-", r.stars, r.runSeconds, r.dist, r.faults, r.stats.a || "-", r.stats.mf === undefined ? "-" : r.stats.mf].map((x, i) => String(x).padEnd(i === 1 ? 10 : i === 2 ? 9 : 7)).join(" "));
const won = rows.filter((r) => r.result === "success");
console.log("\nsuccess " + won.length + "/" + rows.length + (won.length ? " | median run " + won.map((r) => r.runSeconds).sort((a, b) => a - b)[Math.floor(won.length / 2)] + " s | stars " + [3, 2, 1].map((s) => s + ":" + won.filter((r) => r.stars === s).length).join(" ") : ""));
