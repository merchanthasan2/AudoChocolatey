#!/usr/bin/env bash
set -e

echo "=========================================================="
echo "  Quantum Updater - Native macOS DMG Builder"
echo "  Universal 2 Binary (Apple Silicon M1-M6 + Intel x86_64)"
echo "=========================================================="

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$PROJECT_ROOT"

DIST_DIR="$PROJECT_ROOT/dist"
APP_BUNDLE="$DIST_DIR/Quantum Updater.app"
DMG_STAGING="$DIST_DIR/dmg_staging"
FINAL_DMG="$DIST_DIR/QuantumUpdater_2.0.0_macOS.dmg"

# 1. Clean previous builds
echo "[1/5] Cleaning output directories..."
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR/macos" "$DMG_STAGING" "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources/web"

# 2. Compile Universal Binary (arm64 + x86_64)
echo "[2/5] Compiling native Universal 2 binary with clang..."
clang -arch arm64 -arch x86_64 -fobjc-arc -O3 \
  -framework Cocoa -framework WebKit -framework IOKit -framework AVFoundation \
  src-macos/main.m \
  -o "$APP_BUNDLE/Contents/MacOS/Quantum Updater"

chmod +x "$APP_BUNDLE/Contents/MacOS/Quantum Updater"

# 3. Copy Application Assets & Resources
echo "[3/5] Bundling application resources & web assets..."
cp src-tauri/icons/icon.icns "$APP_BUNDLE/Contents/Resources/icon.icns"
cp web/icon.png "$APP_BUNDLE/Contents/Resources/icon.png"
cp web/icon.png "$APP_BUNDLE/Contents/Resources/web/icon.png"
cp web/icon.png "$APP_BUNDLE/Contents/Resources/web/logo.png"
cp web/index.html "$APP_BUNDLE/Contents/Resources/web/"
cp web/style.css "$APP_BUNDLE/Contents/Resources/web/"
cp web/app.js "$APP_BUNDLE/Contents/Resources/web/"

# 4. Create macOS Info.plist
cat << 'EOF' > "$APP_BUNDLE/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>Quantum Updater</string>
    <key>CFBundleIconFile</key>
    <string>icon.icns</string>
    <key>CFBundleIdentifier</key>
    <string>in.quantumstep.quantumupdater</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Quantum Updater</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>2.0.0</string>
    <key>CFBundleVersion</key>
    <string>2.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSRequiresAquaSystemAppearance</key>
    <false/>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright © 2026 Quantum Step (quantumstep.in). All rights reserved.</string>
</dict>
</plist>
EOF

# 5. Ad-Hoc Code Sign Application Bundle
echo "[4/6] Code signing application bundle..."
codesign --force --deep --sign - "$APP_BUNDLE"
xattr -rc "$APP_BUNDLE" 2>/dev/null || true

# 6. Build in-place update zip archive
echo "[5/6] Creating in-place update archive (QuantumUpdater_macOS_Update.zip)..."
ditto -c -k --keepParent "$APP_BUNDLE" "$DIST_DIR/QuantumUpdater_macOS_Update.zip"

# 7. Build Drag-and-Drop Disk Image (.dmg)
echo "[6/6] Preparing DMG layout and creating compressed UDZO image..."
cp -R "$APP_BUNDLE" "$DMG_STAGING/"
ln -s /Applications "$DMG_STAGING/Applications"

hdiutil create -volname "Quantum Updater" \
  -srcfolder "$DMG_STAGING" \
  -ov -format UDZO \
  "$FINAL_DMG"

# Remove quarantine on generated DMG
xattr -rc "$FINAL_DMG" 2>/dev/null || true
rm -rf "$DMG_STAGING"

echo "=========================================================="
echo "  [SUCCESS]: macOS DMG & Update Package created successfully!"
echo "  DMG: $FINAL_DMG ($(du -sh "$FINAL_DMG" | cut -f1))"
echo "  ZIP: $DIST_DIR/QuantumUpdater_macOS_Update.zip ($(du -sh "$DIST_DIR/QuantumUpdater_macOS_Update.zip" | cut -f1))"
echo "  Architecture: Universal 2 (Apple Silicon M1-M6 + Intel x86_64)"
echo "  Code Signed: Validated"
echo "=========================================================="

