#!/bin/bash
# Developer: heaplyn
# Date: 2026-08-09
# Summary: Downloads and installs FlareSolverr Windows x64 binary into the local flaresolverr/ directory.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TARGET_DIR="$SCRIPT_DIR/flaresolverr"
ZIP_FILE="$SCRIPT_DIR/flaresolverr.zip"
URL="https://github.com/FlareSolverr/FlareSolverr/releases/download/v3.3.21/flaresolverr_windows_x64.zip"

echo "⬇️ Downloading FlareSolverr v3.3.21 for Windows..."
curl -L -o "$ZIP_FILE" "$URL"

echo "📦 Extracting Zip contents..."
mkdir -p "$TARGET_DIR"
unzip -o -q "$ZIP_FILE" -d "$SCRIPT_DIR"

# Handle extraction renaming
EXTRACTED_DIR="$SCRIPT_DIR/flaresolverr_windows_x64"
if [ -d "$EXTRACTED_DIR" ]; then
    echo "🚚 Moving files to target directory..."
    cp -r "$EXTRACTED_DIR"/* "$TARGET_DIR/"
    rm -rf "$EXTRACTED_DIR"
fi

echo "🧹 Cleaning up temp zip file..."
rm -f "$ZIP_FILE"

echo "✅ FlareSolverr installed successfully in: $TARGET_DIR"
