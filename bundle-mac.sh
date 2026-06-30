#!/usr/bin/env bash
# bundle-mac.sh — Build and package TenantManager as a macOS .app bundle
# Usage: ./bundle-mac.sh [Release|Debug]
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME="osx-arm64"            # Change to osx-x64 for Intel Macs
PROJECT="src/TenantManager.App/TenantManager.App.csproj"
PUBLISH_DIR="publish/mac"
APP_NAME="TenantManager"
APP_BUNDLE="dist/${APP_NAME}.app"

echo "▶ Building (${CONFIGURATION}, ${RUNTIME})..."
dotnet publish "$PROJECT" \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$PUBLISH_DIR"

echo "▶ Creating .app bundle structure..."
rm -rf "$APP_BUNDLE"
mkdir -p "${APP_BUNDLE}/Contents/MacOS"
mkdir -p "${APP_BUNDLE}/Contents/Resources"

echo "▶ Copying binary and assets..."
cp -r "${PUBLISH_DIR}/"* "${APP_BUNDLE}/Contents/MacOS/"

echo "▶ Copying Info.plist..."
cp "assets/Info.plist" "${APP_BUNDLE}/Contents/Info.plist"

echo "▶ Copying icon..."
cp "assets/TenantManager.icns" "${APP_BUNDLE}/Contents/Resources/TenantManager.icns"

echo "▶ Setting executable permission..."
chmod +x "${APP_BUNDLE}/Contents/MacOS/${APP_NAME}.App"

echo ""
echo "✅ Bundle created at: ${APP_BUNDLE}"
echo ""
echo "To run:  open ${APP_BUNDLE}"
echo "To install: cp -r ${APP_BUNDLE} /Applications/"
