# 第 04 步开发验收：SQLite 数据合同、迁移与恢复

日期：2026-10-16。仓库：`mosen6266-netizen/MS_001`。
状态：**DEV PASS；Windows实机/物理断电与生产环境测试 DEFERRED**。

## 本步源码交付
- `src/Core/Data/DataContracts.cs`：版本化剧本数据合同、无账号秘密的角色映射、发送前等待/正在输入/发送后等待三个字段。30秒 + 5秒 = 35秒，严格顺序，不存在重叠。
- `src/Infrastructure/Persistence/AppDatabase.cs`：SQLite WAL + FULL 持久化、单版本事务迁移、迁移 SQL 哈希记录、完整性检查、升级前一致性备份、独立恢复候选（不覆盖运行中的app.db）。
- `src/Infrastructure/Persistence/Migrations/0001_initial.sql`：账号本地元数据、群/话题、剧本与角色、媒体资源索引、外键和时间约束。
- `src/Infrastructure/Persistence/Migrations/0002_tasks.sql`：任务、冻结绑定、逐步任务状态、outbox及脱敏事件信息（不执行发送）。
- `tests/Migrations/Program.cs`：启动、升级、SQL约束、回滚、备份恢复、数据不丢失、时长与导出安全等27个断言。
- `.github/workflows/ci.yml`：Linux/Windows双环境执行迁移故障注入测试，同时保留既有 React/TypeScript 和 Windows WPF 编译作业。

## GitHub CI 事实
- [GitHub Actions 测试执行 38922965210](https://github.com/mosen6266-netizen/MS_001/actions/runs/38922965210)：**completed/success**。
- SQLite migrations / ubuntu-latest：**27 checks passed**。
- SQLite migrations / windows-latest：**27 checks passed**。
- UI：依赖锁定 + TypeScript/React lint/test/build **success**。
- Windows WPF：dotnet build + self-contained publish **success**。
- 首次执行遇到 NuGet 高危公告 `NU1903`（SQLitePCLRaw.lib.e_sqlite3 2.1.11）；没有关闭安全门槛，升级 `Microsoft.Data.Sqlite` 至 `10.0.13` 后上述全套任务通过。
- 基础 SQL迁移与异常注入使用模拟数据，不创建或登录 Telegram 账号、不写 Cloudflare、不操作 Signal MS002。

## 恢复与安全边界
- v1 → v2 在提交前故障注入触发后保留 v1 完整且未生成半迁移表；正常重启可再次迁移。
- v1 一致性备份可恢复为独立候选文件；**恢复功能不会在数据库仍被使用时直接覆盖原文件**。未来独立维护模式负责确认所有连接关闭、切换文件并保留回退副本。
- `app.db` 是本地元数据存储，不是 TDLib 登录会话/密钥仓库。备份目前是非加密元数据数据库快照，后续的加密备份、访问控制与独立会话存储属于安全和发布阶段。
- 真实物理断电、Win10/Win11 安装、杀毒软件、NTFS 句柄占用与不同硬件的大容量性能，按用户指令统一 DEFERRED 至最终实机验收。仿真异常不代表真实断电已经测试。

## 状态交接
开发步骤：**04 done_dev**。
下一步骤：**05 Windows 容器与导航框架（not_started）**。
32步开发进度：**4/32 = 12.5%**，剩余28步；最终真实设备验收仍未通过。
