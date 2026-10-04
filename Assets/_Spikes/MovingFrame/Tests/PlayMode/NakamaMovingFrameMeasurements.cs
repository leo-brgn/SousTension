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
            public void Read(out float moveX, out float moveZ, out bool act)
            {
                act = false;
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
            public void Read(out float moveX, out float moveZ, out bool act)
            {
                moveX = 0f;
                float dz = _targetZ - _z();
                moveZ = Math.Abs(dz) > 0.3f ? Math.Sign(dz) : 0f;
                double t = _clock.Now - _start();
                act = t >= _pressAt && t < _pressAt + 0.25;
            }
        }

        private sealed class IdleInput : IInputSource
        {
            public void Read(out float moveX, out float moveZ, out bool act) { moveX = 0f; moveZ = 0f; act = false; }
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

        private static void Cleanup(List<Client> clients, CancellationTokenSource cts)
        {
            cts.Cancel();
            foreach (var c in clients) { c.Controller.Dispose(); c.Net.Dispose(); }
        }

        private static double Pct(List<double> sorted, double p)
            => sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];
    }
}
