using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Walkpadead.Core;
using Walkpadead.Game;

namespace Walkpadead.Tests
{
    public sealed class PrototypeTests
    {
        private Scene scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            scene = SceneManager.CreateScene("Prototype checks");
            SceneManager.SetActiveScene(scene);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var prototype = SceneManager.GetSceneByName("Prototype");
            if (prototype.IsValid() && prototype.isLoaded)
                yield return SceneManager.UnloadSceneAsync(prototype);
            if (scene.IsValid() && scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator ReloadingPrototypeCreatesANewPlayableRun()
        {
            yield return SceneManager.LoadSceneAsync("Prototype", LoadSceneMode.Additive);
            var first = UnityEngine.Object.FindFirstObjectByType<DesktopGame>();
            Assert.That(first, Is.Not.Null);
            Assert.That(first.gameObject.scene.name, Is.EqualTo("Prototype"));
            first.UseInputSource(new ControlledSource());
            first.StartOrResume();
            Assert.That(first.Session.Phase, Is.EqualTo(RunPhase.Running));
            yield return SceneManager.UnloadSceneAsync("Prototype");
            Assert.That(first == null, Is.True);

            yield return SceneManager.LoadSceneAsync("Prototype", LoadSceneMode.Additive);
            var restarted = UnityEngine.Object.FindFirstObjectByType<DesktopGame>();
            Assert.That(restarted, Is.Not.Null);
            Assert.That(restarted.Session.Phase, Is.EqualTo(RunPhase.Ready));
            Assert.That(restarted.Enemies.Count, Is.EqualTo(1));
            Assert.That(restarted.Gun.Ammo, Is.EqualTo(Sidearm.MagazineSize));
            restarted.UseInputSource(new ControlledSource());
            restarted.StartOrResume();
            Assert.That(restarted.Session.Phase, Is.EqualTo(RunPhase.Running));
        }

        [Test]
        public void RotationDoesNotSteerForwardMovement()
        {
            var rig = new GameObject("Rig");
            rig.transform.rotation = Quaternion.Euler(15, 65, 0);
            rig.AddComponent<ForwardLocomotion>().Advance(0.833333);
            Assert.That(rig.transform.position.x, Is.EqualTo(0).Within(0.00001));
            Assert.That(rig.transform.position.z, Is.EqualTo(0.833333).Within(0.00001));
        }

        [UnityTest]
        public IEnumerator RealRaycastsDamageZombieAndRespectMagazine()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.65f, 0);
            var gun = new GameObject("Gun").AddComponent<Sidearm>(); gun.Build(camera.transform);
            var zombie = ZombieActor.Spawn(new Vector3(0, 0, 6), null);
            var kills = 0; gun.Killed += _ => kills++;
            yield return null;
            var ray = new Ray(camera.transform.position, zombie.Chest - camera.transform.position);
            Assert.That(gun.Fire(ray), Is.True);
            Assert.That(zombie.Dead, Is.False);
            Assert.That(gun.Fire(ray), Is.False, "Cooldown blocks a second shot in the same frame.");
            gun.Tick(0.2f);
            Assert.That(gun.Fire(ray), Is.True);
            Assert.That(zombie.Dead, Is.True);
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(gun.Ammo, Is.EqualTo(10));
            gun.Reload(); Assert.That(gun.Fire(ray), Is.False);
            gun.Tick(1.2f); Assert.That(gun.Ammo, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator InputLossFreezesEnemiesAndRequiresManualResume()
        {
            var game = new GameObject("Game").AddComponent<DesktopGame>();
            var input = new ControlledSource();
            game.UseInputSource(input);
            yield return null;
            game.StartOrResume();
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Running));
            input.Connected = false;
            yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
            var distance = game.Session.DistanceM;
            var elapsed = game.Session.ElapsedSeconds;
            var enemyPosition = game.Enemies[0].transform.position;
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(game.Session.DistanceM, Is.EqualTo(distance));
            Assert.That(game.Session.ElapsedSeconds, Is.EqualTo(elapsed));
            Assert.That(game.Enemies[0].transform.position, Is.EqualTo(enemyPosition));
            input.Connected = true;
            yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Paused));
            game.StartOrResume();
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Running));
        }

        [UnityTest]
        public IEnumerator SpeedBonusesRequireOptInAndFollowReportedSpeed()
        {
            var game = new GameObject("Game").AddComponent<DesktopGame>();
            var input = new ControlledSource(); game.UseInputSource(input);
            yield return null; game.StartOrResume(); yield return null;
            Assert.That(game.TryCollectSpeedBonus(1), Is.False);
            game.ToggleSpeedBonuses();
            Assert.That(game.TryCollectSpeedBonus(1), Is.True);
            Assert.That(game.TryCollectSpeedBonus(1), Is.False);
            Assert.That(input.Requests, Is.EqualTo(1));
            yield return null;
            Assert.That(game.Session.SpeedMps, Is.EqualTo(input.Speed).Within(0.000001));
            input.Speed = 3.1 / 3.6;
            input.Result.SetResult(new SpeedControlResult("confirmed", "Pad reports 3.1", 3.1));
            yield return null;
            Assert.That(game.Session.SpeedMps, Is.EqualTo(3.1 / 3.6).Within(0.000001));
            var pickup = game.NextPickup;
            Assert.That(pickup.Advance(9, 0), Is.True);
            Assert.That(pickup.Advance(9, 0), Is.False, "A pickup is consumed exactly once");
            input.Connected = false; yield return null;
            Assert.That(game.SpeedBonusesEnabled, Is.False);
            Assert.That(game.TryCollectSpeedBonus(-1), Is.False);
        }

        [UnityTest]
        public IEnumerator LockedControlDisablesBonusesWhileTelemetryContinues()
        {
            var game = new GameObject("Game").AddComponent<DesktopGame>();
            var input = new ControlledSource(); game.UseInputSource(input);
            yield return null; game.StartOrResume(); game.ToggleSpeedBonuses();
            Assert.That(game.TryCollectSpeedBonus(-1), Is.True);
            input.Result.SetResult(new SpeedControlResult("rejected", "Control locked · restart bridge", controlLocked: true));
            yield return null;
            Assert.That(game.Input.Available, Is.True);
            Assert.That(game.Session.Phase, Is.EqualTo(RunPhase.Running));
            Assert.That(game.SpeedBonusesEnabled, Is.False);
            StringAssert.Contains("locked", game.BonusStatus);
            Assert.That(game.TryCollectSpeedBonus(1), Is.False);
            Assert.That(input.Requests, Is.EqualTo(1));
        }

        private sealed class ControlledSource : ITreadmillSource, ITreadmillSpeedControl
        {
            public bool Connected = true;
            public double Speed = 0.833333;
            public int Requests;
            public TaskCompletionSource<SpeedControlResult> Result = new TaskCompletionSource<SpeedControlResult>();
            public bool CanAdjustSpeed => Connected;
            public string SpeedRangeLabel => "1.0–5.0 km/h";
            public Task<SpeedControlResult> AdjustSpeedAsync(InputReading input, int deltaTenths)
            { Requests++; return Result.Task; }
            private readonly TelemetryState state = new TelemetryState();
            private readonly string id = Guid.NewGuid().ToString();
            private ulong sequence;
            public InputReading Read()
            {
                if (!Connected) state.Disconnect("Test disconnect");
                else
                {
                    sequence++;
                    state.Accept(TelemetryPacket.Parse("{\"schema_version\":1,\"connection_id\":\"" + id +
                        "\",\"sequence\":" + sequence + ",\"connected\":true,\"stale\":false,\"input_source\":\"simulation\"," +
                        "\"speed_mps\":" + Speed.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        ",\"usable_speed_mps\":" + Speed.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        ",\"field_age_ms\":{\"speed_mps\":0},\"belt_state\":\"running\"}"),
                        Time.realtimeSinceStartupAsDouble);
                }
                return state.Read(Time.realtimeSinceStartupAsDouble);
            }
            public void Dispose() { }
        }
    }
}
