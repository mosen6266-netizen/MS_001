# 第24步开发阶段验收｜性能预算、资源防泄漏与8小时模拟

日期：2026-10-16。唯一仓库 `mosen6266-netizen/MS_001`。**结论 done_dev，第25步待开始。** 不构成Windows10/11实机连续8小时或正式Telegram发送验收。

## 可核实CI数据
- [GitHub Actions Run 38973014639](https://github.com/mosen6266-netizen/MS_001/actions/runs/38973014639)：**completed/success，43/43项成功**；包含Windows WPF构建及win-x64发布，所有旧账号、群、历史、回执、恢复和前端回归作业全部通过。
- 新 `tests/Performance/Program.cs`：**Windows 227/227、Linux 227/227**；重复28,800次逻辑时钟推进，模拟8小时趋势，8小时后的480个一分钟摘要点容量固定，没有后台真实时钟等待。对LRU大容量压力、线程安全和订阅注销/重复释放、CPU内存线程句柄阈值、冷却及重报警、日志双文件轮转均进行断言。
- 首轮资源预算C#声明语法错误已修复；第二轮测试用了低于严重上限的520,000字节却错误期待512KiB超限，已修正为550,000字节，**资源限额本身没有被降低**。最终使用上述完整成功CI验收。
- 代码主要提交 [`10c493dc`](https://github.com/mosen6266-netizen/MS_001/commit/10c493dc0f8e413811d9a8c7a11a13bd606b140b)，最终测试修正 [`45085eb5`](https://github.com/mosen6266-netizen/MS_001/commit/45085eb524a70bce21e23bcb691a6b076a6ca4f8)。

## 本步交付
- 资源门禁：当前进程的CPU、内存、GC堆、线程和Windows句柄，明确的timer/cache/JSONL字节上限。连续两次异常才进入降频，连续三次健康才恢复；不将日志内容或Telegram资料导入指标。
- 关键任务和紧急停止**不降级**；非关键本地状态读取采用5→15→30秒、异常扫描6→30→60秒降频；界面单时钟倒计时维持，资源超限弹窗按既有去重/节流机制提醒。
- `SubscriptionRegistry` 在窗口关闭时统一注销 WebView2 导航、新窗口、WebMessageReceived、托盘双击和 DispatcherTimer Tick，不残留匿名回调；`BoundedLruCache` 上限1024条，`ResourceTrend` 每分钟一条最多480点。
- 只读资源数据，不生成真实Telegram发送请求。参阅 `docs/PERFORMANCE.md` 资源预算与复现步骤。

## DEFERRED 真实验收
- Windows10/11硬件上真实8小时及72小时 CPU/RAM/句柄、WebView2子进程、睡眠/杀毒/联网、TDLib多账号50个用户许可群组持续运行和界面流畅度仍未实机执行。
- 本步缓存LRU为通用安全组件，不表示所有后续UI缓存已经接入。运行中心仍展示本地模拟快照，不伪造Telegram正式发送效果。
- 没有触碰Signal MS002，也没有部署/修改Cloudflare生产卡密。下一步25需独立产品标识、密钥和数据库隔离。

**开发进度 24/32（75%），剩余8步。下一步25独立卡密产品授权。**
