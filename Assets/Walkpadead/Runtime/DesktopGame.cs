using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Walkpadead.Core;

namespace Walkpadead.Game
{
    public sealed class DesktopGame : MonoBehaviour
    {
        public RunSession Session { get; private set; } = new RunSession();
        public Sidearm Gun { get; private set; }
        public Camera ViewCamera { get; private set; }
        public bool IsQuest { get; private set; }
        public QuestControls Quest { get; private set; }
        public Vector3 PlayerPosition => player.position;
        public bool ControlsReady => !IsQuest || Quest.TrackingReady;
        public IReadOnlyList<ZombieActor> Enemies => enemies;
        public InputReading Input { get; private set; }
        public bool SpeedBonusesEnabled { get; private set; }
        public string BonusStatus { get; private set; } = "Speed bonuses off · B to enable";
        public bool CanControlSpeed => source is ITreadmillSpeedControl control && control.CanAdjustSpeed;
        public string SpeedRangeLabel => (source as ITreadmillSpeedControl)?.SpeedRangeLabel ?? "Speed control unavailable";
        public SpeedPickup NextPickup { get; private set; }
        private Task<SpeedControlResult> speedRequest;
        private float nextSpeedRequestAt;
        private int pickupIndex;
        private Transform pickupRoot;
        private readonly List<ZombieActor> enemies = new List<ZombieActor>();
        private ITreadmillSource source;
        private Transform player;
        private ForwardLocomotion locomotion;
        private Transform enemyRoot;
        private RouteVisuals route;
        private QuestArena arena;
        private IAimSource aim;
        private PrototypeHud hud;
        private float spawnIn = 7;
        private float ignoreFireUntil;
        private int spawnCount;
        private bool shuttingDown;
        private string connectionPath;
        private bool offlineSimulation;
        private static bool restartWithSimulator;
        private float nextQuestDiagnosticAt;
        private RunPhase lastDiagnosticPhase;
        private int lastDiagnosticAmmo = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            restartWithSimulator = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Prototype" && scene.name != "QuestPrototype") return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<DesktopGame>() != null) return;
            // Run on every load, including Try again. Keep procedural objects in
            // their owning scene when a test or editor tool loads it additively.
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            new GameObject("Walkpadead").AddComponent<DesktopGame>();
            if (previous.IsValid() && previous.isLoaded && previous != scene)
                SceneManager.SetActiveScene(previous);
        }

        private void Awake()
        {
            IsQuest = gameObject.scene.name == "QuestPrototype";
            BonusStatus = "Speed bonuses off · " + (IsQuest ? "Y" : "B") + " to enable";
            Application.targetFrameRate = 90;
            Application.runInBackground = false;
            connectionPath = Environment.GetEnvironmentVariable("WALKPADEAD_CONNECTION_FILE");
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--connection-file") connectionPath = args[i + 1];
            if (string.IsNullOrWhiteSpace(connectionPath))
                connectionPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".runtime", "connection.json"));
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var directory = activity.Call<AndroidJavaObject>("getFilesDir"))
                connectionPath = Path.Combine(directory.Call<string>("getAbsolutePath"), "walkingpad-connection.json");
#endif

            if (IsQuest) { arena = new GameObject("Extraction yard").AddComponent<QuestArena>(); arena.Build(); }
            else { route = new GameObject("Service road").AddComponent<RouteVisuals>(); route.Build(); }
            player = new GameObject(IsQuest ? "Gaze-steered player" : "Forward-only player").transform;
            locomotion = player.gameObject.AddComponent<ForwardLocomotion>();
            ViewCamera = new GameObject(IsQuest ? "Quest headset" : "Desktop camera").AddComponent<Camera>();
            ViewCamera.tag = "MainCamera";
            ViewCamera.transform.SetParent(player, false);
            ViewCamera.transform.localPosition = new Vector3(0, 1.65f, 0);
            ViewCamera.fieldOfView = 72; ViewCamera.nearClipPlane = 0.04f; ViewCamera.farClipPlane = 170;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor; ViewCamera.backgroundColor = RenderSettings.fogColor;
            ViewCamera.gameObject.AddComponent<AudioListener>();
            if (IsQuest)
            {
                Quest = gameObject.AddComponent<QuestControls>(); Quest.Build(ViewCamera, player); aim = Quest;
                ViewCamera.farClipPlane = 96;
            }
            else aim = new DesktopMouseAim(ViewCamera);
            Gun = new GameObject("Sidearm").AddComponent<Sidearm>();
            Gun.Build(IsQuest ? Quest.RightAim : ViewCamera.transform, IsQuest);
            Gun.Killed += _ => Session.RecordKill();
            enemyRoot = new GameObject("Infected").transform;
            Spawn(12, -0.6f);
            pickupRoot = new GameObject("Speed bonuses").transform;
            NextPickup = SpeedPickup.Spawn(8, 1, pickupRoot);
            hud = new GameObject("HUD").AddComponent<PrototypeHud>(); hud.Build(this);
            if (IsQuest && restartWithSimulator) { restartWithSimulator = false; UseSimulator(); }
            else Reconnect();
        }

        private void Update()
        {
            if (source == null) return;
            if (IsQuest)
            {
                Quest.Tick();
                if (!ControlsReady) { Session.Pause(Quest.Status); SpeedBonusesEnabled = false; }
            }
            Input = source.Read();
            if (speedRequest != null && speedRequest.IsCompleted)
            {
                var result = speedRequest.Status == TaskStatus.RanToCompletion ? speedRequest.Result :
                    new SpeedControlResult("unconfirmed", "Speed request unconfirmed; bonuses disabled");
                BonusStatus = result.Status == "confirmed" ? $"Pad reports {result.TargetKmh:0.0} km/h" : result.Message;
                if (result.ControlLocked) SpeedBonusesEnabled = false;
                speedRequest = null;
            }
            if (!Input.Available) SpeedBonusesEnabled = false;
            var keyboard = IsQuest ? null : Keyboard.current;
            if (keyboard?.bKey.wasPressedThisFrame == true || Quest?.BonusesPressed == true) ToggleSpeedBonuses();
            if ((keyboard?.escapeKey.wasPressedThisFrame == true || Quest?.PausePressed == true) && Session.Phase == RunPhase.Running)
                PauseRun("Paused");
            else if (keyboard?.enterKey.wasPressedThisFrame == true || Quest?.StartPressed == true)
                StartOrResume();

            var displacement = Session.Tick(Input, Time.unscaledDeltaTime);
            locomotion.Advance(displacement, IsQuest ? ViewCamera.transform.forward : Vector3.forward);
            if (Session.Phase == RunPhase.Running)
            {
                var dt = Time.unscaledDeltaTime;
                if (!IsQuest) aim.Tick();
                Gun.Tick(dt);
                if (aim.ReloadPressed) Gun.Reload();
                if (Time.unscaledTime >= ignoreFireUntil && aim.FirePressed) Gun.Fire(aim.AimRay);
                TickEnemies(dt);
                if (IsQuest) arena.Follow(player.position); else route.Follow(player.position.z);
                if (IsQuest ? NextPickup.Advance(player.position, dt) : NextPickup.Advance(player.position.z, dt))
                {
                    TryCollectSpeedBonus(NextPickup.DeltaTenths);
                    Destroy(NextPickup.gameObject);
                    pickupIndex++;
                    var next = IsQuest ? player.position + locomotion.Heading * 18 : new Vector3(0, 0, 8 + pickupIndex * 18);
                    NextPickup = SpeedPickup.Spawn(next, pickupIndex % 2 == 0 ? 1 : -1, pickupRoot);
                }
            }
            var running = Session.Phase == RunPhase.Running;
            if (!IsQuest)
            { Cursor.lockState = running ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !running; }
            hud.Refresh(Session, Input, Gun);
            LogQuestRuntime();
        }

        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogQuestRuntime()
        {
            if (!IsQuest || (Time.unscaledTime < nextQuestDiagnosticAt &&
                lastDiagnosticPhase == Session.Phase && lastDiagnosticAmmo == Gun.Ammo)) return;
            Debug.Log($"[Walkpadead Quest] game phase={Session.Phase} input={Input.Available} " +
                $"speedMps={Session.SpeedMps:0.000} position={player.position:F2} ammo={Gun.Ammo} kills={Session.Kills}");
            lastDiagnosticPhase = Session.Phase; lastDiagnosticAmmo = Gun.Ammo;
            nextQuestDiagnosticAt = Time.unscaledTime + 5;
        }

        private void TickEnemies(float dt)
        {
            for (var i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy.Dead) { Destroy(enemy.gameObject); enemies.RemoveAt(i); continue; }
                if (!enemy.Tick(dt, player.position, IsQuest)) continue;
                Session.Damage(18); Destroy(enemy.gameObject); enemies.RemoveAt(i);
            }
            if (Session.Phase != RunPhase.Running) return;
            spawnIn -= dt;
            if (spawnIn <= 0 && enemies.Count < 6)
            {
                spawnCount++;
                Spawn(17 + spawnCount % 3, (spawnCount % 3 - 1) * 1.7f);
                spawnIn = Mathf.Max(3.8f, 7 - (float)Session.ElapsedSeconds / 40);
            }
        }

        private void Spawn(float ahead, float lane)
        {
            var position = IsQuest ? player.position + locomotion.Heading * ahead +
                Vector3.Cross(Vector3.up, locomotion.Heading) * lane : new Vector3(lane, 0, player.position.z + ahead);
            enemies.Add(ZombieActor.Spawn(position, enemyRoot));
        }

        public void StartOrResume()
        {
            if (!ControlsReady) return;
            if (Session.Phase == RunPhase.Defeated || Session.Phase == RunPhase.Escaped)
            {
                restartWithSimulator = IsQuest && offlineSimulation;
                SceneManager.LoadScene(IsQuest ? "QuestPrototype" : "Prototype"); return;
            }
            if (source != null && Session.Resume(source.Read()))
            { ignoreFireUntil = Time.unscaledTime + 0.2f; Quest?.ResetButtons(); }
        }

        public void PauseRun(string reason)
        { Session.Pause(reason); SpeedBonusesEnabled = false; Quest?.ResetButtons(); }

        public void Reconnect()
        {
            offlineSimulation = false;
            UseInputSource(new WebSocketTreadmillSource(connectionPath));
        }

        public void UseSimulator()
        { offlineSimulation = true; UseInputSource(new SimulatedTreadmillSource()); }

        public void ToggleSpeedBonuses()
        {
            if (SpeedBonusesEnabled)
            { SpeedBonusesEnabled = false; BonusStatus = "Speed bonuses off"; return; }
            SpeedBonusesEnabled = ControlsReady && CanControlSpeed && Input?.Available == true;
            BonusStatus = SpeedBonusesEnabled ? "Speed bonuses on · " + SpeedRangeLabel :
                CanControlSpeed ? "Fresh walking input required" : SpeedRangeLabel;
        }

        public bool TryCollectSpeedBonus(int deltaTenths)
        {
            if (!SpeedBonusesEnabled || Session.Phase != RunPhase.Running || !Input.Available || !CanControlSpeed)
            { BonusStatus = "Pickup passed · speed bonuses off"; return false; }
            if (speedRequest != null || Time.unscaledTime < nextSpeedRequestAt)
            { BonusStatus = "Pickup skipped · speed change cooldown"; return false; }
            if (deltaTenths != -1 && deltaTenths != 1) return false;
            nextSpeedRequestAt = Time.unscaledTime + 10;
            BonusStatus = (deltaTenths > 0 ? "+0.1 km/h" : "-0.1 km/h") + " requested · awaiting pad";
            speedRequest = ((ITreadmillSpeedControl)source).AdjustSpeedAsync(Input, deltaTenths);
            return true;
        }

        /// <summary>Takes ownership of an input adapter. Switching sources always pauses a run.</summary>
        public void UseInputSource(ITreadmillSource next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            Session.Pause("Reconnecting input");
            SpeedBonusesEnabled = false;
            source?.Dispose();
            source = next;
            Input = source.Read();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (shuttingDown || connectionPath == null) return;
            if (!focused)
            {
                PauseRun(IsQuest ? "Quest app interrupted — press X to resume" : "Window unfocused — resume when ready");
                // Discard the socket's buffered data after app suspension or focus loss.
                source?.Dispose(); source = null;
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else if (source == null) RestoreInput();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!IsQuest || shuttingDown || connectionPath == null) return;
            if (paused) { PauseRun("Quest suspended — press X to resume"); source?.Dispose(); source = null; }
            else if (source == null) RestoreInput();
        }

        private void RestoreInput() { if (offlineSimulation) UseSimulator(); else Reconnect(); }

        private void OnApplicationQuit() { shuttingDown = true; source?.Dispose(); }
        private void OnDestroy()
        {
            source?.Dispose(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
