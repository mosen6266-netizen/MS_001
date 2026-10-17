# MOSEN Telegram Scheduler — 数据恢复与升级回滚操作手册

**设计流程，不代表已在真实Windows断电/磁盘损坏场景完成验证。** 未通过实机验收前不得作为生产恢复承诺。

## 一、停止、取证与隔离

关闭主程序并停止全部任务；提交状态不明的消息保持Unknown/Review，不重发。记录固定错误代码与版本，不泄露账号、验证码、密码或API密钥。

受控保存：
- 用户数据 %LOCALAPPDATA%\MOSEN\TelegramScheduler（app.db、可能存在的app.db-wal/app.db-shm、backups、后续sessions、诊断及webview2）。
- 程序目录 %LOCALAPPDATA%\Programs\MOSEN\TelegramScheduler。
- 更新临时与恢复文件 previous-program、previous-program.prior、previous-program.pending、last-update-error.txt。

私有数据备份需限制Windows NTFS读取权限，勿将会话或数据库上传公开仓库或聊天群。

## 二、SQLite可靠恢复的实际边界

1. AppDatabase.CreateBackup使用SQLite Backup API生成数据库完整副本，执行PRAGMA integrity_check。
2. AppDatabase.PrepareRestore先验证备份的完整性、受支持schema版本及每一条schema_migrations SHA256是否匹配程序内嵌迁移记录；只创建独立的restore-candidate-*.db并再次核查，**不会直接覆盖正在使用的app.db**。
3. **准备候选≠完成恢复。** 只能在关闭所有数据库连接、先保存现有数据库完整回退副本、人工核对旧/新schema兼容性后，另行执行受控的候选切换。项目当前未提供已实机验证的“一键恢复”功能。
4. 不能只拷贝app.db而在进程仍运行时删除WAL/SHM，不能将高版本数据库直接降到低版本或删迁移日志。
5. 若备份的SQLite页完整但迁移SHA被篡改，候选生成会拒绝。这是安全阻断，不得通过修改签名或关闭检查绕开。

## 三、升级失败及程序文件恢复

发行候选必须验证P-256清单和整包SHA256及Windows Authenticode发布者证书。主程序退出后，程序目录独立备份，再执行安装器，检查新程序签名和版本。失败时脚本尝试恢复旧程序文件并保留错误日志。两个滚动程序目录备份只用于程序文件，**不是数据库可跨版本自动回滚**。

如果previous-program.pending存在，则视为未完成备份，人工复核前不得删除/覆盖。正式版可信公钥当前为UNCONFIGURED，自动更新应直接拒绝。更新辅助PowerShell脚本完整性/NTFS ACL仍需修复和实机复审。

## 四、未知投递结果

已提交但结果不确定的事务进入Review，只有从官方来源确认实际消息状态才能处理；重新联网、重启、换账号或恢复备份都不得自动重发或重新激活任务。

## 五、卸载与彻底清理

默认卸载保留私人数据。彻底清理必须先确认独立备份，使用单独的Remove-LocalUserData.ps1并显式确认；这会永久清除本产品目录，绝不用于Signal MS002。未在干净Win10/11全面验证前不要依赖此流程进行生产迁移。

## 六、最终实机证据

Win10/Win11分别记录干净安装/离线WebView2、覆盖升级、卸载保留数据、故意损坏备份、断网和异常终止、签名更新失败回滚、真实用户许可及官方TDLib登录重启。每项包含UTC时间、审核人、可核验记录和最终成功/失败结论，才能考虑在release/readiness.json中标记VERIFIED。
