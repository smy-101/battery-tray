# 0003 经典蓝牙电量:HFP CfgMgr32(耳机)

**状态**:HFP 部分已完成(待 FreeClip 真机验证);HID 0x85 兜底等键鼠设备到手再做

背景:用户当前只有 HUAWEI FreeClip(经典蓝牙 HFP 耳机)可测。AEP 属性
(`System.Devices.BatteryLife`)对它没有值 → 走参考实现逆向出的私有路径:

1. `BluetoothDevice.FromIdAsync(aepId)` → `BluetoothAddress` → 12 位大写十六进制
2. 注册表 `HKLM\SYSTEM\CurrentControlSet\Enum\BTHENUM` 下找 HFP 服务子键(GUID 含 `111e`)与含地址的实例
3. `CM_Locate_DevNodeW` + `CM_Get_DevNode_PropertyW` 读私有属性 `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} pid 2`
4. 精度 10%(HFP 协议本身限制)

分层:
- `HfpRules`(Core,纯函数):服务键匹配 / 地址匹配 / 实例 id 拼接 —— 9 个单测(合计 15 全绿)
- `HfpBatteryReader`(app):CfgMgr32 P/Invoke,同步,调用方放线程池
- `BluetoothScanner.FillClassicHfpBatteryAsync`:已连接 + 经典 + AEP 无电量 → 补读,来源标 `HfpDevProp`

- [x] HfpRules 纯逻辑 + 单测
- [x] CfgMgr32 读取器
- [x] 扫描管线接入(逐台 try/catch,单台失败不影响其他)
- [ ] FreeClip 验证:dump.bat 显示电量(10% 精度)+ 面板展示 + 托盘 tooltip
