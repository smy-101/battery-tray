# 0002 设备枚举→面板列表

**状态**:已完成(待 Windows 真机验证)

架构(参考 peterandree & JPITSG 同类项目逆向出的路径):

- `src/BatteryTray.Core`(net8.0 纯逻辑,WSL 可测):DeviceEntry / DeviceCatalog(去重+排序)
- `src/BatteryTray`:BluetoothScanner(AEP 枚举 + DeviceWatcher)、DevicePanel(左键面板)、TrayApp(60s 轮询 + 2s 去抖)
- TFM 升级 `net8.0-windows10.0.19041.0`(WinRT 投影必需)

关键事实(来自参考实现):
- 枚举 AssociationEndpoint,selector = `BluetoothDevice/BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)`
- 枚举时**只能请求文档化属性**(IsConnected/IsPaired/BatteryLife),乱请求整包被拒
- 电量第一来源:属性缓存 `System.Devices.BatteryLife`(Settings 里那个数);后续 ticket 再上 HFP(CfgMgr32)与 GATT 实时读
- NotifyIcon.Text 上限 63 字符

- [x] DeviceCatalog 单测 6 个全绿(WSL)
- [x] AEP 扫描:经典 + LE,含断连设备(灰显)
- [x] 左键面板(无边框、置顶、Esc/失焦关闭、贴托盘定位)
- [x] watcher 事件 2s 去抖 → 即时刷新;60s 轮询兜底
- [x] --dump 打印设备表(名称/类型/连接/电量/来源)
- [ ] 用户 Windows 验证:dump.bat 列出全部已配对蓝牙设备;面板展示;断开一台设备后 ~几秒内列表变化
