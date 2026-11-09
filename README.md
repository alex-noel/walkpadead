# Walkpadead

A walking-pad-powered zombie survival game for an Apple Silicon Mac with mouse aiming, plus a standalone Quest prototype with gaze steering and tracked-controller shooting. The game accepts live KingSmith X21 telemetry through the independent [Rust WalkingPad SDK](../walkingpad-sdk/README.md), with simulation available for development.

The Unity prototype contains a procedural street, forward movement, mouse aim, a sidearm, approaching infected, a pursuing-horde meter, health, pause/resume, and a two-minute run with results. Road pickups can change pad speed by ±0.1 km/h within 1.0–5.0 km/h. Movement uses the pad's reported speed directly: km/h ÷ 3.6 metres per second. Its input and optional speed-control clients use authenticated local WebSockets.

**Hardware verified:** this `KS-NACH-X21C` uses the KingSmith text protocol, service `00011234`, and the v5 encoding table. The SDK read stopped-belt telemetry and 3.2 km/h with increasing steps/time; the user confirmed 3.2 km/h on the physical display and later confirmed live game speed sync. Distance was absent and remains unknown. The user confirmed the first physical +0.1 km/h pickup. A later request timed out and locked controls. Bluetooth write spacing and lockout feedback have been updated; repeated physical changes still need retesting.

**The Mac prototype is built.** Unity import and rendering are verified, 13 Play Mode tests pass across desktop and Quest, and the standalone controls have been confirmed by the user. The visuals and weapon audio are procedural placeholders.

**The Quest APK is built** at `Builds/Quest/Walkpadead.apk` (Android ARM64, OpenXR). Headset yaw steers movement; the right controller aims and fires. Connect an authorized Quest by USB and run `python3 scripts/quest.py --simulate`, or use `--install-only` and choose the offline simulator in the headset. See the [Quest guide](docs/quest-prototype.md) for controls and setup. Quest hardware testing is still pending.

Start a live bridge without Unity:

```sh
python3 scripts/play.py --bridge-only --device KS-NACH-X21C
```

Keep the pad awake and disconnect WalkingPad+/KS Fit before connecting the SDK. A sleeping pad may stop advertising. The launcher defaults to the packaged CLI in the sibling SDK repository. Live-input failure never falls back to simulation.

Play the built game with the X21:

```sh
python3 scripts/play.py --device KS-NACH-X21C --allow-speed-control --min-speed-kmh 1 --max-speed-kmh 5
```

On macOS, you can also double-click **scripts/Play X21.command** in Finder. **scripts/Play Simulator.command** runs the same game with clearly labeled simulated input. Both enable the optional speed-control channel with the 1.0–5.0 km/h limits and end their bridge when the game closes. Bonuses start **off** in the game; press **B** or use the menu button to enable them. Green pickups request +0.1 km/h and orange pickups request −0.1 km/h. The HUD shows whether the pad confirms the change. Omitting `--allow-speed-control` keeps the bridge read-only.

For simulated input from a terminal, use `python3 scripts/play.py --simulate` or omit the source option. In an open editor, start the bridge with `--bridge-only`, select **Walkpadead → Open prototype**, and press Play. **Enter** starts/resumes, **mouse** aims, **left click** fires, **R** reloads, and **Escape** pauses. The HUD identifies live, simulated, or recorded input. Stale input, disconnects, and source changes pause gameplay and require manual resume.

To rebuild with Unity 6000.3.28f1, use **Walkpadead → Build macOS ARM64** in the editor, or close the editor and run `python3 scripts/unity.py build`. The app is written to `Builds/macOS/Walkpadead.app`.

- [Quest controls, APK build and USB bridge setup](docs/quest-prototype.md)
- [Prototype setup and validation](docs/desktop-prototype.md)
- [Implementation status](docs/implementation-status.md)
- [Architecture and technology decisions](docs/architecture.md)
- [Delivery roadmap](docs/roadmap.md)

The SDK is a separate MIT-licensed Rust repository. Quest hardware validation, direct headset BLE, Wi-Fi pairing, companion UI and other-platform hardware validation remain future work. Neither repository has been published; game licensing remains a later decision.
