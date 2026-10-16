# 第14步时间与媒体编辑测试矩阵

| 功能验收 | 合成CI检查 | 实际Windows/Telegram |
|---|---|---|
| 发送前等待30秒+输入5秒→第35秒可发送，绝不重叠 | 纯虚拟时间线 | FINAL DEFERRED |
| 0+0可立即进入发送阶段，后等待从回执开始 | TimingPreview单元 | FINAL DEFERRED |
| 剧本默认5/5/0及调整后只影响新增消息 | 编辑器reducer测试 | FINAL DEFERRED |
| 每条自定义时间、快捷0/3/5/10/15/30秒 | 前端组件/合约 | FINAL DEFERRED |
| 多选批量修改仅变更勾选时间字段 | reducer测试 | FINAL DEFERRED |
| 旧毫秒时间、复制/拖拽/撤销无损 | JS+C#历史草稿测试 | FINAL DEFERRED |
| PNG/JPEG/WebP识别、限制5MiB/尺寸 | C#文件头与合同 | 真机解码/预览 DEFERRED |
| 仅本地hash引用、去重、128MiB总额度 | C#本地素材测试 | FINAL DEFERRED |
| 图文、纯图片、替换和移除引用 | UI组件、草稿序列化 | Win10/11拖放 DEFERRED |
| 篡改图片与非法SVG/路径/超大数据拒绝 | C#素材和IPC安全 | FINAL DEFERRED |
| 多剧本使用同图移除不误删实体 | C#去重/引用保留 | FINAL DEFERRED |
| 历史剧本/账号/群组/设置无损 | 全量Windows+Linux CI | FINAL DEFERRED |
| ZIP导入导出图片/角色移植 | **第15步** | NOT IN STEP14 |
| 发送中typing上传/回执/中断恢复 | 后续调度与实机 | NOT IN STEP14 |
