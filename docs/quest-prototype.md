# Quest prototype

The Quest scene runs the same two-minute survival game with headset-directed movement and a tracked right-hand weapon. Desktop controls remain available in the separate `Prototype` scene.

## Controls

| Input | Action |
|---|---|
| Look left/right | Steer virtual walking in that direction |
| Look up/down | Look around; movement stays level and keeps its full speed |
| Right controller orientation | Aim the sidearm and menu pointer |
| Right trigger | Fire once per press; point and select menu buttons |
| Right A | Reload |
| Right B | Pause |
| Left X | Start, resume or retry |
| Left Y | Toggle optional ±0.1 km/h pad bonuses |

The controller pose comes from OpenXR, which combines controller sensor and tracking data. There is no raw gyro integration or accumulated gyro drift in game code. Walking speed remains the reported pad speed divided by 3.6, in metres per second. Looking almost vertically keeps the previous horizontal direction.

The Quest uses an open extraction yard so gaze steering works in any direction. Zombies pursue the player across the ground, and pickups require walking within one metre. The right controller’s aim is independent of the headset heading. Menus and telemetry are rendered on a world-space canvas in front of the headset.

Tracking loss, headset removal, app focus loss and suspension pause gameplay. Restoring tracking does not resume the run automatically. Press X or select Resume, then release and press the trigger to fire. A game pause does not stop the physical belt. Physical speed-bonus retesting remains deferred as requested.

## Build

Use the pinned **Unity 6000.3.28f1** editor with **Android Build Support**, SDK, NDK and OpenJDK installed. Open the project, then choose **Walkpadead → Build Quest APK**. The output is `Builds/Quest/Walkpadead.apk`.

The menu command configures Android ARM64 IL2CPP, Vulkan, API 32 minimum / API 35 target, OpenXR 1.16.1, single-pass stereo, and the Touch, Touch Plus and Touch Pro interaction profiles. Quest 2, Pro, 3 and 3S are declared targets; declaration is not hardware validation. The development APK uses application ID `dev.walkpadead.quest` and includes only `QuestPrototype`.

For a running editor, the equivalent Unity CLI build is:

```sh
~/.unity/bin/unity command build run --project-path "$PWD" --format json -- \
  --target Android --output-path Builds/Quest/Walkpadead.apk \
  --scenes '["Assets/Walkpadead/Scenes/QuestPrototype.unity"]' \
  --options '["Development"]' --confirm true
```

Run **Walkpadead → Prepare Quest** once before the CLI build. The menu build performs preparation automatically. `Assets/Walkpadead/Editor/QuestProjectSetup.cs` owns these settings; generated Unity assets are saved through the editor API.

## Install and test without the pad

Enable Developer Mode on the Quest, connect it to the Mac by USB and accept the headset’s USB debugging prompt. The launcher accepts `--adb /path/to/adb` if Android tools live elsewhere.

```sh
python3 scripts/quest.py --devices
python3 scripts/quest.py --install-only
```

Open **Walkpadead** in the headset’s **Unknown Sources** apps, point at **Use offline simulator · 3.0 km/h**, and press X to start. This explicitly selected simulator runs entirely on the headset and needs neither the Mac nor a treadmill. The choice is retained when you retry the run. It cannot send physical speed commands.

To test the production telemetry client with the Rust bridge instead:

```sh
python3 scripts/quest.py --simulate
```

This installs the APK, starts the Rust simulator, forwards its localhost WebSocket port to the Quest, copies the authenticated connection file into private app storage and launches the game. Use `--skip-install` for subsequent runs, or `--serial SERIAL` when multiple devices are attached. Ctrl-C ends the bridge; lost or stale telemetry pauses the game.

## WalkingPad connection

The first headset integration uses the existing Mac BLE bridge. Keep the X21 awake and WalkingPad+/KS Fit disconnected. Start it with:

```sh
python3 scripts/quest.py --device KS-NACH-X21C
```

USB ADB reverse carries the original authenticated loopback URL. Keep the cable and launcher connected for this development transport. The app runs on the Quest; the Mac provides treadmill telemetry. No LAN listener is opened, and credentials are passed through stdin rather than command arguments or logs. The connection file is private to the debug app and removed when the launcher exits while the device remains connected.

Optional pad speed bonuses require both `--allow-speed-control` on the launcher and the Y/menu toggle in the game. Limits stay fixed at **1.0–5.0 km/h** with **±0.1 km/h** per pickup. Existing confirmation, cooldown and lockout behavior is unchanged. A failed live connection never selects simulation automatically.

Direct Quest Bluetooth and a paired Wi-Fi companion connection are future work. The `run-as` credential transfer requires this development APK; it is not a release distribution workflow.

## Verification

The APK build succeeds with zero warnings/errors, passes signature verification, and has the expected ARM64/OpenXR/VR manifest configuration. Fourteen Unity Play Mode tests and 22 core checks pass.

Automated Unity tests cover horizontal gaze steering, pitch-independent speed, independent controller aim, trigger release gating, reload, tracking loss/headset removal, planar enemies/pickups, the OpenXR input bindings and existing desktop behavior. The startup regression also covers devices that are already tracking before actions are enabled: gameplay actions explicitly read their initial state, while UI clicks still require a fresh press. Core WebSocket and movement checks remain in `tests/Walkpadead.Checks`. Test summaries and render captures are in ignored `TestResults/`.

The development APK is installed on the user's Quest 2 (Android 12/API 32), and the user confirmed the in-game menu. The first device run exposed a controller startup issue and stripped sphere/capsule colliders. A regression test reproduced the initial-tracking issue before the fix and passes after it. `Assets/Walkpadead/link.xml` preserves the procedural primitive components in Android builds. Development builds log tracking, button edges and gameplay state under `[Walkpadead Quest]` for on-device verification, without connection credentials.

The corrected APK is installed; controller behavior, stereo appearance, latency, headset lifecycle behavior and frame rate still require on-device validation. No physical walking-pad speed changes are part of these checks.

References: [Unity OpenXR input](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/input.html), [Oculus Touch profile](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.16/manual/features/oculustouchcontrollerprofile.html), [Android Debug Bridge](https://developer.android.com/tools/adb).
