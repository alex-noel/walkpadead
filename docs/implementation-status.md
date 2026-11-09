# Implementation status

Updated 8 November 2026.

The independent Rust SDK lives at `/Users/alexnoel/Documents/ChatGPT/walkingpad-sdk`, in its own Cargo workspace and Git repository. The Unity game is a separate consumer. The simulation-only restriction has been removed.

## Implemented

- BLE scan, GATT inspection, passive capture, and opt-in telemetry capture.
- Standard FTMS decoding and a KingSmith text driver with bounded reassembly, handshake-based v1/v5 encoding selection, read-only initialization, and property polling.
- `watch --device …` and `bridge --device …` for real input; `bridge --simulate` remains available. A failed live session never uses synthetic input.
- Authenticated localhost WebSocket, private connection file, latest-value delivery, explicit live/replay/simulation provenance, and shutdown invalidation.
- The game launcher accepts `--device KS-NACH-X21C`. The C# client checks the selected source, the HUD names it, and source changes require manual resume.
- Three-second speed expiry, based on the observed roughly two-second X21 cadence. Per-field age, receipt silence, stop, disconnect, counter resets, and connection epochs remain explicit.
- Sanitized hardware profile, handshake-only recording, and stopped-telemetry fixtures for regression tests. Raw captures and local identifiers remain in ignored runtime directories.

The bridge remains read-only by default. `--allow-speed-control` enables authenticated **±0.1 km/h** requests within the user's **1.0–5.0 km/h** limits. Each request requires fresh running telemetry, matches the current connection and speed, and waits for a later speed report to confirm it. Only one request may be pending, with ten seconds between requests. Uncertain commands lock that bridge session and are never automatically retried. Start/stop, mode, user-ID, clock, and reconnect speed restoration are not implemented.

The game has alternating green/orange road pickups, a B/menu toggle on desktop (Y/menu on Quest) that starts off, and requested/confirmed/rejected feedback. Player movement uses the latest reported pad speed directly at one Unity unit per metre, without smoothing. It never substitutes a requested target for telemetry.

## Quest implementation

The `QuestPrototype` scene adds an OpenXR headset and right-controller rig. Headset yaw steers ground-plane movement at exactly the reported walking speed; pitch does not change speed or height. The tracked controller aim pose drives the sidearm, and its trigger fires once per press. A reloads, B pauses, X starts/resumes/retries, and Y toggles the existing opt-in pad bonuses. A world-space HUD/menu supports controller pointing and trigger selection. The Quest extraction yard, enemies and pickups support movement across both horizontal axes.

Tracking loss, headset removal, focus loss and suspension pause the run, disable speed bonuses and require explicit resume. Held buttons must be released before they can trigger again. The Quest menu includes an explicit offline simulator. `scripts/quest.py` can instead launch the Mac Rust bridge and forward its authenticated loopback connection to the headset over USB ADB reverse. Direct headset BLE and paired Wi-Fi transport are not implemented. The APK is installed on the user's Quest 2, and the in-game menu is confirmed. The controller startup fix is deployed for verification; performance remains unmeasured.

## Hardware verification

The user’s `KS-NACH-X21C` exposes service `00011234-0000-1000-8000-00805f9b34fb`, write-without-response `0001fed7`, and notify `0001fed8`. No FTMS service was present. Its handshake selects the v5 substitution alphabet, so BeltControl’s default UUIDs/table cannot be used unchanged.

With WalkingPad+ closed, direct BLE capture returned:

- Stopped: 0 km/h, `runState 0`, 0 steps, and 0 elapsed seconds.
- Running: 3.2 km/h (0.8889 m/s), `runState 1`, and increasing steps/time. The user started the belt physically and confirmed the display’s 3.2 km/h reading.
- Distance was absent; it remains null. Calories and other metrics have not been validated for this unit.

A 90-second live bridge session completed. A subsequent 35-second session verified **physical X21 → Rust BLE bridge → production C# client → gameplay core**, including movement and pause on bridge shutdown. Its recorded stream contained 177 snapshots and no stale reading after the first valid measurement while connected. Final speed became unavailable and the credential file was removed normally.

The pad stopped advertising while asleep; waking its display restored discovery. Initialization and polling are verified together, not claimed to be the minimal command sequence. These short sessions do not establish long-term reliability, measured belt velocity, acceleration/stop latency, or support for other firmware variants.

The new control path has passed a rendered Unity run against the Rust simulator: collected +0.1 and −0.1 pickups confirmed **3.0 → 3.1 → 3.0 km/h**. After the pad was awakened, the user confirmed a physical pickup increased speed from 3.2 to 3.3 km/h. A diagnostic request for 3.4 km/h then timed out while reports stayed at 3.3, locking subsequent bonuses. A read-only capture showed manual mode already active. The transport now spaces fragments by 30 ms and complete lines by a further 60 ms, and the client disables bonuses when any request reports a locked bridge. Repeated physical changes still need retesting with the game closed to isolate diagnostic requests.

## Software verification

- 35 Rust tests: 23 library, 7 CLI integration, and 5 WebSocket integration, including control limits, duplicates, cooldown, canceled requests, confirmation timeout/lockout, authentication, and both speed-change directions.
- The library’s 22 portable tests also pass without the Bluetooth feature.
- Rust formatting and Clippy with warnings treated as errors pass.
- 22 C# regression checks, including direct movement scaling, source matching, two-second device cadence, stale/stop/resume handling, and the real Rust simulator fault demo.
- One additional physical-device acceptance check through the production C# client and gameplay core.
- Production and editor sources compile as C# 9 / .NET Standard 2.1 against Unity’s actual reference assemblies, with zero warnings/errors.
- Fourteen Unity Play Mode tests pass. Eight Quest checks cover horizontal gaze steering and pitch, OpenXR pose/button bindings, already-tracking devices at action startup, independent controller aim/reload, world-space menu selection by tracked pointer, tracking loss/headset removal and release-before-fire, and planar enemies/pickups. The six desktop checks cover: movement independent of aim, physics hits/cooldown/reload, input-loss pause/manual resume, scene reload creating a fresh playable run, bonus opt-in/one-time collection/telemetry-driven movement, and control-lock feedback while telemetry continues.
- Unity package import, URP material/scene creation, and rendered HUD/gameplay are verified. At simulated 3 km/h the actual game advanced 1.55 metres; two raycast body shots produced one kill and consumed two rounds.
- The Quest Android ARM64 IL2CPP APK builds with zero warnings/errors; Android/OpenXR validation is clean. APK signature verification passes. Manifest inspection confirms GameActivity, VR category, head tracking, OpenXR libraries, Internet permission and a debuggable app for private credential transfer. USB deployment and the game menu are confirmed on Quest 2. Device logs exposed stripped sphere/capsule colliders, now explicitly retained in the generated native registration. The controller startup regression fails before the initial-state fix and passes after it. Controller acceptance and performance validation are in progress with simulated speed.
- The macOS ARM64 development build succeeds with zero build warnings/errors. The standalone app launches on M4 with Metal, and the user confirmed Enter, firing, reload, and pause controls.

## Unity and remaining work

The activated Unity 6000.3.28f1 project contains a procedural route, forward movement, mouse aim, raycast sidearm/reload, infected, health, horde distance, a two-minute run/results flow, and pause/resume. The build is `Builds/macOS/Walkpadead.app`; Finder launchers under `scripts/` select live X21 or simulated input explicitly. The Unity CLI was updated to 1.0.0-beta.17 with user approval, and Pipeline 0.10.0-exp.1 drives editor verification. Pipeline's runtime server is excluded from the game build.

The first actual import uncovered and fixed duplicate test-assembly references and scene creation beside Unity's initial unsaved scene. Runtime bootstrap now runs on every Prototype scene load, fixing retry after a completed run. Mac architecture uses the platform-specific Unity build setting, verified by the built executable's ARM64 header. Local test/build summaries and captures are under ignored `TestResults/`.

Next work is physical bonus verification, a complete human survival run and difficulty tuning, longer X21 validation, physical acceleration/stop timing, optional metrics, automatic BLE reconnect, companion UI, Quest device validation and profiling, direct headset BLE, and paired Wi-Fi transport. Windows/Linux CI is configured but has not run; those platforms have not been hardware-validated. Neither repository has been published.

See the [Quest guide](quest-prototype.md), [desktop guide](desktop-prototype.md), SDK [usage](../../walkingpad-sdk/README.md), and SDK [validation record](../../walkingpad-sdk/docs/validation.md).
