#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -x .tools/dotnet/dotnet ]]; then
    dotnet_executable="$PWD/.tools/dotnet/dotnet"
else
    dotnet_executable="$(command -v dotnet)"
fi
export DOTNET_CLI_HOME="$PWD/.tools/dotnet-home"
export NUGET_PACKAGES="$PWD/.tools/nuget"
export DOTNET_NOLOGO=1
"$dotnet_executable" run --project tests/Walkpadead.Checks -- "$@"
