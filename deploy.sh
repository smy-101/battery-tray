#!/usr/bin/env bash
# 构建 + 部署到 Windows(WSL 对 C:\Users 无 ACL 权限,必须走 powershell.exe 从 \\wsl$ 拉取)
set -euo pipefail
cd "$(dirname "$0")"

mise exec -- dotnet publish src/BatteryTray -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -o publish

# dump.bat 随构建产物一起部署
printf '@echo off\r\ncd /d "%%~dp0"\r\nBatteryTray.exe --dump\r\npause\r\n' > publish/dump.bat

powershell.exe -NoProfile -Command "\
\$dest = 'C:\Users\Lenovo\Downloads\BatteryTray'; \
New-Item -ItemType Directory -Force -Path \$dest | Out-Null; \
Copy-Item '\\\\wsl.localhost\\archlinux\\home\\smy-101\\workspace\\battery-tray\\publish\\BatteryTray.exe' \$dest -Force; \
Copy-Item '\\\\wsl.localhost\\archlinux\\home\\smy-101\\workspace\\battery-tray\\publish\\dump.bat' \$dest -Force; \
Get-ChildItem \$dest | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host"
