# 0009 界面打磨:设备面板/设置对话框/深浅色

**状态**:已完成(截图验证:浅色 + 深色;真机交互待用户确认)

## 改动

- `TrayRules.BandOf(byte?)` → `BatteryBand`(Good/Warn/Low/None,纯函数 + 7 单测):
  图标取色、面板电量条、设置主按钮强调色共用同一阈值(>40 绿 / 21–40 黄 / ≤20 红)
- 新增 `UiTheme`:HKCU `AppsUseLightTheme` 检测深色(失败回退浅色,可强制覆盖);
  浅/深两套调色板;DWM 深色标题栏、Win11 圆角封装;电量档位品牌色
- `DevicePanel` 重做:
  - 自绘 ListView 行:状态点(绿=已连接/灰=断连)+ 连接类型(`蓝牙/BLE/USB/2.4G · 已连接/断连`)
    + 胶囊电量条(档位配色,1% 也保证可见)+ 百分比;读不到显示 —;断连整行置灰
  - 头部:BatteryTray + “上次扫描 HH:mm:ss”
  - 无边框:Win11 圆角 + CS_DROPSHADOW 投影;宽度随最长设备名自适应(400–560),≤9 行
  - 修复过的坑:
    - ImageList 哑元行高:必须先落成 Handle 再释放哑元位图(否则 handle 创建时 ArgumentException → 整列表空白)
    - Details 视图 owner-draw 必须同时挂 DrawItem(行背景)与 DrawSubItem(单元格)
    - 空 SubItem 不触发 DrawSubItem:连接/电量列要加占位 subitem
- `SettingsDialog` 重做:TableLayoutPanel(抗 DPI/文字加宽)、深浅色主题、深色标题栏、
  主按钮品牌绿强调、AutoSize 表单、范围提示写入标签
- `--ui-preview <目录> [dark|light]`:示例设备渲染面板+设置对话框为 PNG(截屏取真实像素,
  因 ListView 自绘不响应 WM_PRINT/DrawToBitmap);异常写 `ui-error.txt` 而非弹窗

## 已验证(2026-10-26,Windows 实机 --ui-preview)

- [x] 浅色/深色面板:状态点、连接类型不截断(像素级确认 USB/2.4G 行文本止于 x≈297 < 列界 343)、
      电量条 85% 绿 / 40% 黄 / 15% 红(像素统计 45/52、20/52、8/52)、未知 —、断连置灰
- [x] 浅色/深色设置对话框:字段/复选框/按钮主题化,深色标题栏
- [x] `dotnet test`:82 通过(新增 BandOf 7 例)

## 待用户确认(真机交互)

- [ ] 托盘左键:面板在光标旁弹出、点击别处/Esc 关闭
- [ ] 深色模式跟随系统切换(面板与设置)
- [ ] 断连设备整行灰显、重新连接后恢复
