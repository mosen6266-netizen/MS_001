# 第28步开发阶段测试矩阵（签名更新与回滚）
- [x] Windows/Linux测试：P-256签名、未知密钥拒绝、产品与URL隔离、篡改拒绝、版本/哈希/长度边界。
- [x] Windows/Linux测试：完整下载、HTTP Range断点续传、服务器忽略断点时重新下载、错误SHA删除、无网不触碰原安装。
- [x] Windows WPF编译、自包含Publish、离线Inno完整安装包编译、签名更新按钮与后置脚本包含合同回归。
- [x] 静态审查：签名发布工作流只允许手动dispatch，缺失证书与公钥明确禁止发布，使用只读构建与单独最小权限draft发布作业（签名真实性待真实密钥环境验收）。
- [x] 静态审查：签名私钥不在仓库；两个公开固定信任值目前为UNCONFIGURED，开发程序拒绝自动安装。
- [x] 静态审查：回滚脚本等待主程序退出、再次验证安装包签名及SHA、保留旧程序备份，并核对新安装EXE版本及签名；异常进入恢复路径，用户数据库目录不在删除/替换范围（实际执行待CI或实机）。
- [x] GitHub Actions完整回归成功：52/52 作业，Run 39031497799，受测源码Commit 716f22ad3a4c707b5b4574cad34b738f2d1c70ff。
- [ ] DEFERRED：真实签名证书和密钥配置、正式EXE Authenticode签名与用户可下载Release、真实Win10/11断网/断电/安装失败恢复、SQLite迁移回退、杀软和重装实测，全部集中最终发行验收。

- [x] 17/17 项仓库源码静态合同核对通过（**不等于** Windows/Linux 单测、WPF 构建或真实 Inno 安装）。
- [x] 早期queued状态已被最终[Run 39031497799](https://github.com/mosen6266-netizen/MS_001/actions/runs/39031497799)完成/成功结果取代（开发阶段done_dev）。
- [x] 静态检查：安装成功后仍强制检查新EXE Authenticode证书指纹及清单准确版本，不一致则恢复旧程序；连续升级时轮换`previous-program`/`previous-program.prior`两代备份，未完成`.pending`阻止自动覆盖。
- [x] 中间运行被更新代码提交替代；最终GitHub CI 52/52通过，见docs/STEP_28_ACCEPTANCE.md。

- [x] Windows CI真实编译ReleaseSigner/ReleaseVerifier，解析3个PowerShell脚本；先后捕获脚本截断和WPF/WinForms同名符号，均修复并重跑通过。
- [ ] 发布者Windows PFX、P-256密钥仍UNCONFIGURED：生产签名和真正公开Releases、Win10/11实体连续升级/回滚仍列DEFERRED，不是开发CI可替代的工作。
