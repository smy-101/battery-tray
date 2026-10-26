# 0005 托盘状态:图标变色 + tooltip + toast

**状态**:已完成(图标/tooltip 已验证路径;toast 由 9 个单测覆盖,等真实低电量触发)

- `TrayRules`(Core,纯函数,9 单测):
  - `MinBatteryPercent`:已连接可读设备取最低;无可读 → null
  - `NewlyLow(before, after, 20)`:首读即低 / 跌破阈值才弹;持续低位不重复;回升再跌再弹;断连不弹
- `BatteryIcon.Draw(level, known)`:known=false 画空心灰(无可读设备);绿>40 / 黄20–40 / 红<20
- `TrayApp`:每次扫描后重绘图标;`Icon.FromHandle` 的 HICON 用 `DestroyIcon` 正确销毁(修掉句柄泄漏)
- toast:`NotifyIcon.ShowBalloonTip`(Windows 10+ 渲染为原生通知),多条设备合并一条
- 阈值 20 暂为常量,第 6 步(设置)做成可配

验证指引:
- 连接 FreeClip → 图标绿色实心(~满格)
- 断开/合盒 → 图标变空心灰
- toast:单元测试覆盖;真实验证需电量真实跌破 20%
