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

echo "▶ Packaging as PKG..."
PKG_NAME="dist/${APP_NAME}-${RUNTIME}.pkg"

rm -f "$PKG_NAME"

# We must sign the app ad-hoc first to satisfy pkgbuild requirements
# and clean up detritus.
echo "▶ Removing detritus and signing ad-hoc..."
find "$APP_BUNDLE" -type f -name "._*" -delete
find "$APP_BUNDLE" -type f -name ".DS_Store" -delete
xattr -cr "$APP_BUNDLE"
codesign --force --deep --sign - "$APP_BUNDLE"

# pkgbuild requires a payload directory
PAYLOAD_DIR="dist/payload"
rm -rf "$PAYLOAD_DIR"
mkdir -p "$PAYLOAD_DIR"
cp -r "$APP_BUNDLE" "$PAYLOAD_DIR/"

pkgbuild --root "$PAYLOAD_DIR" \
         --identifier com.tenantmanager.app \
         --version 1.0.6 \
         --install-location /Applications \
         --scripts installer/macos/scripts \
         "$PKG_NAME"

rm -rf "$PAYLOAD_DIR"

echo ""
echo "✅ Bundle created at: ${APP_BUNDLE}"
echo "📦 PKG generated at: ${PKG_NAME}"
echo ""
echo "To run:  open ${APP_BUNDLE}"
echo "To install: cp -r ${APP_BUNDLE} /Applications/"
