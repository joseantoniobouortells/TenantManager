#!/usr/bin/env bash
# bundle-mac.sh — Build and package TenantManager as a macOS .app bundle
# Usage: ./bundle-mac.sh [Configuration] [Runtime]
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-osx-arm64}"      # Accept osx-x64 or osx-arm64 as second argument
PROJECT="src/TenantManager.App/TenantManager.App.csproj"
PUBLISH_DIR="publish/mac"
APP_NAME="TenantManager"
APP_BUNDLE="dist/${APP_NAME}.app"
ZIP_NAME="dist/${APP_NAME}-${RUNTIME}.zip"

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

echo "▶ Packaging as DMG..."
DMG_NAME="dist/${APP_NAME}-${RUNTIME}.dmg"
TEMP_DMG="dist/temp.dmg"

rm -f "$DMG_NAME" "$TEMP_DMG"
# Create a temporary empty DMG (e.g. 250MB should be plenty for the self-contained .NET app)
hdiutil create -size 250m -fs HFS+ -volname "Tenant Manager" "$TEMP_DMG"

# Mount it and get the device name
DEVICE=$(hdiutil attach -noverify -noautoopen "$TEMP_DMG" | egrep '^/dev/' | sed 1q | awk '{print $1}')

# Copy the .app into the mounted DMG and create the Applications symlink
cp -R "$APP_BUNDLE" "/Volumes/Tenant Manager/"
ln -s /Applications "/Volumes/Tenant Manager/Applications"

# Detach (unmount)
hdiutil detach "$DEVICE"

# Compress into the final DMG
hdiutil convert "$TEMP_DMG" -format UDZO -o "$DMG_NAME"
rm -f "$TEMP_DMG"

echo ""
echo "✅ Bundle created at: ${APP_BUNDLE}"
echo "📦 DMG generated at: ${DMG_NAME}"
echo ""
echo "To run:  open ${APP_BUNDLE}"
echo "To install: cp -r ${APP_BUNDLE} /Applications/"
