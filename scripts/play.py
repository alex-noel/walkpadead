#!/usr/bin/env python3
"""Launch the WalkingPad telemetry bridge and desktop game (simulation by default)."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]


def stop(process):
    if process is not None and process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=4)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--bridge-only", action="store_true", help="Feed an already-open Unity editor")
    mode.add_argument("--editor", type=Path, help="Open this activated Unity executable")
    parser.add_argument("--bridge", type=Path, help="Path to the walkingpad CLI executable")
    parser.add_argument("--game", type=Path, help="Override the game executable path")
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--device", help="Exact Bluetooth ID or unique name, e.g. KS-NACH-X21C")
    source.add_argument("--simulate", action="store_true", help="Use simulated input (the default)")
    parser.add_argument("--scan-seconds", type=int, default=8)
    parser.add_argument("--speed-kmh", type=float, help="Simulation speed, default 3 km/h")
    parser.add_argument("--seconds", type=int, default=3600)
    parser.add_argument("--demo-faults", action="store_true", help="Stale at 11s; stop at 12s; walk at 15s")
    parser.add_argument("--allow-speed-control", action="store_true", help="Allow optional +/-0.1 km/h road bonuses")
    parser.add_argument("--min-speed-kmh", type=float, default=1.0)
    parser.add_argument("--max-speed-kmh", type=float, default=5.0)
    args = parser.parse_args()
    if args.device is not None and (not args.device.strip() or args.speed_kmh is not None or args.demo_faults):
        parser.error("Live input requires a device name/ID; --speed-kmh and --demo-faults are simulation options.")
    speed_kmh = 3 if args.speed_kmh is None else args.speed_kmh
    if not 0 <= speed_kmh <= 20 or not 1 <= args.seconds <= 3600 or not 1 <= args.scan_seconds <= 30:
        parser.error("Speed must be 0–20 km/h, seconds 1–3600, and scan-seconds 1–30.")
    if not 0.5 <= args.min_speed_kmh < args.max_speed_kmh <= 6.0:
        parser.error("Speed bonus limits must stay within 0.5–6.0 km/h, with min < max.")
    sdk = ROOT.parent / "walkingpad-sdk"
    candidates = [sdk / "dist/WalkingPad SDK.app/Contents/MacOS/walkingpad", sdk / "target/debug/walkingpad"]
    bridge = args.bridge or next((p for p in candidates if p.is_file()), None)
    if bridge is None or not bridge.is_file():
        parser.error("Build the sibling walkingpad-sdk CLI first, or pass --bridge /path/to/walkingpad.")
    executable = args.editor or args.game or ROOT / "Builds/macOS/Walkpadead.app/Contents/MacOS/Walkpadead"
    if not args.bridge_only and not executable.is_file():
        parser.error("The game is not built yet. Close the editor and run python3 scripts/unity.py build; "
                     "or use --bridge-only with the project open in Unity.")

    runtime = ROOT / ".runtime"
    runtime.mkdir(mode=0o700, exist_ok=True)
    directory = None if args.bridge_only else Path(tempfile.mkdtemp(prefix="session-", dir=runtime))
    connection_file = (directory or runtime) / "connection.json"
    if connection_file.exists():
        parser.error("A connection file already exists. Stop its bridge first. If that bridge crashed, "
                     "remove .runtime/connection.json before trying again.")
    bridge_process = game_process = None
    try:
        command = [str(bridge.resolve()), "bridge",
                   "--seconds", str(args.seconds), "--connection-file", str(connection_file)]
        if args.device:
            command += ["--device", args.device, "--scan-seconds", str(args.scan_seconds)]
        else:
            command += ["--simulate", "--speed-kmh", str(speed_kmh)]
        if args.demo_faults:
            command.append("--demo-faults")
        if args.allow_speed_control:
            command += ["--allow-speed-control", "--min-speed-kmh", str(args.min_speed_kmh),
                        "--max-speed-kmh", str(args.max_speed_kmh)]
        bridge_process = subprocess.Popen(command, cwd=ROOT)
        deadline = time.monotonic() + 10
        while not connection_file.is_file():
            if bridge_process.poll() is not None:
                return bridge_process.returncode or 1
            if time.monotonic() > deadline:
                raise RuntimeError("Telemetry bridge did not create its connection file within 10 seconds.")
            time.sleep(0.05)
        label = f"Live input: {args.device}" if args.device else f"Simulated input: {speed_kmh:g} km/h"
        print(f"{label}. Ctrl-C ends the bridge session.", flush=True)
        if args.bridge_only:
            print("In Unity: Walkpadead > Open prototype, then press Play.", flush=True)
            return bridge_process.wait()
        environment = os.environ.copy()
        environment["WALKPADEAD_CONNECTION_FILE"] = str(connection_file)
        command = [str(executable.resolve())]
        if args.editor:
            command += ["-projectPath", str(ROOT), "-executeMethod", "Walkpadead.Editor.ProjectSetup.OpenPrototype"]
        else:
            command += ["--connection-file", str(connection_file)]
        game_process = subprocess.Popen(command, cwd=ROOT, env=environment)
        while game_process.poll() is None:
            if bridge_process.poll() is not None:
                print("Telemetry bridge ended; the game will pause. Close the game to end the launcher.", flush=True)
                return game_process.wait()
            time.sleep(0.2)
        return game_process.returncode
    except KeyboardInterrupt:
        return 0
    finally:
        # Ctrl-C is handled by Rust and permits normal credential-file cleanup.
        if bridge_process is not None and bridge_process.poll() is None:
            import signal
            bridge_process.send_signal(signal.SIGINT)
            try:
                bridge_process.wait(timeout=30 if args.device else 4)
            except subprocess.TimeoutExpired:
                stop(bridge_process)
        stop(game_process)
        if directory is not None:
            shutil.rmtree(directory)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
