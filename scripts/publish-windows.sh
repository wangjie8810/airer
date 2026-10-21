#!/bin/bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
dotnet="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if [[ ! -x "$dotnet" ]]; then
  dotnet="dotnet"
fi
version="$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$root/src/Airer/Airer.csproj" | head -n 1)"
if [[ -z "$version" ]]; then
  echo "Could not read Version from Airer.csproj" >&2
  exit 1
fi

"$dotnet" publish "$root/src/Airer/Airer.csproj" -c Release -r win-x64 --self-contained true -p:Version="$version" -o "$root/dist/win-x64"
"$dotnet" publish "$root/src/Airer/Airer.csproj" -c Release -r win-arm64 --self-contained true -p:Version="$version" -o "$root/dist/win-arm64"

makensis="$(command -v makensis || true)"
if [[ -z "$makensis" && -x /opt/homebrew/bin/makensis ]]; then
  makensis=/opt/homebrew/bin/makensis
fi
if [[ -z "$makensis" ]]; then
  echo "makensis was not found. Published folders are in dist/, but the setup exe was not built." >&2
  echo "Install NSIS (brew install makensis) and run this script again." >&2
  exit 1
fi

build_setup() {
  local arch="$1"
  local src="$root/dist/win-${arch}"
  local out="$root/dist/Airer-Setup-win-${arch}.exe"
  "$makensis" \
    -DSRCDIR="$src" \
    -DOUTFILE="$out" \
    -DARCH="$arch" \
    -DVERSION="$version" \
    "$root/installer/airer.nsi"
  echo "  $out"
}

build_setup x64
build_setup arm64
echo "Windows 10/11 builds ${version}:"
echo "  $root/dist/win-x64/Airer.exe"
echo "  $root/dist/win-arm64/Airer.exe"
