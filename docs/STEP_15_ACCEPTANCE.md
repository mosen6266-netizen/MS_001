# 第15步开发阶段验收｜角色备注移植与剧本ZIP格式

日期：2026-10-16。仓库：`mosen6266-netizen/MS_001`。开发阶段 done_dev；下一步第16步。

## 权威构建与测试证据
- [GitHub Actions 38954528584](https://github.com/mosen6266-netizen/MS_001/actions/runs/38954528584)：**completed/success、25/25 Job**，含Win10/11目标所使用的Windows托管runner WPF x64构建发布、TypeScript/Vite前端和旧阶段回归，但**不是Win10/11真实桌面用户实测**。
- 本步新增 `tests/Scripts/Portable/Program.cs`：**Linux 18/18，Windows 18/18**。包含三角色跨电脑模拟、B机多余账号仍匹配、缺失及同名歧义、按群候选过滤、NFC、三段时间（含历史1500ms）、PNG去重和恢复、ZIP路径穿越/重复项/额外文件/外国格式/身份字段拒绝。
- 首轮UI失败：SSR初次渲染直接访问window（25 Job中UI失败）；已在 `6ba635d7e5285963f9000905305689d56d940c23` 改为typeof window安全检查，重新运行CI后**全绿**，保留失败证据不隐藏。
- 代码提交 `2ad3feebba6b5e08bfe412575a6269ffb4e1fd6a`，UI修复 `6ba635d7e5285963f9000905305689d56d940c23`。完整文件及格式见 SCRIPT_FORMAT.md。

## 交付范围
- `.mosentgscript` ZIP V1：manifest严格白名单、角色备注/UUID、完整文字/三段毫秒时间、内容寻址图片（PNG/JPEG/WebP），跨电脑可还原。无账号会话、手机号、真实userID、机密或本机路径。
- ZIP路径/内容哈希/未声明文件/大小上限校验；解析整个ZIP成功后再导入媒体，导入草稿数据库事务创建，保护已有草稿。重复图片只一份。
- 精确昵称备注映射（去前后空格+Unicode NFC）；目标电脑有额外账号不阻断；缺失/同名账号角色分别标记 missing/ambiguous，不随机绑定。按群可用账号过滤接口已实现，最终实际群权限与用户选择在Step16落实。
- Windows系统文件对话框只允许用户本地选择文件路径；WebView2 IPC不接受任意路径；编辑器增加导入和导出按钮，不进行Telegram实际发送。

## 推迟到正式用户验收/后续步骤
- Windows10/11真实窗口/对话框、不同磁盘异常和大媒体跨机实测尚未进行（DEFERRED）；TDLib原生账号授权、真实群成员及权限、运行时绑定、平台慢速模式和72小时长稳未验证。
- 本步只是剧本草稿交换功能，不代表可发布正式Telegram群发送工具；MS002及任何现有Cloudflare授权生产资源完全未修改。

**结论：第15步开发和云端CI验证通过。进度15/32（46.875%），剩余17步；第16步待开始。**
