# 第16步开发阶段验收｜不可变发布快照与启动前预检

日期：2026-10-16，唯一仓库 `mosen6266-netizen/MS_001`。结论：**done_dev**，第17步待开始；不等于生产/Windows真实授权验收通过。

## 可核实的开发/CI证据
- [完整 Actions run 38956311705](https://github.com/mosen6266-netizen/MS_001/actions/runs/38956311705) 状态 **completed/success，27/27 Job**。
- 新增 `tests/Preflight/Program.cs`：Windows **30/30**、Linux **30/30**（合成账号/群，不使用任何真实Telegram凭据），包括发布v1/v2、草稿与发布隔离、旧修订拒绝、空剧本拒绝、图片引用损坏、SHA篡改拒绝、角色遗漏全部列出、歧义与人工选择、群权限刷新、未授权、重复任务、论坛话题缺失/关闭、50群边界及30秒+5秒时间顺序。
- 旧SQLite v1→v6与跨版本备份、settings、group、editor draft、安全、TDLib adapter/auth/session mocks、图片ZIP、Chromium截图、UI lint/test/build全部通过；Windows WPF x64编译、self-contained publish成功。
- 中间发现失败并真实修复：历史回归测试仍要求v5，已经对Settings/Groups/Scripts Editor统一更新；TypeScript发布版本列表unknown类型已加严格验证与收窄；初次桌面编译缺少 `DraftConflictException` 命名空间已补充。最后完整CI确实全绿。
- 主要源码提交：[核心模块 `755d001e`](https://github.com/mosen6266-netizen/MS_001/commit/755d001e3d32ec6d30969c5b172e055b20eaca00)，[前端接线 `8635e6b`](https://github.com/mosen6266-netizen/MS_001/commit/8635e6b3922339f3b914e62a9b0368a3eb78eb35)，[最终UI类型修复 `b6d6296`](https://github.com/mosen6266-netizen/MS_001/commit/b6d6296896ee6fff95331cbfb65a97b413c0adc8)。

## 已交付行为
- 发布动作遵守当前草稿revision CAS并原子保存 `script_versions`、角色与消息、media资产引用和带SHA256的完整快照；发布后后续草稿编辑不能修改运行所依赖的发布版本。
- 纯时间与链接实体预览；不包含实际Telegram发送、假冒输入状态或虚构回执。
- 每群预检全部问题，以备注精确匹配已使用角色并报告所有缺少角色、同名歧义、人工选人验证、账号即时授权、群/话题及媒体完整性；只有完全通过才能生成`FrozenTarget`，与任何后续编辑草稿隔离。
- 运行入口**尚未开放**：因为原生TDLib实时授权与群权限刷新未进行，客户端源代码明确将LiveAuthorized/MembershipsFreshForStart置为false，真实预检会拒绝启动；第17步仅先实现独立调度模拟。

## 最终发布验收仍缺少
- Windows10/11真人界面操作和DPI，多账号会话官方实时校验，真实群/话题权限、所有同名账号的人工选择，合法消息收发，断电/网络/休眠恢复、至少72小时长稳与300/50容量实测。
- CI只验证云端runner构建与合成环境，不替代真实客户端。Signal MS002和现有Cloudflare卡密生产资源未修改。

**进度 16/32 = 50%，剩余16步。下一步 Step17。**
