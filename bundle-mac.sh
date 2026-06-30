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
DMG_STAGING="dist/dmg_staging"

rm -rf "$DMG_STAGING"
mkdir -p "$DMG_STAGING"
mv "$APP_BUNDLE" "$DMG_STAGING/"
ln -s /Applications "$DMG_STAGING/Applications"

hdiutil create -volname "Tenant Manager" -srcfolder "$DMG_STAGING" -ov -format UDZO "$DMG_NAME"

echo ""
echo "✅ Bundle created at: ${DMG_STAGING}/${APP_NAME}.app"
echo "📦 DMG generated at: ${DMG_NAME}"
echo ""
echo "To run:  open ${APP_BUNDLE}"
echo "To install: cp -r ${APP_BUNDLE} /Applications/"
