#!/bin/bash
# Steam Workshop Upload Script for DuckovHaptics

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
RELEASE_DIR="$PROJECT_DIR/release"
VDF_FILE="$SCRIPT_DIR/workshop_item.vdf"

echo "=== DuckovHaptics Workshop Upload ==="
echo ""

# Check for required files
if [ ! -f "$RELEASE_DIR/DuckovHaptics.dll" ]; then
    echo "ERROR: DuckovHaptics.dll not found in release folder!"
    echo "Please build the project first:"
    echo "  dotnet build -c Release"
    echo "  cp bin/Release/netstandard2.1/DuckovHaptics.dll ../release/"
    exit 1
fi

if [ ! -f "$SCRIPT_DIR/preview.png" ]; then
    echo "ERROR: preview.png not found in workshop folder!"
    echo "Please add a 512x512 or 1920x1080 preview image."
    exit 1
fi

# Ensure info.ini is in release folder
cp "$PROJECT_DIR/info.ini" "$RELEASE_DIR/" 2>/dev/null || true

echo "Content folder: $RELEASE_DIR"
echo "Files to upload:"
ls -la "$RELEASE_DIR"
echo ""

# Prompt for Steam credentials
read -p "Steam Username: " STEAM_USER
read -s -p "Steam Password: " STEAM_PASS
echo ""

echo ""
echo "Starting SteamCMD upload..."
echo "You may need to enter a Steam Guard code."
echo ""

# Run SteamCMD
steamcmd +login "$STEAM_USER" "$STEAM_PASS" \
    +workshop_build_item "$VDF_FILE" \
    +quit

echo ""
echo "=== Upload Complete ==="
echo "Check the Steam Workshop for your item!"
