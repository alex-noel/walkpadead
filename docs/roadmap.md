# Walkpadead delivery roadmap

## Confirmed starting point

- Hardware: KingSmith X21 and an Apple Silicon M4 Mac.
- First playable: laptop game with mouse aiming.
- Eventual platform scope: macOS, Windows, Linux, and Meta Quest.
- Reusable component: a separate open-source walking-pad library.
- Existing comparison app: WalkingPad+ 1.0.8 installed locally.

This X21’s BLE layout and v5 text encoding are identified. Speed, run state, steps, and time are observed at roughly two-second intervals. Additional metrics, acceleration behavior, longer-session reliability, and other firmware variants remain hardware validation work.

The independent SDK, discovery/inspection CLI, FTMS and KingSmith decoding, capture/replay tools, and live/simulated WebSocket bridge are implemented. The user lifted the simulation-only restriction. Direct X21 reads include a stopped belt and user-confirmed walking at 3.2 km/h. See [implementation status](implementation-status.md) for the completed slice and remaining work.

The Unity movement/shooting prototype and a short two-minute survival loop are built for macOS ARM64. Six Unity Play Mode tests pass; the rendered scene receives speed from the Rust bridge, real raycasts produce kills, and the user has confirmed the standalone controls and live speed sync. Optional road pickups request ±0.1 km/h changes within 1.0–5.0 km/h; both directions pass simulated end-to-end confirmation. The [desktop prototype guide](desktop-prototype.md) documents launch and validation. This preliminary run does not complete the later five-minute game milestone or the long-session hardware checks.

## Phase 1 Build the controller library and companion

### Milestone 1 Discover the X21 protocol

**Deliverable:** a device profile, a small telemetry investigation program, and anonymized packet fixtures.

1. Record the model/firmware if available, Bluetooth name, macOS version, and installed comparison-app version.
2. Verify what WalkingPad+ displays in a normal session, including speed units and available counters. Stop the belt before changing connection owners.
3. Disconnect other treadmill apps. Scan from the Mac, then enumerate all GATT services, characteristics, properties, and relevant readable capabilities.
4. Choose the matching protocol family from the actual device layout. If it requires an unknown handshake, keep that investigation isolated from the main SDK.
5. Subscribe to notifications and issue only understood status/handshake requests. Use the physical controls to change speed during the measurement session.
6. Compare steady speeds, acceleration, stopping, and session counters against the pad display and separate comparison-app sessions. Measure report intervals and stop-report delay.
7. Record a short sample, then test reconnect, an idle belt, and app contention. Confirm whether telemetry is available without requesting motor control.

**Exit criteria:** valid speed reports with understood units; known available/unsupported fields; documented update cadence; an identified driver; and a reproducible connection process on M4. No motor changes caused by connecting, reading, disconnecting, or reconnecting.

**Decision gate:** if the X21 uses an unsupported protocol, finish the required decoder/handshake investigation before promising a hardware delivery date. Continue game work with simulated input. Installation of WalkingPad+ is not evidence of a public SDK or of current connectivity.

### Milestone 2 Release the first Rust SDK

**Deliverable:** an independently buildable library and CLI supporting this X21 on macOS ARM64.

Implement the measured protocol first, plus the transport, capabilities, connection lifecycle, nullable metrics, field freshness, diagnostics, recording, and replay. Keep additional driver slots available without implementing unverified models speculatively.

Suggested CLI commands: `scan`, `inspect`, `watch`, `record`, `replay`, and `bridge`. The public API and events follow the architecture document. Pin dependencies and record the minimum supported toolchain.

**Exit criteria:** a 30-minute telemetry session completes without a crash; speed agrees with the display within its reported resolution; stale data is visibly invalidated; disconnect/reconnect works; counters handle resets; and the same recording reproduces the same normalized events. Hardware measurements determine latency claims.

Validate parsers with captured normal, truncated, malformed, and unknown frames. Test unit conversion, optional fields, bad checksums, counter reset/wrap, reconnect epochs, and stale-speed behavior. Parser fuzzing is useful at this external binary-data boundary. Run formatting, linting, and library tests in CI.

### Milestone 3 Package the companion app

**Deliverable:** a Mac app with device selection, connection status, live metrics, and a local telemetry bridge.

Use the shared Rust implementation behind a minimal Tauri interface. Package the Bluetooth permission description correctly, and show connection conflicts as actionable status messages. The app and CLI are alternative hosts for one BLE session. Include a sample C# client and a replay mode.

**Exit criteria:** a packaged app can obtain Bluetooth permission and connect; a Unity client receives telemetry; restarting the game does not restart the treadmill; restarting the bridge makes the game pause; permission denial, revoked permissions, and stale data have clear UI states. The network listener is loopback-only by default.

Release the initial SDK with installation instructions, a small example, attribution, protocol notes, and a compatibility matrix that distinguishes build-tested from device-tested combinations. Public repository/package publication is a later delivery action; this plan does not perform it.

## Phase 2 Build the zombie game

### Milestone 4 Make the movement and shooting prototype

**Deliverable:** a native Mac build with a simple route, mouse aiming, one gun, and one approaching zombie.

Create the game first against a simulator implementing the same telemetry interface. Implement direct 1:1 speed conversion, stop handling, pause/resume, a speed indicator, and a health indicator. Swap the simulator for the local WebSocket source once the SDK works. Road bonuses use the separate optional command interface and wait for confirmed telemetry before virtual movement changes.

**Exit criteria:** changing simulated or real speed changes virtual forward speed; a reported stop stops forward movement; mouse aiming stays independent of locomotion; shooting reliably hits the target; stale input pauses the session; resuming never reuses an old nonzero speed.

### Milestone 5 Deliver the first complete run

**Deliverable:** a five-minute desktop survival loop.

Add a pursuing-horde meter, a second enemy behavior, encounter pacing, ammunition/reload, a start/calibration flow, a finish condition, and a results screen. Use simple placeholder art until the loop is fun. Calibrate to the player's chosen pace and tune difficulty through encounters.

**Exit criteria:** a player can connect, start a run, walk and aim, finish or lose, and restart without developer intervention. Device loss pauses enemy attacks and scoring. A complete replay can run without hardware. A 30-minute desktop playtest checks stability and whether the walking/shooting combination is enjoyable.

### Milestone 6 Validate the other desktop systems

**Deliverable:** Windows and Linux SDK/companion builds and desktop game builds where supported and tested.

Add Windows x64 and an explicitly selected Linux distribution/architecture to the release matrix. Add Intel macOS only when it is a needed target. Reuse shared parsers and telemetry contracts, then test each OS's Bluetooth permissions, connection lifecycle, and suspend/resume with actual hardware.

**Exit criteria:** each advertised device-tested combination passes the same connection and session scenarios. CI compilation alone never changes its status to device-tested.

### Milestone 7 Add Quest with the Mac bridge

**Deliverable:** a game that runs on the headset, uses gaze steering and Quest controller aiming, and receives X21 telemetry from the Mac. The development transport is USB ADB reverse; paired local-network transport remains planned.

The OpenXR rig, horizontal gaze steering, controller trigger/aim, world-space menus and tracking-loss pause are implemented. Validate on the selected Quest hardware and profile mobile rendering and input responsiveness. Add authenticated encrypted network pairing after the USB development path. This is a standalone game executable with a companion dependency for treadmill input.

**Exit criteria:** controller aiming and shooting work; measured telemetry age remains acceptable; network loss pauses gameplay; suspend/headset removal invalidates movement input; and the scene sustains the chosen frame-rate target on-device. Begin VR evaluation with simulated input and validate the physical setup before moving-belt trials.

### Milestone 8 Remove the companion dependency

**Deliverable:** a fully standalone Quest edition with an embedded SDK.

Begin with a tiny direct-BLE telemetry viewer. Package the Rust native library, JNI initialization, C ABI, and Unity wrapper. Test the actual X21/headset combination. Keep the network source available as a fallback.

**Exit criteria:** the headset discovers the X21, reads and validates its telemetry, handles permissions and app lifecycle transitions, and recovers without motor commands. A release build passes the same scenarios as the development build. Resolve the applicable Meta Bluetooth-permission review before promising store distribution.

## Proposed effort ranges

These are planning estimates for one developer familiar with Rust and Unity, using placeholder art. They are not measured commitments.

| Work | Indicative effort |
| --- | --- |
| X21 investigation | 2–5 developer-days if a compatible protocol is already understood; open-ended discovery if it is not. |
| Rust SDK and diagnostic CLI | 5–10 developer-days after the protocol gate. |
| Companion app and Unity data adapter | 3–5 developer-days. |
| First complete desktop run | 5–10 developer-days. |
| Windows/Linux validation and packaging | 3–7 developer-days, dependent on access to test machines. |
| Quest game using the Mac bridge | 5–10 developer-days after desktop gameplay works. |
| Direct Quest BLE and native packaging | 5–15 developer-days after a successful headset feasibility test. |

Reserve extra time for art, sound, public-release packaging, store review, and wider model support. The first useful goal is one measured X21 speed stream driving one simple Mac scene.

## First implementation backlog

| Order | Task | Completion evidence |
| --- | --- | --- |
| 1 | Create the separate SDK workspace and a macOS ARM64 discovery tool. | Build runs and can inspect the selected X21's services. |
| 2 | Capture a device profile and choose the matching protocol. | Recorded service/characteristic layout and driver decision. |
| 3 | Decode speed and expose fresh/stale status. | Compared readings and timing log across speed changes/stops. |
| 4 | Add recording and replay fixtures. | Deterministic normalized events without Bluetooth. |
| 5 | Serve the versioned telemetry stream locally. | C# sample receives and invalidates reports correctly. |
| 6 | Build a Unity route, mouse aim, and target. | A playable Mac scene driven by simulation and real telemetry. |

The only unresolved inputs that materially affect later implementation are the measured X21 protocol/firmware, the target Quest model, the desired public distribution channels, and licensing choices for the game and its assets. They do not prevent starting the Mac/X21 investigation.
