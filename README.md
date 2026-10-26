# BatteryTray

Windows 系统托盘应用:实时显示已连接蓝牙 / 2.4G 外设的电量。托盘单图标颜色跟随最低电量,左键弹出设备面板,低于阈值弹 Windows 通知。

![状态](https://img.shields.io/badge/status-usable-green) ![tests](https://img.shields.io/badge/tests-75%20passed-brightgreen) ![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)

## 支持的读取路径

| 路径 | 覆盖设备 | 状态 |
|---|---|---|
| AEP 属性缓存(`System.Devices.BatteryLife`)| 蓝牙设备(设置页里显示的电量的来源) | ✅ 实测 |
| HFP 私有属性(CfgMgr32,BTHENUM 注册表定位)| 经典蓝牙耳机(HFP,精度 10%——协议限制) | ✅ 实测(HUAWEI FreeClip) |
| GATT Battery Service `0x180F` 实时读 | BLE 设备 | ✅ 实现,待硬件验证 |
| HID 电池页 `0x85` 输入报告 | 蓝牙键鼠、通用 2.4G 接收器 | ✅ 实现,待硬件验证 |
| 罗技 HID++(2.0 feature 协商 + 1.0 RAP 回退)| Unifying / Bolt / Lightspeed 接收器 | ✅ 实现,待硬件校准 |

> 纯 HFP 协议的耳机电量 Windows 没有公开 API,本项目通过 BTHENUM 注册表定位 + CfgMgr32 私有属性 `DEVPKEY {104EA319-…} pid 2` 读取 Windows 蓝牙栈缓存的值。

## 功能

- 单托盘图标:绿 >40% / 黄 20–40% / 红 <20%(取全部已连接可读设备的最低值);无可读设备时空心灰
- 左键弹出设备面板:名称 / 连接类型与状态点 / 彩色电量条,跟随系统深浅色,Win11 圆角+投影;头部显示上次扫描时间
- 低电量 Windows 通知:首读即低或跌破阈值弹一次,持续低位不重复,回升再跌会再弹
- 60s 轮询(可配 10–600s)+ DeviceWatcher 设备增删事件即时刷新(2s 去抖)+ 睡眠唤醒自动刷新
- 设置:轮询间隔 / 提醒阈值 / 通知开关 / 开机自启(`HKCU\…\Run`,免管理员);TableLayout 布局,深浅色跟随
- 单实例保护;日志 `%LOCALAPPDATA%\BatteryTray\log.txt`(256KB 自动轮转)
- `BatteryTray.exe --dump`:打印全部设备 + 电量快照与协议诊断行,是新增设备类型的校准工具
- `BatteryTray.exe --ui-preview <目录> [dark|light]`:把设备面板与设置对话框渲染成 PNG,无硬件也能验证界面

## 开发(WSL → Windows 交叉编译)

```bash
mise exec -- dotnet test tests/BatteryTray.Tests   # 75 个单测,纯逻辑在 WSL 可跑
./deploy.sh                                        # 编译 + 单文件发布 + 部署到 Windows
./deploy.sh 'D:\Tools\BatteryTray'                # 或显式指定部署目标
```

- 部署目标解析:命令行参数 > `deploy.target` 文件(本地偏好,已 gitignore)> `%USERPROFILE%\Downloads\BatteryTray`
- `deploy.sh` 用 `powershell.exe` interop 把产物从 `\\wsl$` 拉到 Windows(WSL 对 `C:\Users` 无 ACL 权限,直接拷会被拒);路径经 `WSLENV=PS_DEST/w` 传入
- 技术栈:C# / .NET 8 / WinForms,TFM `net8.0-windows10.0.19041.0`(WinRT 投影),`EnableWindowsTargeting` 允许 Linux 上构建
- 结构:`src/BatteryTray.Core` 纯逻辑层(协议规则/合并/阈值,全部可单测)+ `src/BatteryTray` WinForms/WinRT 层

## 文档

- [SPEC.md](SPEC.md) — 决策记录与技术路线
- [GLOSSARY.md](GLOSSARY.md) — 术语
- [.scratch/issues/](.scratch/issues/) — 实施工单(含实物验证清单)
