using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using SousTension.Sim;
using UnityEngine;
using UnityEngine.TestTools;

namespace SousTension.Spikes.MovingFrame.Tests
{
    /// <summary>
    /// E1-01 measurements: 4 full clients (Nakama service + controller + model) against a RUNNING local Nakama
    /// (server/docker-compose.yml). Skipped when Nakama is unreachable. Results are written to Logs/e1-01-metrics.json.
    /// </summary>
    public class NakamaMovingFrameMeasurements
    {
        private sealed class RealClock : IClockService { public double Now => Time.realtimeSinceStartupAsDouble; }

        /// <summary>Walks a square in boat-local space: forward, right, back, left, 2 s per side (offset per client).</summary>
        private sealed class ScriptedInput : IInputSource
        {
            private readonly IClockService _clock; private readonly double _offset;
            public ScriptedInput(IClockService clock, int index) { _clock = clock; _offset = index * 0.5; }
            public void Read(out float moveX, out float moveZ, out bool act, out bool grab)
            {
                act = false; grab = false;
                int phase = (int)Math.Floor((_clock.Now + _offset) / 2.0) % 4;
                moveX = phase == 1 ? 1f : phase == 3 ? -1f : 0f;
                moveZ = phase == 0 ? 1f : phase == 2 ? -1f : 0f;
            }
        }

        private sealed class Client
        {
            public INetworkService Net; public MovingFrameController Controller; public MovingFrameModel Model;
            public readonly List<double> AckLatencies = new List<double>();
            public double LastAck; public float MaxCorrection; public double MaxSnapshotGap;
            public double LastSnapshotAt = -1; public long Snapshots;
        }

        private struct Scenario { public string Name; public double RttMs, JitterMs, LossPct; }

        private static readonly Scenario[] Scenarios =
        {
            new Scenario { Name = "baseline-localhost", RttMs = 0,   JitterMs = 0,  LossPct = 0 },
            new Scenario { Name = "rtt100-loss2",       RttMs = 100, JitterMs = 10, LossPct = 2 },
            new Scenario { Name = "rtt200-loss2",       RttMs = 200, JitterMs = 20, LossPct = 2 },
        };

        private const int ClientCount = 4;
        private const double RunSeconds = 10.0;

        [UnityTest, Timeout(300000)]
        public IEnumerator Measure_AllScenarios()
        {
            Application.targetFrameRate = 60;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var results = new List<string>();
            foreach (var sc in Scenarios)
            {
                var clock = new RealClock();
                var clients = new List<Client>();
                var cts = new CancellationTokenSource();
                string run = Guid.NewGuid().ToString("N").Substring(0, 8);

                for (int i = 0; i < ClientCount; i++)
                {
                    INetworkService net = new NakamaNetworkService($"e1-01-{sc.Name}-{run}-{i}");
                    if (sc.RttMs > 0 || sc.LossPct > 0)
                        net = new SimulatedLatencyNetworkService(net, clock, sc.RttMs / 2000.0, sc.JitterMs / 1000.0, sc.LossPct / 100.0, seed: 100 + i);
                    var model = new MovingFrameModel();
                    var ctrl = new MovingFrameController(net, new ScriptedInput(clock, i), clock, model);
                    clients.Add(new Client { Net = net, Controller = ctrl, Model = model });
                }

                float maxCorr = 0f; int playersSeen = 0, ackCount = 0;
                try
                {
                // Connect all (sequentially: keeps the server's match join order stable)
                foreach (var c in clients)
                {
                    var t = c.Net.ConnectAsync(cts.Token);
                    double deadline = clock.Now + 10;
                    while (!t.IsCompleted && clock.Now < deadline) yield return null;
                    if (!t.IsCompleted || t.IsFaulted)
                    {
                        Assert.Ignore("Nakama unreachable (start it with: docker compose -f server/docker-compose.yml up -d): "
                            + (t.IsFaulted ? t.Exception?.GetBaseException().Message : "timeout"));
                    }
                }

                double start = clock.Now, end = start + RunSeconds;
                double last = start;
                while (clock.Now < end)
                {
                    float dt = (float)(clock.Now - last); last = clock.Now;
                    foreach (var c in clients)
                    {
                        long before = c.Model.LastServerTime > 0 ? 1 : 0;
                        double ackBefore = c.Model.LastAckLatencyMs;
                        c.Controller.Tick(dt);
                        if (c.Model.LastAckLatencyMs != ackBefore) c.AckLatencies.Add(c.Model.LastAckLatencyMs);
                        if (c.Model.HasServerTime && c.Model.LastReceiveLocalTime != c.LastSnapshotAt)
                        {
                            if (c.LastSnapshotAt > 0) c.MaxSnapshotGap = Math.Max(c.MaxSnapshotGap, c.Model.LastReceiveLocalTime - c.LastSnapshotAt);
                            c.LastSnapshotAt = c.Model.LastReceiveLocalTime; c.Snapshots++;
                        }
                        c.MaxCorrection = Math.Max(c.MaxCorrection, c.Model.MaxCorrection);
                    }
                    yield return null;
                }

                // Measure and report
                var all = clients.SelectMany(c => c.AckLatencies).OrderBy(x => x).ToList();
                double p50 = Pct(all, 0.5), p95 = Pct(all, 0.95), p99 = Pct(all, 0.99);
                maxCorr = clients.Max(c => c.MaxCorrection); ackCount = all.Count;
                double maxGap = clients.Max(c => c.MaxSnapshotGap);
                double upKBs = clients.Average(c => c.Net.BytesSent) / 1024.0 / RunSeconds;
                double downKBs = clients.Average(c => c.Net.BytesReceived) / 1024.0 / RunSeconds;
                playersSeen = clients.Min(c => c.Model.RemoteIds.Count() + 1);
                string F(double v, string f = "F1") => v.ToString(f, CultureInfo.InvariantCulture);
                var sb = new StringBuilder();
                sb.Append("{\"scenario\":\"" + sc.Name + "\",\"rttMs\":" + F(sc.RttMs, "F0") + ",\"lossPct\":" + F(sc.LossPct, "F0") + ",\"clients\":" + ClientCount + ",\"seconds\":" + F(RunSeconds, "F0") + ",")
                  .Append("\"ackMs\":{\"p50\":" + F(p50) + ",\"p95\":" + F(p95) + ",\"p99\":" + F(p99) + "},\"maxCorrectionCm\":" + F(maxCorr * 100f, "F2") + ",")
                  .Append("\"maxSnapshotGapMs\":" + F(maxGap * 1000, "F0") + ",\"upKBps\":" + F(upKBs, "F2") + ",\"downKBps\":" + F(downKBs, "F2") + ",\"playersSeenMin\":" + playersSeen + "}");
                results.Add(sb.ToString());
                Debug.Log("[E1-01] " + sb);
                }
                finally { Cleanup(clients, cts); } // always close sockets, even when an assertion fails

                Assert.AreEqual(ClientCount, playersSeen, $"{sc.Name}: every client must see all players");
                Assert.Greater(ackCount, 20, $"{sc.Name}: inputs must be acknowledged");
                Assert.Less(maxCorr, 0.05f, $"{sc.Name}: prediction correction must stay under 5 cm");
                yield return new WaitForSeconds(1.0f); // let the match drain between scenarios
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/e1-01-metrics.json", "[\n  " + string.Join(",\n  ", results) + "\n]\n");
        }

        /// <summary>Walks to a station (boat-local z) then presses the interact key during [pressAt, pressAt + 0.25 s] (seconds since start).</summary>
        private sealed class StationBot : IInputSource
        {
            private readonly IClockService _clock; private readonly Func<float> _z; private readonly float _targetZ;
            private readonly Func<double> _start; private readonly double _pressAt;
            public StationBot(IClockService clock, Func<float> z, float targetZ, Func<double> start, double pressAt)
            { _clock = clock; _z = z; _targetZ = targetZ; _start = start; _pressAt = pressAt; }
            public void Read(out float moveX, out float moveZ, out bool act, out bool grab)
            {
                grab = false;
                moveX = 0f;
                float dz = _targetZ - _z();
                moveZ = Math.Abs(dz) > 0.3f ? Math.Sign(dz) : 0f;
                double t = _clock.Now - _start();
                act = t >= _pressAt && t < _pressAt + 0.25;
            }
        }

        private sealed class IdleInput : IInputSource
        {
            public void Read(out float moveX, out float moveZ, out bool act, out bool grab) { moveX = 0f; moveZ = 0f; act = false; grab = false; }
        }

        private struct LockScenario { public string Name; public double RttMs, LossPct, PressA, PressB; public bool ExpectSuccess; }

        private static readonly LockScenario[] LockScenarios =
        {
            new LockScenario { Name = "interlock-rtt0-1s-apart",        RttMs = 0,   LossPct = 0, PressA = 6.0, PressB = 7.0, ExpectSuccess = true },
            new LockScenario { Name = "interlock-rtt200-loss2-2s-apart", RttMs = 200, LossPct = 2, PressA = 6.0, PressB = 8.0, ExpectSuccess = true },
            new LockScenario { Name = "interlock-rtt0-3.6s-apart",       RttMs = 0,   LossPct = 0, PressA = 6.0, PressB = 9.6, ExpectSuccess = false },
        };

        [UnityTest, Timeout(300000)]
        public IEnumerator Measure_TwoPlayerInterlock()
        {
            Application.targetFrameRate = 60;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var results = new List<string>();
            foreach (var sc in LockScenarios)
            {
                var clock = new RealClock();
                var cts = new CancellationTokenSource();
                var clients = new List<Client>();
                string run = Guid.NewGuid().ToString("N").Substring(0, 8);
                double start = double.MaxValue;
                var models = new MovingFrameModel[ClientCount];
                for (int i = 0; i < ClientCount; i++) models[i] = new MovingFrameModel();

                for (int i = 0; i < ClientCount; i++)
                {
                    int idx = i;
                    INetworkService net = new NakamaNetworkService($"e1-01-{sc.Name}-{run}-{i}");
                    if (sc.RttMs > 0 || sc.LossPct > 0)
                        net = new SimulatedLatencyNetworkService(net, clock, sc.RttMs / 2000.0, 0.01, sc.LossPct / 100.0, seed: 200 + i);
                    IInputSource input = i == 0 ? new StationBot(clock, () => models[idx].LocalZ, -9f, () => start, sc.PressA)
                                       : i == 1 ? new StationBot(clock, () => models[idx].LocalZ, 9f, () => start, sc.PressB)
                                       : (IInputSource)new IdleInput();
                    clients.Add(new Client { Net = net, Model = models[i], Controller = new MovingFrameController(net, input, clock, models[i]) });
                }

                double[] seenSuccessAt = new double[ClientCount], seenTimeoutAt = new double[ClientCount];
                string[] lastResult = new string[ClientCount];
                int[] baseCount = new int[ClientCount], baseTick = new int[ClientCount];
                try
                {
                    foreach (var c in clients)
                    {
                        var t = c.Net.ConnectAsync(cts.Token);
                        double deadline = clock.Now + 10;
                        while (!t.IsCompleted && clock.Now < deadline) yield return null;
                        if (!t.IsCompleted || t.IsFaulted)
                            Assert.Ignore("Nakama unreachable (docker compose -f server/docker-compose.yml up -d): " + (t.IsFaulted ? t.Exception?.GetBaseException().Message : "timeout"));
                    }
                    // The match persists across scenarios: take a baseline once every client has a first snapshot.
                    double warm = clock.Now + 3;
                    while (clock.Now < warm && !models.All(m => m.HasServerTime)) { foreach (var c in clients) c.Controller.Tick(0f); yield return null; }
                    for (int i = 0; i < ClientCount; i++) { baseCount[i] = models[i].Interlock.Count; baseTick[i] = models[i].Interlock.ResultTick; }
                    start = clock.Now;
                    double end = start + sc.PressB + 7.0, last = start;
                    while (clock.Now < end)
                    {
                        float dt = (float)(clock.Now - last); last = clock.Now;
                        for (int i = 0; i < ClientCount; i++)
                        {
                            clients[i].Controller.Tick(dt);
                            var il = clients[i].Model.Interlock;
                            if (il.Count > baseCount[i] && seenSuccessAt[i] == 0) seenSuccessAt[i] = clock.Now;
                            if (il.Result == "timeout" && il.ResultTick > baseTick[i] && seenTimeoutAt[i] == 0) seenTimeoutAt[i] = clock.Now;
                            lastResult[i] = il.Result;
                        }
                        yield return null;
                    }
                }
                finally { Cleanup(clients, cts); }

                bool allSawSuccess = seenSuccessAt.All(x => x > 0);
                bool anySuccess = seenSuccessAt.Any(x => x > 0);
                bool anyTimeout = seenTimeoutAt.Any(x => x > 0);
                double lat = allSawSuccess ? (seenSuccessAt.Max() - (start + sc.PressB)) * 1000.0 : double.NaN;
                results.Add("{\"scenario\":\"" + sc.Name + "\",\"rttMs\":" + sc.RttMs.ToString("F0") + ",\"lossPct\":" + sc.LossPct.ToString("F0")
                    + ",\"pressGapSec\":" + (sc.PressB - sc.PressA).ToString("F1") + ",\"expectSuccess\":" + sc.ExpectSuccess.ToString().ToLower()
                    + ",\"success\":" + anySuccess.ToString().ToLower() + ",\"timeoutSeen\":" + anyTimeout.ToString().ToLower()
                    + ",\"secondPressToResultMs\":" + (double.IsNaN(lat) ? "null" : lat.ToString("F0")) + "}");
                Debug.Log("[E1-01-LOCK] " + results[results.Count - 1]);

                if (sc.ExpectSuccess)
                {
                    Assert.IsTrue(allSawSuccess, sc.Name + ": every client must see the interlock succeed");
                    Assert.Less(lat, 1500, sc.Name + ": result should reach all clients within 1.5 s of the second press");
                }
                else
                {
                    Assert.IsFalse(anySuccess, sc.Name + ": a press outside the 3 s window must not succeed");
                    Assert.IsTrue(anyTimeout, sc.Name + ": the first press must time out");
                }
                yield return new WaitForSeconds(1.0f);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/e1-01-interlock.json", "[\n  " + string.Join(",\n  ", results) + "\n]\n");
        }

        private enum StepKind { GoTo, GrabCargo, WaitUntil, Press }
        private struct Step
        {
            public StepKind Kind; public float X, Z; public string Cargo; public double At;
            public static Step GoTo(float x, float z) => new Step { Kind = StepKind.GoTo, X = x, Z = z };
            public static Step GrabCargo(string id, double notBefore) => new Step { Kind = StepKind.GrabCargo, Cargo = id, At = notBefore };
            public static Step WaitUntil(double t) => new Step { Kind = StepKind.WaitUntil, At = t };
            public static Step Press() => new Step { Kind = StepKind.Press };
        }

        /// <summary>Follows a script of steps: walk to a point, chase and grab a piece of cargo, wait, press grab again (drop).</summary>
        private sealed class ScriptBot : IInputSource
        {
            private readonly IClockService _clock; private readonly MovingFrameModel _model; private readonly Func<double> _start;
            private readonly List<Step> _steps; private int _i; private double _stepStart = -1;
            public double LastGrabPressAt;

            public ScriptBot(IClockService clock, MovingFrameModel model, Func<double> start, params Step[] steps)
            { _clock = clock; _model = model; _start = start; _steps = new List<Step>(steps); }

            private void Advance() { _i++; _stepStart = -1; }

            public void Read(out float moveX, out float moveZ, out bool act, out bool grab)
            {
                moveX = moveZ = 0f; act = false; grab = false;
                if (_i >= _steps.Count) return;
                var s = _steps[_i];
                if (_stepStart < 0) _stepStart = _clock.Now;
                double t = _clock.Now - _start();
                switch (s.Kind)
                {
                    case StepKind.GoTo:
                        Walk(s.X, s.Z, 0.2f, ref moveX, ref moveZ, out bool arrived);
                        if (arrived) Advance();
                        break;
                    case StepKind.WaitUntil:
                        if (t >= s.At) Advance();
                        break;
                    case StepKind.Press:
                        grab = true; if (LastGrabPressAt == 0) LastGrabPressAt = _clock.Now;
                        if (_clock.Now - _stepStart > 0.25) Advance();
                        break;
                    case StepKind.GrabCargo:
                    {
                        bool found = false; float cx = 0, cz = 0;
                        foreach (var c in _model.Cargo) if (c.Id == s.Cargo) { found = true; cx = c.X; cz = c.Z; }
                        if (!found) break;
                        float dx = cx - _model.LocalX, dz = cz - _model.LocalZ;
                        bool near = Math.Sqrt(dx * dx + dz * dz) < 1.0;
                        if (!near || t < s.At) { Walk(cx, cz, 0.6f, ref moveX, ref moveZ, out _); break; }
                        grab = true; if (LastGrabPressAt == 0) LastGrabPressAt = _clock.Now;
                        if (_clock.Now - _stepStart > 0.25 && t >= s.At) Advance();
                        break;
                    }
                }
            }

            private void Walk(float tx, float tz, float stop, ref float mx, ref float mz, out bool arrived)
            {
                float dx = tx - _model.LocalX, dz = tz - _model.LocalZ, d = (float)Math.Sqrt(dx * dx + dz * dz);
                arrived = d < stop;
                if (!arrived) { mx = dx / d; mz = dz / d; }
            }
        }

        private struct CargoScenario { public string Name; public double RttMs, LossPct; }

        private static readonly CargoScenario[] CargoScenarios =
        {
            new CargoScenario { Name = "cargo-rtt0",         RttMs = 0,   LossPct = 0 },
            new CargoScenario { Name = "cargo-rtt200-loss2", RttMs = 200, LossPct = 2 },
        };

        [UnityTest, Timeout(300000)]
        public IEnumerator Measure_CarriedCargo()
        {
            Application.targetFrameRate = 60;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            var results = new List<string>();
            foreach (var sc in CargoScenarios)
            {
                var clock = new RealClock();
                var cts = new CancellationTokenSource();
                var clients = new List<Client>();
                string run = Guid.NewGuid().ToString("N").Substring(0, 8);
                double start = double.MaxValue;
                var models = new MovingFrameModel[ClientCount];
                for (int i = 0; i < ClientCount; i++) models[i] = new MovingFrameModel();

                // bot0 carries a light crate across the boat and drops it; bot1 + bot2 lift the heavy fuel flask together
                var bot0 = new ScriptBot(clock, models[0], () => start,
                    Step.GrabCargo("crate1", 5.0), Step.GoTo(-2f, 5f), Step.WaitUntil(10.0), Step.Press());
                var bot1 = new ScriptBot(clock, models[1], () => start,
                    Step.GrabCargo("fuel", 6.0), Step.WaitUntil(8.5), Step.GoTo(-0.5f, 0f), Step.WaitUntil(13.0), Step.Press());
                var bot2 = new ScriptBot(clock, models[2], () => start,
                    Step.GrabCargo("fuel", 6.8), Step.WaitUntil(8.5), Step.GoTo(0.5f, 0f));
                var inputs = new IInputSource[] { bot0, bot1, bot2, new IdleInput() };
                for (int i = 0; i < ClientCount; i++)
                {
                    INetworkService net = new NakamaNetworkService($"e1-01-{sc.Name}-{run}-{i}");
                    if (sc.RttMs > 0 || sc.LossPct > 0)
                        net = new SimulatedLatencyNetworkService(net, clock, sc.RttMs / 2000.0, 0.01, sc.LossPct / 100.0, seed: 300 + i);
                    clients.Add(new Client { Net = net, Model = models[i], Controller = new MovingFrameController(net, inputs[i], clock, models[i]) });
                }

                double crateCarriedAt = 0, heavyLiftedAt = 0; float maxFollowErr = 0f; double maxSlide = 0; int followSamples = 0;
                float lastCrateX = float.NaN, lastCrateZ = float.NaN; bool dropped = false;
                try
                {
                    foreach (var c in clients)
                    {
                        var t = c.Net.ConnectAsync(cts.Token);
                        double deadline = clock.Now + 10;
                        while (!t.IsCompleted && clock.Now < deadline) yield return null;
                        if (!t.IsCompleted || t.IsFaulted)
                            Assert.Ignore("Nakama unreachable (docker compose -f server/docker-compose.yml up -d): " + (t.IsFaulted ? t.Exception?.GetBaseException().Message : "timeout"));
                    }
                    double warm = clock.Now + 3;
                    while (clock.Now < warm && !models.All(m => m.HasServerTime)) { foreach (var c in clients) c.Controller.Tick(0f); yield return null; }
                    start = clock.Now;
                    string id0 = clients[0].Net.LocalUserId, id1 = clients[1].Net.LocalUserId, id2 = clients[2].Net.LocalUserId;
                    var observer = models[3];
                    double end = start + 19.0, last = start;
                    while (clock.Now < end)
                    {
                        float dt = (float)(clock.Now - last); last = clock.Now;
                        foreach (var c in clients) c.Controller.Tick(dt);

                        double renderTime = observer.EstimateServerTime(clock.Now) - 0.1;
                        foreach (var c in observer.Cargo)
                        {
                            if (c.Id == "crate1" && c.Carriers.Length == 1 && c.Carriers[0] == id0)
                            {
                                if (crateCarriedAt == 0) crateCarriedAt = clock.Now;
                                // Skip the first 0.5 s: at pickup the cargo snaps to its carrier, and the render time (100 ms in the past) still shows the loose position.
                                if (clock.Now - crateCarriedAt > 0.5 && observer.TrySampleRemote(id0, renderTime, out float px, out float pz) && observer.TryGetCargoRenderPosition("crate1", renderTime, out float cx, out float cz))
                                { maxFollowErr = Math.Max(maxFollowErr, (float)Math.Sqrt((cx - px) * (cx - px) + (cz - pz) * (cz - pz))); followSamples++; }
                            }
                            if (c.Id == "fuel" && c.Carriers.Length == 2)
                            {
                                if (heavyLiftedAt == 0) heavyLiftedAt = clock.Now;
                                if (clock.Now - heavyLiftedAt > 0.5 && observer.TrySampleRemote(id1, renderTime, out float ax, out float az) && observer.TrySampleRemote(id2, renderTime, out float bx, out float bz)
                                    && observer.TryGetCargoRenderPosition("fuel", renderTime, out float fx, out float fz))
                                {
                                    float mx = (ax + bx) / 2, mz = (az + bz) / 2;
                                    maxFollowErr = Math.Max(maxFollowErr, (float)Math.Sqrt((fx - mx) * (fx - mx) + (fz - mz) * (fz - mz))); followSamples++;
                                }
                            }
                            if (c.Id == "crate1" && crateCarriedAt > 0 && c.Carriers.Length == 0)
                            {
                                if (!dropped) { dropped = true; lastCrateX = c.X; lastCrateZ = c.Z; }
                                maxSlide = Math.Max(maxSlide, Math.Sqrt((c.X - lastCrateX) * (c.X - lastCrateX) + (c.Z - lastCrateZ) * (c.Z - lastCrateZ)));
                            }
                        }
                        yield return null;
                    }
                }
                finally { Cleanup(clients, cts); }

                double grabLatency = crateCarriedAt > 0 && bot0.LastGrabPressAt > 0 ? (crateCarriedAt - bot0.LastGrabPressAt) * 1000.0 : double.NaN;
                results.Add("{\"scenario\":\"" + sc.Name + "\",\"rttMs\":" + sc.RttMs.ToString("F0") + ",\"lossPct\":" + sc.LossPct.ToString("F0")
                    + ",\"crateCarriedSeen\":" + (crateCarriedAt > 0).ToString().ToLower() + ",\"heavyLiftedSeen\":" + (heavyLiftedAt > 0).ToString().ToLower()
                    + ",\"grabToObserverMs\":" + (double.IsNaN(grabLatency) ? "null" : grabLatency.ToString("F0"))
                    + ",\"maxCargoFollowErrCm\":" + (maxFollowErr * 100f).ToString("F2") + ",\"followSamples\":" + followSamples
                    + ",\"maxSlideAfterDropCm\":" + (maxSlide * 100.0).ToString("F0") + "}");
                Debug.Log("[E1-01-CARGO] " + results[results.Count - 1]);

                Assert.IsTrue(crateCarriedAt > 0, sc.Name + ": the observer must see the crate carried by bot 0");
                Assert.IsTrue(heavyLiftedAt > 0, sc.Name + ": two players grabbing within 3 s must lift the heavy flask");
                Assert.Less(maxFollowErr, 0.05f, sc.Name + ": carried cargo must stay within 5 cm of its carrier(s) as drawn by an observer");
                Assert.Less(grabLatency, 1500, sc.Name + ": grab must be visible to an observer within 1.5 s");
                yield return new WaitForSeconds(1.0f);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/e1-01-cargo.json", "[\n  " + string.Join(",\n  ", results) + "\n]\n");
        }

        private static void Cleanup(List<Client> clients, CancellationTokenSource cts)
        {
            cts.Cancel();
            foreach (var c in clients) { c.Controller.Dispose(); c.Net.Dispose(); }
        }

        private static double Pct(List<double> sorted, double p)
            => sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
    }
}
