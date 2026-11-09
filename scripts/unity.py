#!/usr/bin/env python3
"""Prepare, test or build the Unity project in batch mode (close its GUI editor first)."""
import argparse
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["prepare", "test", "build"])
    parser.add_argument("--editor", type=Path, default=os.environ.get("UNITY_EDITOR",
                        ROOT / ".tools/Unity.app/Contents/MacOS/Unity"))
    args = parser.parse_args()
    if not args.editor.is_file():
        parser.error("Install Unity 6000.3.28f1, then pass --editor /path/to/Unity or set UNITY_EDITOR.")
    logs = ROOT / "Logs"
    logs.mkdir(exist_ok=True)
    common = [str(args.editor.resolve()), "-batchmode", "-projectPath", str(ROOT)]
    def execute(extra, log):
        print("Unity log:", logs / log, flush=True)
        return subprocess.run(common + extra + ["-logFile", str(logs / log)], cwd=ROOT).returncode
    if args.action == "test":
        result = execute(["-quit", "-nographics", "-executeMethod", "Walkpadead.Editor.ProjectSetup.Prepare"], "prepare.log")
        if result:
            return result
        output = ROOT / "TestResults"
        output.mkdir(exist_ok=True)
        return execute(["-runTests", "-testPlatform", "PlayMode", "-testResults", str(output / "playmode.xml")], "playmode.log")
    method = "BuildMac" if args.action == "build" else "Prepare"
    return execute(["-quit", "-executeMethod", "Walkpadead.Editor.ProjectSetup." + method], args.action + ".log")


if __name__ == "__main__":
    sys.exit(main())
