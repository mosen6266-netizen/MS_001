# 非目标、操作权限及隐私边界 — Step 01

## V1.0 非目标
1. 不构建 Bot-only 方案，也不模拟 Telegram Desktop 的鼠标点击代替协议。
2. 不进行陌生人私信批发、未经同意广告推广、伪造多人对话、身份冒用、假装第三方账号正在输入或已读。
3. 不开发绕过 Telegram FLOOD_WAIT、群慢速模式、用户封禁或频率限制的逻辑；失败后更换账号“接着发”不是可用的恢复策略。
4. 不承诺同一台 PC 上 300 账号同时在线；只有实际模拟容量、单账号授权和受许可多任务实测各自成立才标记通过。
5. 不自动将账号会话、聊天正文、联系人、手机号或开发者API凭据上传 Cloudflare。
6. 不把 Telegram 密钥、账号会话、验证码、Cloudflare 凭据或任何生产密钥放进 GitHub。
7. 不修改 Signal Auto Scheduler MS002、MS007 或已在用的旧许可证生产资源。
8. 不承诺 Windows 10 的系统安全支持；即使应用通过兼容性测试，OS的安全更新是不同议题。

## 用户与平台授权
- Telegram 账号必须由合法所有者主动授权；每个被运行任务使用的群/话题也必须具有发送权限。
- 真实账号登录、首次向群发送、生产 Cloudflare 操作分别按步骤获得明确批准。
- 客户端必须在用户退出/账号撤销授权时停止对该账号的发送；不保留失效会话用于批量重试。
- 所有自动化均应易于停止、有中文清晰的任务状态和审计线索。

## 软件与品牌注意
- 工作名称 MOSEN Group Scheduler，不能以Telegram官方客户端身份宣传或使用未经核准的Telegram官方标识。
- 用户已提供纸飞机图案，现仓库里的 SVG 为转换源；产品商标合规与最终图标需在正式发布门禁审查。
- 当前GitHub `MS_001` 可见性为 public（于2026-10-16核对）。此仓库只存源码与非敏感规划，若不希望公开源代码，应在 GitHub 手动改为 Private。公开仓库不能存个人会话。
## 当前官方参考
- Telegram API 使用条款：https://core.telegram.org/api/terms
- Microsoft Windows 10 生命周期：https://learn.microsoft.com/en-us/lifecycle/announcements/windows-10-end-of-support
