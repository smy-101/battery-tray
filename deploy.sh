#!/usr/bin/env bash
# 构建 + 部署到 Windows 桌面(WinExe 无法从 WSL 直接写 C:,走 powershell.exe 拉取)
set -euo pipefail
cd "$(dirname "$0")"

mise exec -- dotnet publish src/BatteryTray -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -o publish

powershell.exe -NoProfile -Command "
\$dest = \"\$env:USERPROFILE\Desktop\BatteryTray\"
New-Item -ItemType Directory -Force -Path \$dest | Out-Null
Copy-Item '\\\\wsl.localhost\\archlinux\\home\\smy-101\\workspace\\battery-tray\\publish\\BatteryTray.exe' \$dest -Force
Write-Host \"deployed -> \$dest\"
"
