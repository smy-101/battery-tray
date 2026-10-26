# 0001 骨架:项目+托盘图标+交叉编译流水线

**状态**:已完成(待 Windows 真机验证)

- [x] mise.toml 固定 dotnet 8.0.427(WSL shim 解析失败时用 `mise exec --`)
- [x] csproj:`net8.0-windows` + `EnableWindowsTargeting` + 单文件自包含
- [x] Program.cs:`--dump` 分支(AttachConsole 父进程,WinExe 无控制台)
- [x] TrayApp:NotifyIcon + 运行时绘制电池图标 + 右键退出
- [x] deploy.sh:publish → powershell.exe 从 \\wsl.localhost 拉到桌面
- [ ] 用户在 Windows 验证:托盘图标出现(绿色电池)、右键可退出、dump.bat 有输出

**验证路径**:`\\wsl 侧 ./deploy.sh; Windows 侧双击 Desktop\BatteryTray\BatteryTray.exe`
