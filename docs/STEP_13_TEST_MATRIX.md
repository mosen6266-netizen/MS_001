# 第13步 草稿编辑器验收矩阵

| 验收条件 | 本阶段自动化测试 | 真实设备最终验收 |
|---|---|---|
| Unicode备注角色键与账户主键完全分离 | C# + TS role-only校验 | DEFERRED |
| 下拉选择已登记本机备注，无自由输入发言人 | UI源码/SSR检查 | DEFERRED |
| 300个角色/1000条消息容量限制 | 边界规则 | DEFERRED |
| 文本中英文、换行、emoji持久保存 | SQLite读写单测 | DEFERRED |
| 拖拽、上下移动、多选、复制、删除 | 纯reducer单测 | WebView2实际拖拽 DEFERRED |
| 撤销/重做与文本框Ctrl+Z隔离 | reducer/快捷键单测 | DEFERRED |
| 自动保存/手动保存/冲突拒绝 | SQLite CAS/渲染+模拟 | 突然断电/磁盘满 DEFERRED |
| 切换剧本前保存失败阻止切换 | 源码逻辑/构建 | DEFERRED |
| 多剧本、新建、归档、恢复、复制 | SQLite测试 | DEFERRED |
| 未登录账号不被标记“已授权” | UI SSR文字测试 | DEFERRED |
| 时间显示顺序30+5=35s | C#/TS静态数值回归 | 第14/发送运行阶段 DEFERRED |
| 草稿修改不影响已发布运行快照 | 独立表结构/SQL | 第16及最终实操 DEFERRED |
| 非法字段/脚本任意发送IPC拒绝 | 安全测试 | DEFERRED |
| Win10/11多DPI与1440/700渲染 | Chromium首页截屏 | 第31步实机 DEFERRED |
