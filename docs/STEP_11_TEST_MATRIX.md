# Step11 开发测试矩阵
| 测试场景 | 目标 | 云端模拟/自动化 | 真实实机 |
|---|---|---|---|
| v1→v2→v3升级 | SQL原事务/旧账号备注/剧本/计时保留 | CI迁移回归 | DEFERRED |
| 300条账号注册 | 正常上限，不超限；分组/备注检索分页 | 假账号CI | DEFERRED |
| 301条拒绝 | 在SQL事务中拒绝 | 假账号CI | DEFERRED |
| 备注冲突 | 用revision拒绝旧页面覆盖 | CI | DEFERRED |
| 重复Telegram身份 | 已验证官方userID不能绑定两条槽位 | SQLite唯一索引CI | DEFERRED（官方getMe） |
| 账号归档 | 完全一致备注确认/引用阻断/保留元数据 | CI/前端合约 | DEFERRED |
| 账号恢复 | 归档后可见与恢复，不视为在线 | CI/前端合约 | DEFERRED |
| 18条密钥样本 | GUID隔离 + key文件不被覆盖 + 打开清零 | Linux fake/Windows DPAPI | DEFERRED |
| 300注册 vs 4活动 | 会话metadata仅占目录/连接按需控制 | CI fake runtime | DEFERRED |
| 关闭失败 | NeedsReview预算位保留，重试成功再释放 | CI fake runtime | DEFERRED |
| TDLib真实重启 | 断开/重启已授权账号不再次扫码（授权仍有效时） | NOT_RUN | FINAL BLOCKER |
| Win10/11与72h压力 | WebView2、多屏、内存/句柄/连接池回收 | NOT_RUN | FINAL BLOCKER |
