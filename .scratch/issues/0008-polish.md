# 0008 打磨:单实例/日志/唤醒刷新

**状态**:已完成(待用户验证)

- 单实例:`Local\BatteryTray-SingleInstance` 命名互斥;二次启动安静退出(--dump 不受限)
- 日志:`%LOCALAPPDATA%\BatteryTray\log.txt`
  - `LogRules`(Core,3 测):行格式 ISO8601 + 级别;≥256KB 轮转 .old
  - 接入点:扫描失败 / AEP 枚举降级与失败 / FromIdAsync 取地址失败 / HFP 读取异常 / 启动 / 退出
  - 日志自身异常永久沉默,不影响主功能
- 睡眠唤醒:`SystemEvents.PowerModeChanged` Resume → 复用 2s 去抖重新扫描(设备重连需要时间)
- 断连清理:随全量重扫天然覆盖(catalog 整表替换),第 5 步已实测

验证:
- [ ] 启动后再双击 exe → 托盘不会出现第二个图标
- [ ] `%LOCALAPPDATA%\BatteryTray\log.txt` 有"启动"行
- [ ] 合盖睡眠再唤醒 → 面板/图标状态自动恢复
