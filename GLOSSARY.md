# 术语表

- **电量来源(BatterySource)**:一个设备电量可被读取的途径。V1 有两种:`HidBattery`(HID Usage Page `0x85`)与 `GattBattery`(GATT Battery Service `0x180F`,特征 `0x2A19`)。V2 增加 `HidppBattery`(罗技 HID++,feature report `0x10`/`0x11`)。
- **设备条目(DeviceEntry)**:UI 展示单元 = 设备名 + 连接类型(经典蓝牙/BLE/2.4G 接收器)+ 电量百分比或"不支持"。
- **快照(Dump)**:`--dump` 模式打印的全量设备+电量文本,是跨 WSL/Windows 的验证闭环载体。
- **托盘颜色规则**:取全部已连接可读设备的最低电量;>40% 绿,20–40% 黄,<20% 红;无可读设备时灰/空心。
- **电量档位(BatteryBand)**:百分比到呈现档位的纯函数映射(Good >40 / Warn 21–40 / Low ≤20 / None 未知),托盘图标、设备面板电量条、设置主按钮强调色共用同一阈值(见 `TrayRules.BandOf`)。
