#!/usr/bin/env python3
"""Install/run the Quest APK and forward the local WalkingPad bridge through ADB."""
import argparse
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
import tempfile
import time
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parents[1]
APP = "dev.walkpadead.quest"
REMOTE_CONNECTION = "files/walkingpad-connection.json"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", type=Path, help="Override the Android adb executable")
    parser.add_argument("--serial", help="Select one authorized Quest when multiple devices are connected")
    parser.add_argument("--devices", action="store_true", help="List devices without launching anything")
    parser.add_argument("--install-only", action="store_true")
    parser.add_argument("--skip-install", action="store_true", help="Use the APK already installed on the headset")
    parser.add_argument("--apk", type=Path, default=ROOT / "Builds/Quest/Walkpadead.apk")
    parser.add_argument("--bridge", type=Path)
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--device", help="Use a real WalkingPad, e.g. KS-NACH-X21C; otherwise use simulation")
    modes.add_argument("--simulate", action="store_true")
    parser.add_argument("--speed-kmh", type=float, default=3)
    parser.add_argument("--allow-speed-control", action="store_true", help="Opt in to ±0.1 bonuses within 1.0–5.0 km/h")
    args = parser.parse_args()
    if not 0 <= args.speed_kmh <= 5:
        parser.error("Simulation speed must be between 0 and 5 km/h.")
    bundled = ROOT / ".tools/Unity.app/Contents/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
    adb = args.adb or (bundled if bundled.is_file() else shutil.which("adb"))
    if not adb:
        parser.error("Install Android Build Support or pass --adb /path/to/adb.")
    # The native USB backend in platform-tools 37 can stall during enumeration on macOS.
    # Google's alternative libusb backend completed enumeration on this development Mac.
    if sys.platform == "darwin":
        os.environ.setdefault("ADB_LIBUSB", "1")
    devices = subprocess.run([str(adb), "devices", "-l"], check=True, capture_output=True, text=True, timeout=20).stdout
    if args.devices:
        print(devices, end="")
        return 0
    authorized = [line.split()[0] for line in devices.splitlines()[1:] if len(line.split()) > 1 and line.split()[1] == "device"]
    if args.serial and args.serial not in authorized:
        parser.error("The selected headset is not authorized. Enable Developer Mode and accept USB debugging in the headset.")
    if not args.serial and len(authorized) != 1:
        parser.error("Connect one Quest by USB, enable Developer Mode, and accept USB debugging. Use --devices to inspect it.")
    serial = args.serial or authorized[0]
    prefix = [str(adb), "-s", serial]

    def run(*command, **kwargs):
        return subprocess.run(prefix + list(command), check=True, **kwargs)

    if not args.skip_install:
        if not args.apk.is_file():
            parser.error("Build the APK first: Unity menu Walkpadead > Build Quest APK.")
        run("install", "-r", str(args.apk.resolve()))
    if args.install_only:
        print("Installed. Open Walkpadead from Unknown Sources; choose Use offline simulator to play without a bridge.")
        return 0
    sdk = ROOT.parent / "walkingpad-sdk"
    candidates = [sdk / "dist/WalkingPad SDK.app/Contents/MacOS/walkingpad", sdk / "target/debug/walkingpad"]
    bridge = args.bridge or next((p for p in candidates if p.is_file()), None)
    if not bridge or not bridge.is_file():
        parser.error("Build walkingpad-sdk first or pass --bridge /path/to/walkingpad.")
    runtime = ROOT / ".runtime"
    runtime.mkdir(mode=0o700, exist_ok=True)
    process = None
    port = None
    with tempfile.TemporaryDirectory(prefix="quest-", dir=runtime) as directory:
        connection = Path(directory) / "connection.json"
        command = [str(bridge.resolve()), "bridge", "--seconds", "3600", "--connection-file", str(connection)]
        command += ["--device", args.device] if args.device else ["--simulate", "--speed-kmh", str(args.speed_kmh)]
        if args.allow_speed_control:
            command += ["--allow-speed-control", "--min-speed-kmh", "1.0", "--max-speed-kmh", "5.0"]
        try:
            process = subprocess.Popen(command, cwd=ROOT)
            deadline = time.monotonic() + 15
            while not connection.is_file():
                if process.poll() is not None:
                    raise RuntimeError("The WalkingPad bridge exited before creating its connection file.")
                if time.monotonic() > deadline:
                    raise RuntimeError("The WalkingPad bridge did not become ready within 15 seconds.")
                time.sleep(0.05)
            data = connection.read_bytes()
            url = urlparse(json.loads(data)["url"])
            if url.scheme != "ws" or url.hostname != "127.0.0.1" or not url.port:
                raise RuntimeError("Expected the bridge's authenticated loopback WebSocket URL.")
            port = str(url.port)
            # The Quest keeps the exact loopback URL/token. No LAN listener is opened.
            run("reverse", "tcp:" + port, "tcp:" + port)
            run("shell", "am", "force-stop", APP, stdout=subprocess.DEVNULL)
            run("shell", "run-as", APP, "mkdir", "-p", "files")
            # tee receives credentials through stdin; discard its echoed output.
            run("shell", "-T", "run-as", APP, "tee", REMOTE_CONNECTION, input=data, stdout=subprocess.DEVNULL)
            run("shell", "run-as", APP, "chmod", "600", REMOTE_CONNECTION)
            launch = run("shell", "am", "start", "-n", APP + "/com.unity3d.player.UnityPlayerGameActivity",
                         "-a", "android.intent.action.MAIN", "-c", "com.oculus.intent.category.VR",
                         capture_output=True, text=True)
            if "Error:" in launch.stdout or "Error:" in launch.stderr:
                raise RuntimeError("Android could not launch Walkpadead: " + launch.stdout + launch.stderr)
            print("Quest launch requested with " + ("live WalkingPad input" if args.device else f"simulated input at {args.speed_kmh:g} km/h") +
                  ". Put on the headset; an asleep Quest defers VR launches. "
                  "Keep the USB connection and this launcher open. Ctrl-C ends the bridge.", flush=True)
            return process.wait()
        except KeyboardInterrupt:
            return 0
        finally:
            if process and process.poll() is None:
                process.send_signal(signal.SIGINT)
                try:
                    process.wait(timeout=30 if args.device else 4)
                except subprocess.TimeoutExpired:
                    process.terminate()
                    try:
                        process.wait(timeout=4)
                    except subprocess.TimeoutExpired:
                        process.kill(); process.wait()
            # A disconnected headset can reject cleanup; do not hide the original error.
            cleanup = [["shell", "run-as", APP, "rm", "-f", REMOTE_CONNECTION]]
            if port:
                cleanup.append(["reverse", "--remove", "tcp:" + port])
            for command in cleanup:
                try:
                    subprocess.run(prefix + command, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   timeout=5, check=False)
                except (OSError, subprocess.SubprocessError):
                    pass


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
