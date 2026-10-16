# 第13步｜可视化剧本编辑器：开发与交接

仓库 `mosen6266-netizen/MS_001`，Windows10/11 x64。第13步只实现草稿、视觉交互、角色备注下拉/复制/多选/排序/撤销与本地持久化，不实现真实Telegram发送；第14步处理**逐条时间编辑+图片素材**，第15步实现便携格式导入导出，第16步发布快照/启动预检。

## 数据与架构
- SQLite schema v5：`src/Infrastructure/Persistence/Migrations/0005_script_editor.sql` 增加`editor_drafts(script_id,revision,snapshot_json,archived_utc,updated_utc)`；与`script_versions`及`run_steps`、`outbox`严格隔离。修改草稿不会修改已有发布版本，也不会改动任何已经运行/暂停的任务。
- `EditorDocument.cs`：白名单`title`、`roles[{roleKey,roleLabel}]`、`steps[{stepKey,roleKey,body,preSendDelayMs,typingDurationMs,postSendDelayMs}]`。roleKey是新剧本逻辑UUID；绝不保留原机器accountId、手机号、Telegram userID、API密钥/账号会话；未知字段拒绝而不是默默接纳。
- 注：此处`preSendDelayMs`默认5000、`typingDurationMs`默认5000、`postSendDelayMs`默认0。时间逻辑为**发送前等待→正在输入→发送→发送后等待**，如30秒+5秒则至少35秒后才提交发送，二者不并行。第13步只展示数值；用户自由设置、批量改时间由第14步实现。此时没有发送或打字动作。
- `EditorDraftRepository.cs`：最多200个未归档草稿、每份至多300角色/1000消息、Unicode中英文/emoji/换行、消息内容上限8192字、revision CAS冲突拒绝、追加已验证备份、复制重编号、需明确确认的归档与可逆恢复。群/任务引用尚存在时阻止归档。元数据目录检索不用一次性加载所有消息正文。
- `apps/desktop/MainWindow.Scripts.cs` 与严格`IpcRequestGuard.cs`：通过可信本地WebView2源，只允许`scripts.list/get/create/save/duplicate/archive/restore`。保存大草稿上限524288字符，其余命令依旧8192字符上限，严禁任意发送Telegram请求。
- `src/contracts/ipc.ts` 对多语言文字、角色和时间进行结构检验；两端都不能接受本机账号ID或认证机密作为剧本可移植字段。

## 视觉操作
- `apps/ui/src/components/ScriptEditorPanel.tsx`：左侧剧本搜索/新建/归档/恢复，右侧消息卡片。每条“发言账号”只能从**本机已登记备注下拉**选择，并可搜索筛选，不要求手输入发言身份。
- 备注经`trim+NFC`归一化精确匹配，多个本机账号使用同名备注只对应一个剧本逻辑角色。同名真实账号的最终选择将在第15/16步根据目标群成员权限预检，歧义必须人工明确。
- 当前**尚无真实TDLib登录**，所以下拉中的已登记备注只能用于提前准备逻辑角色，不证明账号已授权/当前在线；页面对此明确说明。已导入旧草稿角色若找不到本机账号，显示“本机缺少”，不会静默替换。
- 可从备注下拉增加消息；每条文字支持中文英文emoji、换行，编辑过程保留本地状态；用checkbox多选+复制/删除，拖拽或上移下移调整顺序，Ctrl+Z/Ctrl+Y按钮/快捷方式撤销重做，消息搜索与20条分页。
- 700ms无操作后自动保存（有冲突/超时时显示错误）；提供手动保存。内部路由切换仍保留编辑器已挂载状态；切换剧本前强制flush，失败阻止切换，不覆盖冲突版本；关闭窗口前有未完成保存时请求浏览器离开确认。
- 组件只在相关操作上更新本地状态，不会每键重新加载整份SQLite剧本；大量条目只展示每页20条，草稿目录只检索简要字段。UI遵守深色主题与固定侧边栏，确认左绿/取消右红。
- `apps/ui/src/script-editor/state.ts`：纯reducer历史不超过80个（数组结构共享），复制/拖拽/上下移动后撤销重做，所有变更只作用当前草稿。按需缓存不建立Telegram连接。

## 开发验收与真实设备界限
- `tests/Scripts/Editor`（Windows、Linux）：SQL迁移、非敏感角色JSON、安全未知字段、保存重启/备份、CAS冲突、复制/归档/恢复、前等待和正在输入间隔不重叠。
- `apps/ui/src/script-editor/state.test.ts`：复制、多选、拖拽、撤销重做、Unicode、快捷键不劫持文本框原生撤销。
- `apps/ui/src/script-editor/script-editor.test.tsx`：白名单IPC、密码/账号ID字段拒绝、无发送动作、离线初始屏。
- 还需要后续Playwright/Windows WebView2真实鼠标拖拽、虚拟键盘、长期输入时CPU/内存与异常断电/磁盘满的最终完整验收。只有纯单测、SSR及CI不代表用户亲自测试过整个桌面UI。
- 与Signal MS002完全隔离。正式账号同步与TDLib DLL未构建/接入，无真实Telegram发送。禁止使用多账号轮换规避平台反滥用、权限及限流。

下一步：**Step14 三段时间与媒体编辑**。不要在没有验证后直接修改state步骤状态；以最新Actions全绿为准。
