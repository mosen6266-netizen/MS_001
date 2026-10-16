# Step04 — 本地数据库、版本合同与故障恢复设计

## 范围
仅实现离线 **SQLite 元数据存储基础**。真实 TDLib 会话数据库、API key、密码、验证码、Token 不得写入 `app.db`，并且不进入日志/剧本导出。

- `src/Core/Data/`：版本化剧本合同与角色映射结构，独立三段计时（先等待、再 typing）。
- `src/Infrastructure/Persistence/Migrations/0001_initial.sql`：账号备注、群/话题元数据、角色/剧本、素材索引、严格 CHECK/FK 约束。
- `src/Infrastructure/Persistence/Migrations/0002_tasks.sql`：按群任务、运行绑定、可核查 outbox 与事件元数据。外键保护避免误删正在使用的实体。
- `AppDatabase`：逐步 SQL 迁移（每版本事务提交）、PRAGMA user_version/迁移 journal SHA256 双重校验、SQLite WAL/FULL、busy timeout、integrity_check、在线备份、仅创建独立恢复候选数据库。
- `tests/Migrations/`：本地无 Telegram 模拟内容的启动/迁移/错误注入/备份/恢复/时长/合同单元验收。

## 持久化路径
默认 `%LOCALAPPDATA%\MOSEN\TelegramScheduler\app.db`；`backups/` 仅放由SQLite Backup API一致性生成的非会话元数据库副本。测试使用临时目录不接触用户数据。

## 恢复原则
升级前先建经 integrity_check 的一致性备份；迁移每版采用 SQLite 事务，进程崩溃或写入失败自动 rollback。
`PrepareRestore` 不覆盖活动的 `app.db` 或 `.db-wal/.db-shm`，只生成可验收的 `restore-candidate-UUID.db`；真实替换必须在后续独立维护模式下完成，确认所有连接关闭后再原子切换与回滚。这避免在线覆盖破坏数据库。
禁止把“保存成功”简单等同于已持久化事务确认；磁盘空间不足、损坏、SQLite Busy 要有明确状态。

## 后续范围与实机延期
- 本步骤只测试编译/数据库迁移；用户授权的账号登录、Windows长期锁文件表现、文件系统断电、杀软、Win10兼容仍列为最终实机验收，不冒称已测。
- 本步骤的模拟异常是**提交前抛异常**，证明 SQLite 事务回滚；不等于已经模拟物理断电（需要系统级混沌/故障测试，计划第 29–31 步）。
- 加密 session、其他敏感数据与 Windows DPAPI 在后续安全步骤实施，不要把本元数据DB宣称为账号会话保管库。
