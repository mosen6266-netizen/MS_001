# Step30 容量基准（开发阶段、离线合成）

日期：2026-10-17。以下是可重复执行的容量测试设计，不是实际Telegram上线容量认证。

## 场景与证据

- 300条本地SQLite账号元数据槽位。验证第301条拒绝、分页与重启仍保留。真实授权在线账号数量 = 0、原生TDLib连接 = 0。
- 50个本地模拟群，每群6个角色账号，真实SQLite任务冻结、2秒虚拟时间的发送前等待与输入阶段、等待外部模拟许可；零网络消息。
- 2项故障注入：未知提交结果需要人工复核、单群失败隔离。逻辑72小时后重新打开持久化调度，所有非终态必须进入Review，绝不自动重发。
- 72小时为加速逻辑时钟，每虚拟分钟1个样本，总共4320个合成资源预算样本。该过程是模型测试，不是72小时真实运行。最长仅缓存480条。
- 真实OS进程资源只在短时间CI中读取：CPU、工作集、托管堆、线程数、Windows句柄(可用时)、SQLite文件大小。JSON记录实际墙钟秒数、OS、架构、.NET版本、逻辑CPU数量。Linux缺少Windows句柄值时明确记null。

## 可复验数据源

tests/Capacity/Program.cs生成两份机器可读JSON；tests/Capacity/report-contract.test.mjs要求准确区分虚拟和实测指标。GitHub Actions分别上传 step30-synthetic-capacity-ubuntu-latest、step30-synthetic-capacity-windows-latest 两个Artifacts，每个保留30天。完整CI聚合必须同时检查这两个任务。

## 判定和限制

成功仅表示该版本在短时虚拟任务模型下可以存储300条元数据并操作50个群的本地模拟任务，不能证明300个真实TDLib账号同时在线、真实消息成功率、实际72小时运行稳定、云端许可、网络重连或真实Win10/11系统资源足够。真正的72小时物理长稳、原生DLL、网络波动、重启和安全回滚需在有授权的真实设备中单独测量，不能冒称PASS。

不允许以未执行的数据填入CPU/RAM峰值；CI结果出来后才能登记真实报告Run、测试Commit及测量内容。


## 首次自动运行器实测（测试源码cc2043fa1460c5a6206036fb964149f17b857dfa）

测试原始附件：[Actions Run 39033877609](https://github.com/mosen6266-netizen/MS_001/actions/runs/39033877609) 内的 step30-synthetic-capacity-ubuntu-latest / step30-synthetic-capacity-windows-latest ZIP → JSON。以下数值取自对应Artifact的真实字段，不是推算。除最后完整CI状态外，两项Capacity Job都已有独立SUCCESS结果。

| 观察项 | Linux Runner | Windows Runner |
|---|---:|---:|
| CI 操作系统标识 | Ubuntu 24.04.5 LTS | Microsoft Windows 10.0.26100 |
| CPU 逻辑核心 | 4 | 4 |
| 运行时 | .NET 10.0.13 | .NET 10.0.13 |
| 300账号元数据 | PASS | PASS |
| 50模拟群、300角色映射 | PASS | PASS |
| 虚拟逻辑时间 | 72h（4320分钟） | 72h（4320分钟） |
| **实际墙钟时间** | **4.190秒** | **20.561秒** |
| 观察到的进程工作集最高值 | **90 MiB** | **63 MiB** |
| 最后一个样本线程数 | 9 | 10 |
| 最后一个样本句柄 | 未提供（null） | 267 |
| 最后一个进程CPU取样区间百分比 | 23.85% | 11.82% |
| SQLite主数据库文件 | 524,288 bytes | 524,288 bytes |
| 注入模拟故障/自动重试 | 2 / 0 | 2 / 0 |
| CI程序终端断言计数 | 20/20 PASS | 20/20 PASS |
| JSON内部在最后文件检查前记录的断言 | 19 | 19 |

注意：工作集是该短测样本中观察到的峰值，而非全时段连续采集峰值。CPU值是进程相邻采样区间的指标，不是72小时平均值；Linux句柄不支持所以不补造。实际Tdlib客户端创建数0、真实账号授权数0、Telegram消息发送数0。虚拟资源预算fixture与上述真实短期OS取样互相隔离。

物理长稳72小时没有执行，仍为最终版本用户实机验收的 DEFERRED 项；不得从以上数值推断真实300在线的RAM占用或真实会话重连性能。
