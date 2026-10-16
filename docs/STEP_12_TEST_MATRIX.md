# Step12 群组、话题、权限验证矩阵
| 场景 | 开发模拟验收 | 真实用户实机 |
|---|---|---|
| chat_id与名称分离，改名后原剧本绑定保留 | SQLite FK/rename mock | DEFERRED |
| 旧手工群资料不作为可信群组 | v4升级后隔离 | DEFERRED |
| 普通群/超级群被识别，私聊和频道过滤 | 伪TDLib JSON解析 | DEFERRED |
| forumTopicInfo的thread id与chat id校验 | TDLib shaped test | DEFERRED |
| 话题关闭和隐藏拒绝启动 | preflight policy | DEFERRED |
| member/admin/creator/restricted/left/banned识别 | parser及策略 | DEFERRED |
| 群默认文字/图片权限不允许时阻止启动 | 解析+策略 | DEFERRED |
| 成员ID属于不同账号时权限不共用 | SQLite per-account | DEFERRED |
| 迟到旧授权不能覆盖新权限撤销 | 时间戳严格比较 | DEFERRED |
| 慢速模式与60秒授权快照TTL | 解析+preflight | DEFERRED |
| 无本次官方重新检查时禁止任务启动 | strict internal fresh-gate | DEFERRED |
| 56组分页+中文搜索及话题检索 | mock SQLite + 前端 | DEFERRED |
| Windows UI仅允许读取群组数据 | IPC白名单+Vitest | DEFERRED |
| 原生TDLib群组/话题同步与真人账号登录 | 未执行 | FINAL BLOCKER |
| 50群任务并发、FLOOD_WAIT与慢速模式实操 | 未执行 | FINAL BLOCKER |
