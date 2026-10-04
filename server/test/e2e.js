// End-to-end check against a running Nakama (docker compose up): 4 clients join the moving-frame match,
// send inputs at 10 Hz and check that the server acknowledges them and keeps everyone in bounds.
// Usage: node server/test/e2e.js   (Node >= 22: global fetch + WebSocket)
const HOST = "127.0.0.1:7350";
const SERVER_KEY = "defaultkey"; // dev-only default key
const OP_INPUT = 1, OP_STATE = 2;
const CLIENTS = 4, SECONDS = 5, INPUT_HZ = 10;

const b64 = (s) => Buffer.from(s).toString("base64");
const unb64 = (s) => Buffer.from(s, "base64").toString();

async function auth(deviceId) {
  const r = await fetch(`http://${HOST}/v2/account/authenticate/device?create=true`, {
    method: "POST",
    headers: { Authorization: "Basic " + b64(SERVER_KEY + ":"), "Content-Type": "application/json" },
    body: JSON.stringify({ id: deviceId })
  });
  if (!r.ok) throw new Error("auth failed " + r.status + " " + (await r.text()));
  return (await r.json()).token;
}

async function getMatchId(token) {
  const r = await fetch(`http://${HOST}/v2/rpc/get_moving_frame_match?http_key=`, {
    method: "POST",
    headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
    body: JSON.stringify("{}")
  });
  if (!r.ok) throw new Error("rpc failed " + r.status + " " + (await r.text()));
  return JSON.parse((await r.json()).payload).matchId;
}

function connect(token) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://${HOST}/ws?token=${token}&format=json`);
    ws.onopen = () => resolve(ws);
    ws.onerror = (e) => reject(new Error("ws error"));
  });
}

async function runClient(idx, matchId, token, stats) {
  const ws = await connect(token);
  const me = { acked: 0, states: 0, lastState: null, rtts: [], sent: new Map() };
  ws.onmessage = (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.match_data && Number(msg.match_data.op_code) === OP_STATE) {
      const st = JSON.parse(unb64(msg.match_data.data));
      me.states++; me.lastState = st;
      const mine = st.players.find((p) => p.id === me.userId);
      if (mine && mine.seq > me.acked) {
        for (let s = me.acked + 1; s <= mine.seq; s++) {
          const t0 = me.sent.get(s);
          if (t0) { me.rtts.push(performance.now() - t0); me.sent.delete(s); }
        }
        me.acked = mine.seq;
      }
    }
    if (msg.match) me.userId = msg.match.self.user_id;
  };
  ws.send(JSON.stringify({ cid: "1", match_join: { match_id: matchId } }));
  await new Promise((r) => setTimeout(r, 400));

  let seq = 0;
  const dirs = [[1, 0], [0, 1], [-1, 0], [0, -1]];
  const timer = setInterval(() => {
    seq++;
    const [mx, mz] = dirs[Math.floor(seq / 10 + idx) % 4];
    me.sent.set(seq, performance.now());
    ws.send(JSON.stringify({ match_data_send: { match_id: matchId, op_code: String(OP_INPUT), data: b64(JSON.stringify({ seq, mx, mz })) } }));
  }, 1000 / INPUT_HZ);
  await new Promise((r) => setTimeout(r, SECONDS * 1000));
  clearInterval(timer);
  await new Promise((r) => setTimeout(r, 400));
  ws.close();
  stats[idx] = { sent: seq, acked: me.acked, states: me.states, rtts: me.rtts, last: me.lastState };
}

(async () => {
  const tokens = await Promise.all(Array.from({ length: CLIENTS }, (_, i) => auth("e2e-device-" + i + "-0000000000")));
  const ids = [];
  for (const t of tokens) ids.push(await getMatchId(t)); // each client asks separately, like the Unity client
  if (new Set(ids).size !== 1) { console.log("E2E FAILED: clients got different matches", ids); process.exit(1); }
  const matchId = ids[0];
  console.log("match:", matchId);
  const stats = [];
  await Promise.all(tokens.map((t, i) => runClient(i, matchId, t, stats)));
  let ok = true;
  stats.forEach((s, i) => {
    const sorted = s.rtts.slice().sort((a, b) => a - b);
    const p50 = sorted[Math.floor(sorted.length * 0.5)] || NaN, p95 = sorted[Math.floor(sorted.length * 0.95)] || NaN;
    console.log(`client ${i}: sent=${s.sent} acked=${s.acked} states=${s.states} ack-latency p50=${p50.toFixed(1)}ms p95=${p95.toFixed(1)}ms`);
    if (s.acked < s.sent - 3) ok = false;
    if (s.states < SECONDS * 8) ok = false;
    for (const p of s.last.players) if (Math.abs(p.x) > 3.0001 || Math.abs(p.z) > 10.0001) ok = false;
  });
  console.log("players in last state:", stats[0].last.players.length);
  if (stats[0].last.players.length !== CLIENTS) ok = false;
  console.log(ok ? "E2E OK" : "E2E FAILED");
  process.exit(ok ? 0 : 1);
})().catch((e) => { console.error(e); process.exit(1); });
