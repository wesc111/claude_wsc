#!/bin/bash
# Builds dist/Blackjack.app (Apple Silicon, self-contained) and dist/Blackjack-<version>.dmg.
# Uses only the .NET SDK and macOS tools (sips, iconutil, codesign, hdiutil).
set -euo pipefail

cd "$(dirname "$0")/.."
PROJECT=src/Blackjack.Gui/Blackjack.Gui.csproj
ICON=src/Blackjack.Gui/Assets/icon-1024.png
RID=osx-arm64
DIST=dist
APP="$DIST/Blackjack.app"
VERSION=$(dotnet msbuild "$PROJECT" -getProperty:Version)

echo "==> Publishing Blackjack $VERSION for $RID"
rm -rf "$DIST"
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained -o "$DIST/publish" -v quiet

echo "==> Building $APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$DIST/publish/." "$APP/Contents/MacOS/"

# Icon: all sizes macOS expects, then one .icns file.
ICONSET="$DIST/Blackjack.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$ICON" --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
    sips -z $((size * 2)) $((size * 2)) "$ICON" --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/Blackjack.icns"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                  <string>Blackjack</string>
    <key>CFBundleDisplayName</key>           <string>Blackjack</string>
    <key>CFBundleIdentifier</key>            <string>com.wesc111.blackjack</string>
    <key>CFBundleExecutable</key>            <string>BlackjackGui</string>
    <key>CFBundleIconFile</key>              <string>Blackjack</string>
    <key>CFBundlePackageType</key>           <string>APPL</string>
    <key>CFBundleShortVersionString</key>    <string>$VERSION</string>
    <key>CFBundleVersion</key>               <string>$VERSION</string>
    <key>LSApplicationCategoryType</key>     <string>public.app-category.card-games</string>
    <key>LSMinimumSystemVersion</key>        <string>13.0</string>
    <key>NSHighResolutionCapable</key>       <true/>
    <key>NSPrincipalClass</key>              <string>NSApplication</string>
</dict>
</plist>
PLIST

# Apple Silicon only runs signed code. Every file in Contents/MacOS (also the .NET .dll files) must be signed
# before the bundle itself; ad-hoc signatures ("-") are free and sufficient on this Mac.
echo "==> Signing (ad-hoc)"
find "$APP/Contents/MacOS" -type f -exec codesign --force --sign - {} \; 2> /dev/null
codesign --force --sign - "$APP"
codesign --verify --strict "$APP"

echo "==> Creating DMG"
STAGE="$DIST/dmg"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "Blackjack $VERSION" -srcfolder "$STAGE" -ov -format UDZO "$DIST/Blackjack-$VERSION.dmg" > /dev/null

rm -rf "$STAGE" "$ICONSET" "$DIST/publish"
echo "==> Done: $APP and $DIST/Blackjack-$VERSION.dmg"
