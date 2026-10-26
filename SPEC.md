# BatteryTray — 规格

Windows 系统托盘应用:显示已连接外设(蓝牙 / 2.4G 接收器)的电量。

## 决策记录(2026-10-26 与用户确认)

| 决策 | 结论 |
|---|---|
| 设备范围 V1 | HID 电池页 `0x85`(经典蓝牙键鼠 + 部分 2.4G 接收器)+ GATT `0x180F`(BLE) |
| 设备范围 V2 | 罗技 HID++(Unifying/Bolt/Lightspeed)。用户无接收器,**尽力而为,无法本地验证** |
| HFP 耳机 | ⚠️ Windows 无公开 API 读纯 HFP 电量(精度仅 10%)。若耳机同时暴露 GATT 电池服务则可读,否则显示"不支持"——平台限制 |
| 技术栈 | C# / .NET 8 / WinForms |
| 构建 | WSL 交叉编译(`EnableWindowsTargeting` + `mise exec -- dotnet`),win-x64 自包含单文件 |
| 部署 | `deploy.sh` → powershell.exe 从 `\\wsl.localhost\archlinux\...` 拷到 `%USERPROFILE%\Desktop\BatteryTray\`(WSL 对 C:\Users 无 ACL 权限,必须走 interop) |
| 托盘形态 | 单图标;颜色=全部设备中最低电量(绿>40% / 黄 20–40% / 红<20%);tooltip 列全部;左键弹出面板(名称/连接类型/电量条);右键菜单 |
| 提醒 | 任一设备 <20% → Windows toast + 图标红;默认开,阈值与开关可配 |
| 刷新 | 60s 轮询(可配)+ DeviceWatcher 设备增删即时刷新 |
| 自启 | 写 `HKCU\...\Run`,默认关 |
| 测试策略 | 纯逻辑(协议解码/聚合/阈值)WSL 单测;WinRT/蓝牙部分靠 `--dump` 模式在 Windows 真机验证(输出设备+电量快照) |

## 用户实际设备(测试优先级)

1. 经典蓝牙键鼠 → HID `0x85`
2. BLE 低功耗设备 → GATT `0x180F`
3. 蓝牙耳机 → GATT(若支持),否则"不支持"

## 实施顺序(见 .scratch/issues/)

骨架 → 设备枚举 → HID 0x85 → GATT → 图标/提醒 → 设置/自启 → HID++ → 打磨
