#!/usr/bin/env bash
# 构建 + 部署到 Windows(WSL 对 C:\Users 无 ACL 权限,必须走 powershell.exe 从 \\wsl$ 拉取)
#
# 部署目标解析顺序:
#   1. 命令行参数:  ./deploy.sh 'D:\Tools\BatteryTray'
#   2. deploy.target 文件(本地偏好,已 gitignore,一行一个 Windows 路径)
#   3. 默认:%USERPROFILE%\Downloads\BatteryTray(Windows 侧当前用户)
set -euo pipefail
cd "$(dirname "$0")"

DEST="${1:-}"
if [[ -z "$DEST" && -f deploy.target ]]; then
  DEST="$(tr -d '\r\n' < deploy.target)"
fi
export PS_DEST="$DEST"
# WSL 环境变量默认不传给 Windows 进程,需 WSLENV 声明(/w = WSL→Windows 方向)
export WSLENV="PS_DEST/w${WSLENV:+:$WSLENV}"

mise exec -- dotnet publish src/BatteryTray -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -o publish

# dump.bat 随构建产物一起部署
printf '@echo off\r\ncd /d "%%~dp0"\r\nBatteryTray.exe --dump\r\npause\r\n' > publish/dump.bat

powershell.exe -NoProfile -Command '
$dest = "$env:PS_DEST"
if ([string]::IsNullOrWhiteSpace($dest)) { $dest = "$env:USERPROFILE\Downloads\BatteryTray" }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
$src = "\\wsl.localhost\archlinux\home\smy-101\workspace\battery-tray\publish"
Copy-Item "$src\BatteryTray.exe" $dest -Force
Copy-Item "$src\dump.bat" $dest -Force
Write-Host "deployed -> $dest"
Get-ChildItem $dest | Select-Object Name, Length | Format-Table -AutoSize | Out-String | Write-Host
'
