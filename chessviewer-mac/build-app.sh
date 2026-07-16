#!/bin/bash
# Packages ChessViewer as a double-clickable macOS .app bundle (ChessViewer.app in the
# repo root). Run this after code changes to refresh the bundle; `dotnet run`/`dotnet build`
# alone do not produce or update it.
set -euo pipefail
cd "$(dirname "$0")"

RID="osx-arm64"
APP="ChessViewer.app"
PUBLISH_DIR="bin/Release/net8.0/$RID/publish"

# Self-contained: a framework-dependent apphost relies on $PATH/DOTNET_ROOT to locate the
# .NET runtime, which Finder-launched processes don't inherit (they get a bare LaunchServices
# environment, not your shell's). That made the bundle crash within ~10ms of every real
# double-click while `open`/`dotnet run` from a terminal worked fine. Bundling the runtime
# removes the dependency on any environment variable being set at launch time.
dotnet publish ChessViewer.Mac.csproj -c Release -r "$RID" --self-contained true -o "$PUBLISH_DIR"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
cp Assets/AppIcon/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
chmod +x "$APP/Contents/MacOS/ChessViewer"

cat > "$APP/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>Chess Viewer</string>
    <key>CFBundleDisplayName</key>
    <string>Chess Viewer</string>
    <key>CFBundleIdentifier</key>
    <string>com.wernerschoegler.chessviewer</string>
    <key>CFBundleVersion</key>
    <string>0.1</string>
    <key>CFBundleShortVersionString</key>
    <string>0.1</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleExecutable</key>
    <string>ChessViewer</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

# dotnet publish already ad-hoc signs the raw executable, but that seal only covers the
# lone binary. Once Info.plist/Resources are added around it, the signature no longer
# matches the bundle's contents and Gatekeeper silently refuses to launch it on a real
# Finder double-click (spctl: "code has no resources but signature indicates they must be
# present"). Re-sign the assembled bundle as a whole so the seal covers everything.
codesign --force --deep --sign - "$APP"

# Nudge Finder/Dock to drop any cached icon for a stale bundle at this path.
touch "$APP"

echo "Built $APP"
