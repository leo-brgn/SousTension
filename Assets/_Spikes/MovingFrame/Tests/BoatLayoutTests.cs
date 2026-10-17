using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SousTension.Sim;
using UnityEngine;

namespace SousTension.Spikes.MovingFrame.Tests
{
    /// <summary>
    /// The boat layout (E10-01): collisions of the players against bulkheads, hatches and baffles, and the guards that keep the client's copy of the
    /// geometry identical to the server (server/src/layout.js, controls.js, coupled.js, power.js, bilge.js) and to the art (boat_layout.json).
    /// The reference positions below are what stepPlayer() of the server computes for the same scripted walks (server/test/layout.test.js).
    /// </summary>
    public class BoatLayoutTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Application.dataPath);
                return dir.Parent.FullName;
            }
        }

        private static string Server(string file) => File.ReadAllText(Path.Combine(RepoRoot, "server", "src", file));

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        private static float[] Numbers(string list)
        {
            var result = new List<float>();
            foreach (Match m in Regex.Matches(list, @"-?\d+(\.\d+)?")) result.Add(F(m.Value));
            return result.ToArray();
        }

        private static float[] ServerArray(string file, string name)
        {
            var m = Regex.Match(Server(file), @"var " + name + @" = \[([^\]]*)\]");
            Assert.IsTrue(m.Success, name + " not found in server/src/" + file);
            return Numbers(m.Groups[1].Value);
        }

        private static (float x, float z) Walk(float x, float z, float mx, float mz, int ticks, bool[] doorsOpen = null)
        {
            for (int i = 0; i < ticks; i++) CharacterMotion.Step(ref x, ref z, mx, mz, 1f, doorsOpen);
            return (x, z);
        }

        // ---- collisions: the same walks as the server, the same results ------------------------------------------------------------------------

        [Test]
        public void ABulkheadStopsAPlayerOutsideItsHatch()
        {
            var (x, z) = Walk(0.5f, 4.8f, 0f, -1f, 10);
            Assert.AreEqual(0.5f, x, 1e-4f); Assert.AreEqual(4.5f, z, 1e-4f);
        }

        [Test]
        public void TheHatchLetsAPlayerThrough()
        {
            var (x, z) = Walk(1.45f, 4.8f, 0f, -1f, 6);
            Assert.AreEqual(1.45f, x, 1e-4f); Assert.AreEqual(3.6f, z, 1e-4f);
        }

        [Test]
        public void TheWallSlidesAPlayerAlongItInsteadOfSticking()
        {
            var (x, z) = Walk(0.5f, 4.8f, 1f, -1f, 3);
            Assert.AreEqual(1.1364f, x, 1e-3f); Assert.AreEqual(4.3757f, z, 1e-3f);
        }

        [Test]
        public void AClosedHatchIsAWallAndTheBaffleBlocksTheLineBehindTheHatch()
        {
            var closed1 = new[] { true, false, true, true, true };
            var (x, z) = Walk(1.45f, 4.8f, 0f, -1f, 8, closed1);
            Assert.AreEqual(1.45f, x, 1e-4f); Assert.AreEqual(4.5f, z, 1e-4f);
            (x, z) = Walk(1.45f, 3.4f, 0f, -1f, 6);
            Assert.AreEqual(1.45f, x, 1e-4f); Assert.AreEqual(3.4f, z, 1e-4f, "the baffle 0.95 m behind the hatch");
        }

        [Test]
        public void TheEndsOfTheBoatAreClampedByTheRadiusAndAPlayerInsideAWallWalksOut()
        {
            var (x, z) = Walk(0f, 2f, 1f, 0f, 500);
            Assert.AreEqual(CharacterMotion.HalfX - BoatLayout.PlayerRadius, x, 1e-4f);
            (x, z) = Walk(1.45f, 12f, 0f, 1f, 500);
            Assert.AreEqual(CharacterMotion.HalfZ - BoatLayout.PlayerRadius, z, 1e-4f);
            (x, z) = Walk(-2.5f, 0f, 0f, 1f, 5);
            Assert.AreEqual(1.5f, z, 1e-4f, "started inside the wall of the bulkhead z = 0");
        }

        [Test]
        public void PredictionUsesTheHatchStatesOfTheLatestSnapshot()
        {
            var p = new PredictionBuffer { DoorsOpen = new[] { true, false, true, true, true } };
            p.Reset(1.45f, 4.8f);
            for (int i = 0; i < 8; i++) p.Predict(0f, -1f);
            Assert.AreEqual(4.5f, p.Z, 1e-4f, "predicted against a closed hatch");
            p.Reconcile(1.45f, 4.5f, 0);
            p.DoorsOpen = null;                                   // the hatch opened: the replayed inputs go through
            for (int i = 0; i < 4; i++) p.Predict(0f, -1f);
            Assert.Less(p.Z, 4f);
        }

        // ---- guards: the client's copy of the geometry equals the server's and the art's ----------------------------------------------------------

        [Test]
        public void TheBulkheadsAndHatchesEqualTheServers()
        {
            CollectionAssert.AreEqual(ServerArray("layout.js", "BULKHEAD_Z"), BoatLayout.BulkheadZ);
            CollectionAssert.AreEqual(ServerArray("layout.js", "HATCH_X"), BoatLayout.HatchX);
            var src = Server("layout.js");
            Assert.AreEqual(BoatLayout.PlayerRadius, F(Regex.Match(src, @"var PLAYER_RADIUS = ([\d.]+);").Groups[1].Value), 1e-6f);
            Assert.AreEqual(BoatLayout.HatchHalf, F(Regex.Match(src, @"var HATCH_HALF = ([\d.]+);").Groups[1].Value), 1e-6f);
            Assert.AreEqual(BoatLayout.WallHalf, F(Regex.Match(src, @"var WALL_HALF = ([\d.]+);").Groups[1].Value), 1e-6f);
            var baffle = Regex.Match(src, @"var BAFFLE_HALF_X = ([\d.]+), BAFFLE_HALF_Z = ([\d.]+), BAFFLE_OFFSET = ([\d.]+);");
            Assert.AreEqual(BoatLayout.BaffleHalfX, F(baffle.Groups[1].Value), 1e-6f);
            Assert.AreEqual(BoatLayout.BaffleHalfZ, F(baffle.Groups[2].Value), 1e-6f);
            Assert.AreEqual(BoatLayout.BaffleOffset, F(baffle.Groups[3].Value), 1e-6f);
            var core = Server("core.js");
            Assert.AreEqual(CharacterMotion.HalfX, F(Regex.Match(core, @"var HALF_X = ([\d.]+);").Groups[1].Value), 1e-6f);
            Assert.AreEqual(CharacterMotion.HalfZ, F(Regex.Match(core, @"var HALF_Z = ([\d.]+);").Groups[1].Value), 1e-6f);
        }

        [Test]
        public void TheCompartmentsEqualTheServersAndTheArtsRooms()
        {
            var water = Server("water.js");
            var rooms = Regex.Matches(water, @"z0: (-?[\d.]+), z1: (-?[\d.]+), volume");
            Assert.AreEqual(BoatLayout.CompartmentZ0.Length, rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                Assert.AreEqual(BoatLayout.CompartmentZ0[i], F(rooms[i].Groups[1].Value), 1e-6f, "z0 of compartment " + i);
                Assert.AreEqual(BoatLayout.CompartmentZ1[i], F(rooms[i].Groups[2].Value), 1e-6f, "z1 of compartment " + i);
            }
            var layout = File.ReadAllText(Path.Combine(RepoRoot, "Assets", "_Project", "Art", "Models", "Environment", "boat_layout.json"));
            var art = Regex.Matches(layout, @"""start"":\s*(-?\d+),\s*""end"":\s*(-?\d+)");
            Assert.AreEqual(BoatLayout.CompartmentZ0.Length, art.Count);
            for (int i = 0; i < art.Count; i++)
            {
                Assert.AreEqual(BoatLayout.CompartmentZ0[i], Math.Min(F(art[i].Groups[1].Value), F(art[i].Groups[2].Value)), 1e-6f, "art room " + i);
                Assert.AreEqual(BoatLayout.CompartmentZ1[i], Math.Max(F(art[i].Groups[1].Value), F(art[i].Groups[2].Value)), 1e-6f, "art room " + i);
            }
            Assert.AreEqual(2f * CharacterMotion.HalfZ, F(Regex.Match(layout, @"""length"":\s*(\d+)").Groups[1].Value), 1e-6f);
        }

        private static (float x, float z) ServerControl(string file, string id)
        {
            var m = Regex.Match(Server(file), @"id: """ + Regex.Escape(id) + @""",(?: comp: \d+,)? x: (-?[\d.]+), z: (-?[\d.]+)");
            Assert.IsTrue(m.Success, id + " not found in server/src/" + file);
            return (F(m.Groups[1].Value), F(m.Groups[2].Value));
        }

        [Test]
        public void TheControlPositionsEqualTheServers()
        {
            void Same(string file, string id, float x, float z)
            {
                var (sx, sz) = ServerControl(file, id);
                Assert.AreEqual(sx, x, 1e-6f, id + ".x"); Assert.AreEqual(sz, z, 1e-6f, id + ".z");
            }
            Same("controls.js", "regime", ControlLayout.RegimeX, ControlLayout.RegimeZ);
            Same("controls.js", "scram", ControlLayout.ScramX, ControlLayout.ScramZ);
            for (int i = 0; i < 4; i++) Same("controls.js", "valve" + i, ControlLayout.ValveX, ControlLayout.ValveZ[i]);
            for (int i = 0; i < 2; i++) Same("controls.js", "pump" + i, ControlLayout.PumpX, ControlLayout.PumpZ[i]);
            for (int i = 0; i < 2; i++) { Same("controls.js", "bilge" + i, ControlLayout.BilgeX[i], ControlLayout.BilgeZ[i]); Same("bilge.js", "bilge" + i, ControlLayout.BilgeX[i], ControlLayout.BilgeZ[i]); }
            Same("controls.js", "tele_up", ControlLayout.TeleX, ControlLayout.TeleUpZ);
            Same("controls.js", "tele_down", ControlLayout.TeleX, ControlLayout.TeleDownZ);
        }

        [Test]
        public void TheCoupledCommandsAndTheBreakerPanelEqualTheServers()
        {
            var coupled = Server("coupled.js");
            foreach (var (id, ax, az, bx, bz) in ControlLayout.Coupled)
            {
                int at = coupled.IndexOf("id: \"" + id + "\"", StringComparison.Ordinal);
                Assert.GreaterOrEqual(at, 0, id);
                var a = Regex.Match(coupled.Substring(at), @"a: \{ x: ([^,]+), z: ([^,]+),");
                var b = Regex.Match(coupled.Substring(at), @"b: \{ x: ([^,]+), z: ([^,]+),");
                if (a.Groups[1].Value.Contains("STATIONS"))                       // the demo pair stands on the server's STATIONS (core.js)
                {
                    var stations = Regex.Matches(Server("core.js"), @"\{ x: (-?[\d.]+), z: (-?[\d.]+) \}");
                    Assert.AreEqual(ax, F(stations[0].Groups[1].Value), 1e-6f); Assert.AreEqual(az, F(stations[0].Groups[2].Value), 1e-6f);
                    Assert.AreEqual(bx, F(stations[1].Groups[1].Value), 1e-6f); Assert.AreEqual(bz, F(stations[1].Groups[2].Value), 1e-6f);
                    continue;
                }
                Assert.AreEqual(ax, F(a.Groups[1].Value), 1e-6f, id + ".a.x"); Assert.AreEqual(az, F(a.Groups[2].Value), 1e-6f, id + ".a.z");
                Assert.AreEqual(bx, F(b.Groups[1].Value), 1e-6f, id + ".b.x"); Assert.AreEqual(bz, F(b.Groups[2].Value), 1e-6f, id + ".b.z");
            }
            var power = Server("power.js");
            Assert.AreEqual(ControlLayout.BreakerX(0), F(Regex.Match(power, @"BREAKERS\[i\]\.x = (-?[\d.]+) \+ 0\.03").Groups[1].Value), 1e-6f);
            Assert.AreEqual(ControlLayout.BreakerZ(0), F(Regex.Match(power, @"BREAKERS\[i\]\.z = (-?[\d.]+) \+ 0\.2").Groups[1].Value), 1e-6f);
            Assert.AreEqual(ControlLayout.BreakerCount, Regex.Matches(power, @"\{ id: ""[a-z0-9]+"", demand:").Count);
        }

        // ---- the 3D boat stands where the simulation says ---------------------------------------------------------------------------------------

        [Test]
        public void TheArtPlacesTheHatchesAndTheEquipmentWhereTheServerExpectsThem()
        {
            SousTension.Spikes.MovingFrame.EditorTools.PlayLocal.EnsureArtCatalog();
            var boat = new GameObject("TestBoat");
            try
            {
                var env = BoatEnvironmentView.TryCreate(boat.transform, new MovingFrameModel());
                Assert.IsNotNull(env, "the catalog has the BoatEnvironment prefab");
                float Dist(Transform t, float x, float z) { var p = env.BoatLocal(t); return Mathf.Sqrt((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z)); }
                for (int d = 0; d < BoatLayout.BulkheadCount; d++)                 // the leaf is hinged 0.45 m from the middle of its opening
                {
                    var door = env.Nearest("HatchDoor", BoatLayout.HatchX[d], BoatLayout.BulkheadZ[d]);
                    Assert.IsNotNull(door, "hatch " + d);
                    Assert.Less(Dist(door, BoatLayout.HatchX[d], BoatLayout.BulkheadZ[d]), 0.6f, "hatch " + d + " is where the bulkhead has its opening");
                }
                Assert.Less(Dist(env.Nearest("ScramLever", ControlLayout.ScramX, ControlLayout.ScramZ), ControlLayout.ScramX, ControlLayout.ScramZ), 0.2f);
                Assert.Less(Dist(env.Nearest("RK1_Console", ControlLayout.RegimeX, ControlLayout.RegimeZ), ControlLayout.RegimeX, ControlLayout.RegimeZ), 0.2f);
                for (int i = 0; i < 4; i++) Assert.Less(Dist(env.Nearest("PrimaryValve", ControlLayout.ValveX, ControlLayout.ValveZ[i]), ControlLayout.ValveX, ControlLayout.ValveZ[i]), 0.1f, "valve " + i);
                for (int i = 0; i < 2; i++) Assert.Less(Dist(env.Nearest("PrimaryPump", ControlLayout.PumpX, ControlLayout.PumpZ[i]), ControlLayout.PumpX, ControlLayout.PumpZ[i]), 0.1f, "pump " + i);
                for (int i = 0; i < 2; i++) Assert.Less(Dist(env.Nearest("BilgePump", ControlLayout.BilgeX[i], ControlLayout.BilgeZ[i]), ControlLayout.BilgeX[i], ControlLayout.BilgeZ[i]), 0.1f, "bilge pump " + i);
                Assert.Less(Dist(env.Nearest("ElectricalPanel", ControlLayout.BreakerX(0), ControlLayout.BreakerZ(2)), ControlLayout.BreakerX(0), ControlLayout.BreakerZ(2)), 0.3f);
                Assert.Less(Dist(env.Nearest("EngineTelegraphFivePosition", ControlLayout.TeleX, 0.5f * (ControlLayout.TeleUpZ + ControlLayout.TeleDownZ)), ControlLayout.TeleX, 5.75f), 0.2f);
                Assert.AreEqual(6, env.RoomLights.Count, "one lamp per compartment");
                Assert.AreEqual(6, env.EmergencyLights.Count);
                Assert.AreEqual(20, env.All("MachineBreaker").FindAll(t => env.BoatLocal(t).x < -2f && env.BoatLocal(t).z < -5f && env.BoatLocal(t).z > -6.2f && env.BoatLocal(t).y > 0.4f).Count, "the 20 breakers of the panel");
            }
            finally { UnityEngine.Object.DestroyImmediate(boat); }
        }

        [Test]
        public void TheClientWalksLikeTheServer()
        {
            // motion_parity.txt: 3 x 3000 steps of a pseudo-random walk through the boat, computed by the server (tools/gen-motion-parity.js).
            var lines = File.ReadAllLines(Path.Combine(RepoRoot, "Assets", "_Spikes", "MovingFrame", "Tests", "motion_parity.txt"));
            float x = 0, z = 0; bool[] doors = null; int step = 0, run = -1; float worst = 0f; int worstStep = 0;
            foreach (var line in lines)
            {
                var t = line.Split(' ');
                if (t[0] == "run")
                {
                    run++; step = 0; x = F(t[1]); z = F(t[2]);
                    doors = new bool[t[3].Length];
                    for (int i = 0; i < doors.Length; i++) doors[i] = t[3][i] == '1';
                    continue;
                }
                CharacterMotion.Step(ref x, ref z, F(t[0]), F(t[1]), F(t[2]), doors);
                float dx = x - F(t[3]), dz = z - F(t[4]);
                float err = Mathf.Sqrt(dx * dx + dz * dz);
                if (err > worst) { worst = err; worstStep = step; }
                // the client follows the server step by step: its own drift stays tiny (it never has to be snapped back)
                Assert.Less(err, 0.02f, "run " + run + " step " + step + ": client (" + x + ", " + z + ") vs server (" + t[3] + ", " + t[4] + ")");
                x = F(t[3]); z = F(t[4]);                                                       // re-sync each step: tests one step at a time, as the reconciliation does
                step++;
            }
            Assert.Less(worst, 0.02f, "worst error " + worst + " at step " + worstStep);
        }
    }
}
