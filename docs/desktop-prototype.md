# Desktop prototype

The Unity project implements a two-minute survival run on a procedural service road. Live or simulated walking speed moves the player forward at one Unity unit per metre. Mouse aiming is independent of that direction. Approaching infected take two body shots or one head shot, while a pursuing horde closes in when the player stops. The sidearm has a 12-round magazine and reloads with R. A completed run shows distance, kills, and time survived.

**Status:** the macOS ARM64 app is built and launches on M4. Unity import/rendering and all six Play Mode tests pass. The real Rust simulator drives the rendered game, physics hits score kills, and the user confirmed the standalone controls and live speed sync. Road bonuses have passed simulated end-to-end confirmation in both directions. The user confirmed the first physical increase; a later timeout prompted a Bluetooth write-spacing fix and clearer lockout feedback, pending a physical retest.

## Tools and versions

- Unity **6000.3.28f1**, Apple Silicon, revision `8f082d2fe476`.
- Universal Render Pipeline **17.3.0**; Input System **1.20.1**; uGUI **2.0.0**; Newtonsoft JSON **3.2.2**; Unity Test Framework **1.6.0**. Versions follow the downloaded editor's package manifest.
- Independent checks use a local **.NET 10.0.403** SDK and Newtonsoft JSON 13.0.2, the version used by Unity's JSON package. The game targets Unity's .NET Standard API profile, not .NET 10.
- The activated editor is extracted at `.tools/Unity.app`. Unity Hub is installed in `/Applications/Unity Hub.app`. `.tools/` is ignored by Git. Neither repository has been published.
- Unity CLI **1.0.0-beta.17** and Pipeline **0.10.0-exp.1** provide live editor commands. Pipeline's runtime server is excluded from the game build.

The version is selected from [Unity's official release API](https://services.api.unity.com/unity/editor/release/v1/releases?limit=1&version=6000.3.28f1&platform=MAC_OS&architecture=ARM64). Unity documents [license management through Hub](https://docs.unity.com/en-us/hub/manage-license) and [URP for Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/universal-render-pipeline/urp-introduction).

## Play

From this repository:

```sh
python3 scripts/play.py --device KS-NACH-X21C --allow-speed-control --min-speed-kmh 1 --max-speed-kmh 5
# Or choose development input explicitly:
python3 scripts/play.py --simulate --allow-speed-control
```

These commands start the sibling Rust bridge and launch `Builds/macOS/Walkpadead.app` with its private connection-file path. The live option reads the X21; simulation starts at 3 km/h by default. Omitting both source options selects simulation. A failed live connection never falls back to simulation. `--allow-speed-control` exposes optional ±0.1 km/h requests; omitting it makes the bridge read-only.

On macOS, double-click `scripts/Play X21.command` or `scripts/Play Simulator.command` in Finder for the same launch flow. Close the game to end its bridge, or press Ctrl-C in the launcher's Terminal window. The default session limit is one hour; expiry pauses the game. Launch through one of these commands so the app receives a valid connection file.

To rebuild, use **Walkpadead → Build macOS ARM64** in the editor. For a batch build, close that project's editor first and run `python3 scripts/unity.py build`. The build setup selects [Unity's macOS ARM64 architecture](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/osxstandalone/userbuildsettings/architecture) explicitly.

For an interactive editor session, add this repository as a project in Unity Hub using the pinned editor, then run:

```sh
python3 scripts/play.py --bridge-only
```

In Unity, select **Walkpadead → Open prototype**, then press **Play**. The **Start run** button becomes available when fresh walking input arrives. Enter also starts or resumes; Escape pauses; mouse movement aims; left click shoots; R reloads. The game starts paused and never automatically resumes after an interruption.

`--bridge-only` uses `.runtime/connection.json`, which an already-running editor can find. For a new editor process launched with the telemetry bridge:

```sh
python3 scripts/play.py --editor .tools/Unity.app/Contents/MacOS/Unity
```

Use `--bridge /path/to/walkingpad` if the SDK executable is elsewhere. `scripts/unity.py` accepts `--editor /path/to/Unity` or `UNITY_EDITOR` for another installation. The launcher defaults to the existing Mac CLI bundle in the sibling `walkingpad-sdk` repository.

## Exercise input faults

```sh
python3 scripts/play.py --demo-faults --speed-kmh 3
```

The fault timeline is measured from simulator launch: reports age between seconds 8–12, speed expires at roughly second 11, the belt reports stopped between 12–15, and walking resumes from second 15. The game pauses when speed expires, and fresh input only re-enables the Resume button. A valid stop produces zero movement immediately. Movement always uses the reported speed directly, without smoothing or a minimum walking speed.

For an editor test, start Play promptly after launching `--bridge-only --demo-faults`, or restart the bridge while the scene is already playing. The client re-reads the connection file on retry, so a new port/token works after restart. It pauses again even if reconnection happens between rendered frames. An old credential file left by an abruptly killed bridge is never overwritten automatically; stop any existing bridge before removing that stale file.

## Live X21 input

```sh
python3 scripts/play.py --bridge-only --device KS-NACH-X21C
# Launch the standalone game:
python3 scripts/play.py --device KS-NACH-X21C
```

`--device` accepts an exact OS ID or unique device-name substring. `--simulate` remains optional; simulation is the default when no device is selected. `--speed-kmh` and `--demo-faults` are rejected with live input. Wake the X21 and disconnect other treadmill apps before connecting. These commands omit the speed-control flag and only read telemetry. The user operates physical start/stop controls in every mode.

The adapter accepts `live_device`, `simulation`, or `replay` only when the packet source matches the connection document. The HUD shows that source, and changing sources pauses gameplay. The SDK currently serves live and simulated WebSockets; its replay CLI is offline, so a replay WebSocket producer remains future work. Quest/network transport also remains future work.

This X21 returned 0 km/h/stopped and later 3.2 km/h/running with increasing steps/time. The user confirmed the latter against the physical display. Distance, calories, and other absent metrics remain unknown. The pad stopped advertising when asleep. Reports arrived about every two seconds, so speed expires after three seconds to accommodate observed timing jitter.

## Road speed bonuses

The Finder launchers enable the bridge's control capability with **1.0–5.0 km/h** limits. For terminal/editor use, add `--allow-speed-control --min-speed-kmh 1 --max-speed-kmh 5` to the launcher command. Press **B** or **Enable ±0.1 km/h bonuses** in the game menu to opt in for the current run. Bonuses start off and are disabled on input loss, source changes, window focus loss, or an unconfirmed command.

Walk through the green **+0.1 km/h** or orange **−0.1 km/h** marker. The first is at 8 metres; subsequent markers are 18 metres apart and alternate direction. Each marker is consumed once. A change requires an already-running belt, fresh input, no pending request, and ten seconds since the last request. A pickup outside the configured range is rejected. Start, stop, mode changes, and automatic restoration of a prior speed are not supported.

The HUD shows the requested change, then confirmation or the reason it was rejected. An uncertain response disables bonuses; the SDK also locks further commands for that bridge session. Check the pad's display before starting a new bridge session. No request is retried automatically.

Player movement follows **reported** speed, never the requested target: `metres_per_second = kilometres_per_hour / 3.6`, at one Unity unit per metre. Thus 3.0 km/h means 0.8333 m/s and 3.1 km/h means 0.8611 m/s. The game does not predict acceleration between device reports. A game pause, quit, or connection loss does not stop the physical belt.

## Project structure

| Location | Responsibility |
| --- | --- |
| `Assets/Walkpadead/Core` | Engine-independent telemetry/control contracts, authenticated WebSocket clients, per-field freshness, and run state. |
| `Assets/Walkpadead/Runtime` | World-forward locomotion, mouse aiming, raycast weapon, infected, road pickups, procedural street, HUD and source lifecycle. |
| `Assets/Walkpadead/Editor` | Reproducible scene/material/pipeline creation and ARM64 build entry point. |
| `Assets/Walkpadead/Tests/PlayMode` | Unity integration checks for locomotion, real physics hits/reload, input loss freezing enemies/time, and restart. |
| `tests/Walkpadead.Checks` | Executable tests of the same production Core sources, including the real Rust bridge. |
| `tests/UnityCompile` | Supplemental C# API compilation against the downloaded editor/template assemblies. |
| `scripts` | Live/simulated bridge and game launcher, Unity build/test wrapper, independent checks. |

Scene and material assets are generated by **Walkpadead → Prepare project** on the first editor import. The saved scene is `Assets/Walkpadead/Scenes/Prototype.unity`; runtime initialization builds its visible geometry. Preparation preserves existing generated materials and pipeline assets, so later art edits are not overwritten. All visuals and the short weapon sound are procedural placeholders.

## Input contract and pause behavior

The client authenticates to `ws://127.0.0.1:<port>/telemetry` using the connection file's bearer token. It rejects external hosts, URL credentials/query strings, oversized messages, unsupported schema versions, malformed numeric fields, and decreasing sequence numbers. Tokens are not printed or stored in Unity assets.

Freshness combines the transmitted age of **speed** with the client's monotonic receipt clock. Repeated sequence numbers cannot refresh a measurement. A silent connection expires after three seconds even while its socket remains open. `usable_speed_mps: null` means unavailable; zero is a valid stopped value. Unknown optional counters remain null, and device counters never overwrite game distance.

Unavailable input freezes movement, encounter timers, enemy attacks, and run time. An interruption over half a second also pauses to avoid applying a large movement step. Window focus loss disposes the socket; refocusing connects afresh and still requires manual resume. Mouse input changes only aim. There is no keyboard fallback that silently replaces walking input.

## Validation

Independent checks, without launching Unity:

```sh
bash scripts/check.sh
bash scripts/check.sh --rust-bridge '../walkingpad-sdk/dist/WalkingPad SDK.app/Contents/MacOS/walkingpad'
```

The second invocation passed **22 checks** on this M4 Mac: contract rejection, unknown measurements, stale age/receipt time, duplicate/out-of-order sequences, exact movement scaling and stop handling, manual resume and reconnection, frame interruption, full timed-run state, authenticated fragmented WebSocket input, silent/closed sockets, source matching and switching, the measured X21 cadence, and the Rust fault-demo lifecycle. The game/API sources also compiled as C# 9 / .NET Standard 2.1 against the editor's actual managed and template assemblies with zero warnings/errors. This supplemental compile does not perform Unity's package import, shader compilation, scene rendering, or native build.

Batch verification (close the project's GUI editor first):

```sh
python3 scripts/unity.py test
python3 scripts/unity.py build
python3 scripts/play.py
```

Batch test output goes to `TestResults/playmode.xml`; logs go to `Logs/`. The live-editor run passed six tests, saved in `TestResults/unity-playmode.json`; the native build summary is `TestResults/macos-build.json`. The final executable is ARM64 and builds with zero warnings/errors. `TestResults/prototype-menu.png` and `TestResults/prototype-gameplay.png` capture the rendered UI and active scene. The actual game advanced at simulated 3 km/h, and two physics body hits consumed two rounds and recorded one kill. The user confirmed the standalone controls. A later rendered run collected both bonus types through the production C# control client and Rust simulator, confirming 3.0 → 3.1 → 3.0 km/h. Captures are `TestResults/bonus-increase.png` and `TestResults/bonus-decrease.png`.

A complete human two-minute run, physical speed-change/stop timing, and longer hardware sessions still need validation. Windows, Linux, and Quest execution remain untested. Art, audio, and encounter balancing are prototype quality.

With the editor already open, use the installed Unity CLI instead of starting another editor:

```sh
unity recompile --project-path . --format json
unity command test run --project-path . --format json -- --mode playmode --filter Walkpadead.PlayModeTests --filter-type assembly --async-tests true
unity command test status --project-path . --format json
```

For an opt-in hardware acceptance check, run a bounded live bridge in one terminal and use its private connection file in another:

```sh
bash scripts/check.sh --live-connection-file .runtime/connection.json
```

This checks the actual production C# client and gameplay core, observes a fresh live reading, and waits for the bridge to end so it can verify that movement pauses. It does not launch Unity or change the belt. The bridge should end within the check’s 120-second timeout; use `scripts/play.py --bridge-only --device KS-NACH-X21C --seconds 35` for a short session.
