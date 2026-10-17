# Step30 容量与加速长稳测试矩阵

- [x] 合成300条本地SQLite账号元数据、分页、超限拒绝，且无tdjson.dll和真实在线会话。
- [x] 50群×6角色（总300个群角色映射）Preflight权限冻结，50条SQLite任务一次性创建，51群拒绝；等待/typing/外部提交屏障被测试。
- [x] 未知提交结果不自动重发，单群失败隔离，模拟重启时剩余任务都转Review，全局停止后持久不复活。
- [x] 72小时加速**模型时间**、4320次逐分钟资源预算取样、仅保留最近480个样本，不是假造的72小时墙钟运行。
- [x] 运行器真实**短时**OS工作集、CPU、堆、线程、Windows可用句柄、SQLite大小、实际经过秒数生成JSON；报告合同拒绝冒称真实TDLib与72h物理稳定。
- [x] 最新Windows/Linux CI两个容量作业 + 全部原有测试、WPF/Inno离线安装器作业与最终聚合全绿，并记录准确Run/Commit及报告Artifact。
- [ ] 独立物理Win10/11下真实tdjson、合法授权账号的小规模真实连接故障恢复测试，**按用户要求集中最终实机验收**。
- [ ] 真实连续72小时墙钟CPU、内存、句柄、磁盘、连接重连统计与机器规格，**按用户要求集中最终实机验收**。

开发阶段与发布阶段严格分开。最终用户实机的两条DEFERRED不能被合成CI成功替代。

### 开发阶段验收记录

GitHub [Run 39033877609](https://github.com/mosen6266-netizen/MS_001/actions/runs/39033877609) completed/success、57/57 Jobs成功，30个上游依赖全部成功；测试commit cc2043fa1460c5a6206036fb964149f17b857dfa。Windows/Linux各20容量断言＋1 JSON合同测试成功，并分别上传命名含step30-synthetic-capacity的原始JSON报告。实测和未测范围详见STEP_30_ACCEPTANCE.md。真正72小时与TDLib账号实机两项仍勾选留空（DEFERRED）。
