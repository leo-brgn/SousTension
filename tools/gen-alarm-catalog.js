// Writes Assets/_Spikes/MovingFrame/Tests/alarm_catalog.txt: the server's alarm catalogue (id, system, severity), one line per alarm, in order.
// The Unity test BoatLayoutTests.TheAlarmCatalogEqualsTheServers compares the client's AlarmCatalog with it; server/test/alarms.test.js checks that
// the file is up to date. Regenerate after changing server/src/alarms.js:  node tools/gen-alarm-catalog.js
const fs = require("fs");
const path = require("path");
const m = require("../server/modules/index.js");
const file = path.join(__dirname, "..", "Assets", "_Spikes", "MovingFrame", "Tests", "alarm_catalog.txt");
const text = m.ALARMS.map((a) => [a.id, a.system, a.severity].join(" ")).join("\n") + "\n";
if (process.argv.includes("--check")) { process.exit(fs.existsSync(file) && fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n") === text ? 0 : 1); }
fs.writeFileSync(file, text);
console.log("wrote " + m.ALARMS.length + " alarms");
