# 第13步开发阶段验收 — 可视化剧本编辑器

日期：2026-10-16；正式开发仓库：`mosen6266-netizen/MS_001`。
**结论：DEV PASS；真实Windows10/11、完整浏览器端到端拖拽、TDLib和Telegram账号/消息操作全部 DEFERRED。**

## 真实CI验证
- [GitHub Actions Run 38946517290](https://github.com/mosen6266-netizen/MS_001/actions/runs/38946517290)：`completed` / **`success`**；**21/21 job通过**，没有失败、跳过或残余构建错误。
- Windows Script Editor草稿持久化 **24/24** 断言成功，Linux **24/24**，仅SQLite和合成角色/文字，没有真实账号和消息发送。
- 前端 Vitest **11个文件、40/40测试成功**；UI/CSS静态 **16/16** 通过。UI单元检查已覆盖角色去重、消息增加/复制/排序/多选/撤销重做、密码/账号ID输入拒绝、浏览器预览不宣称真实发送。前端测试不等于已在Win10/11真机进行鼠标拖拽E2E。
- Windows安全/脱敏/IPC fuzz **35/35**，Linux **33/33** 断言成功，含新增剧本命令、禁止任意发送、非法凭据字段、版本覆盖和过大payload拒绝。
- Windows WPF桌面编译 + win-x64 self-contained publish、SQLite v1→v5迁移/备份/设置、既有TDLib fake adapter/授权状态机/300账号注册/群组话题及权限回归、Chromium 1440/700初始页面截图全部继续PASS。
- 在本步开发中修复：测试数据库初始化顺序、脚本SSR多余aside标签与原单侧边栏断言、C#安全测试未使用常量导致warnings-as-errors、React JSX三元分支、es2022不支持findLastIndex、SSR异步初始化预期、拖拽撤销快捷键的控件范围。所有修复后重新触发完整CI通过。

## 已提交功能的精确范围
1. `src/Infrastructure/Persistence/Migrations/0005_script_editor.sql` 与 `AppDatabase.cs`：schema **v5**，`editor_drafts` 存储编辑草稿（非已发布`script_versions`），确保用户编辑过程不触碰冻结的运行快照/发送队列；升级前备份。
2. `src/Features/Scripts/Editor/EditorDocument.cs`：严格角色与消息模型（roleKey/roleLabel/stepKey/body/3项时间数据），未知字段不被允许，角色经NFC和trim去重，草稿不得含本机accountId、手机号、API Hash/会话；限200未归档剧本、1000条/300角色、正文单条8192字符、整体草稿上限512KiB。
3. `EditorDraftRepository.cs`：事务新建/复制/归档/恢复；保存revision CAS，冲突拒绝悄悄覆盖；同剧本切换前flush，草稿目录检索采用SQLite summary而非一次加载完整消息；复制消息生成新UUID；SQLite备份和重启后草稿恢复。
4. `apps/desktop/MainWindow.Scripts.cs`、`IpcRequestGuard.cs`、`src/contracts/ipc.ts`：Windows WebView2仅允许`scripts.list/get/create/save/duplicate/archive/restore`，严格payload类型及大小上限；**没有任何Telegram网络消息发送接口**。
5. `apps/ui/src/components/ScriptEditorPanel.tsx` 与样式：左侧多剧本管理/搜索/归档恢复，右侧消息卡片；从本机已**登记**账号备注下拉选择逻辑发言角色（不是自由输入账号）；文本/emoji/换行；多选复制与删除、拖拽、上移下移、撤销重做、20条分页、保存状态显示、0.7秒自动保存、强制保存失败阻止切换剧本。为保护草稿，内部路由切换只隐藏不卸载编辑器。名称与消息文本编辑不会每击键串行化整份剧本。
6. `apps/ui/src/script-editor/state.ts`：单一reducer、不可变历史记录与角色UUID；测试覆盖30秒等待+5秒正在输入=35秒的顺序语义，时间配置具体操作与图片属于第14步。
7. `docs/STEP_13_SCRIPT_EDITOR.md`、`docs/STEP_13_TEST_MATRIX.md`：新对话接续和测试矩阵。

## 不能冒称已验证/本步不包含
- 目前无官方TDLib Windows native DLL、无真实Telegram账号登录或群组同步；备注来源是已登记账号的**候选备注**，不是当前真实在线且有群权限的保证。角色在第15/16步导入/启动预检时再逐群确认并冻结绑定。
- 真实Win10/11 WebView2的200草稿/1000消息压力、拖拽鼠标位置、不同DPI/窄屏、异常磁盘和突然断电自动保存、跨窗口同时编辑行为等集中在用户最终安装验收，待做；CI为组件/纯逻辑单测及初始截图，而不是完整端到端UI。
- 三段时间自定义输入、批量设置、图片上传/替换/媒体校验在**第14步**；角色跨设备导入导出在**第15步**，发布/运行预检在**第16步**。
- 当前不公开授权/同步/发送，不保证任何账号数或群任务实况。遵守Telegram权限/限流，不使用多账号规避平台规则。旧 Signal MS002 保持不变。

**Step13 = `done_dev`，Step14 = `not_started`，开发进度 `13/32 = 40.625%`，剩余19步。**
