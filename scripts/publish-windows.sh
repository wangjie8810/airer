#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if [[ ! -x "$dotnet" ]]; then
  dotnet="dotnet"
fi
"$dotnet" publish "$root/src/Airer/Airer.csproj" -c Release -r win-x64 --self-contained true -o "$root/dist/win-x64"
"$dotnet" publish "$root/src/Airer/Airer.csproj" -c Release -r win-arm64 --self-contained true -o "$root/dist/win-arm64"
echo "Windows 10/11 builds:"
echo "  $root/dist/win-x64/Airer.exe"
echo "  $root/dist/win-arm64/Airer.exe"
