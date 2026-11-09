#!/bin/zsh
set -e
cd -- "${0:A:h}/.."
exec python3 scripts/play.py --simulate --allow-speed-control --min-speed-kmh 1 --max-speed-kmh 5
