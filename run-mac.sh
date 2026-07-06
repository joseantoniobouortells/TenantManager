#!/bin/bash
set -e

echo "Compilando aplicación..."
dotnet publish src/TenantManager.App/TenantManager.App.csproj -c Debug -r osx-arm64 --self-contained false -o bin/mac-bundle -v q

echo "Creando paquete de aplicación macOS (TenantManager.app)..."
APP_DIR="bin/TenantManager.app"
mkdir -p "$APP_DIR/Contents/MacOS"
mkdir -p "$APP_DIR/Contents/Resources"

# Copiar binarios
cp -R bin/mac-bundle/* "$APP_DIR/Contents/MacOS/"

# Copiar icono
cp src/TenantManager.App/Assets/app-icon.icns "$APP_DIR/Contents/Resources/"

# Crear Info.plist
cat > "$APP_DIR/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.tenantmanager.app</string>
    <key>CFBundleName</key>
    <string>TenantManager</string>
    <key>CFBundleExecutable</key>
    <string>TenantManager.App</string>
    <key>CFBundleIconFile</key>
    <string>app-icon.icns</string>
    <key>CFBundleVersion</key>
    <string>1.0.2</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.2</string>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSUserNotificationAlertStyle</key>
    <string>alert</string>
</dict>
</plist>
EOF

echo "Firmando binarios (Ad-hoc) para macOS..."
xattr -cr "$APP_DIR"
codesign --force --deep --sign - "$APP_DIR"

echo "Abriendo TenantManager.app..."
open "$APP_DIR"
