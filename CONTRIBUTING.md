# 贡献指南

欢迎提交问题、文档改进和 Pull Request。开始较大修改前，先通过 Issue 讨论范围。

1. Fork 仓库，从 main 新建分支。
2. 安装 `global.json` 指定的 .NET 10 SDK；WPF 需要 Windows。
3. 执行 `pwsh ./scripts/Build.ps1`；交互桌面可额外运行 `pwsh ./scripts/Build.ps1 -Interactive`。
4. 修复或新增行为时添加相应测试。手工验证 UI、焦点和托盘变化；不要只验证矩阵逻辑。
5. 提交使用 Conventional Commits，例如 `fix: preserve tray panel during export`、`docs: clarify startup behavior`。
6. PR 说明问题、改变后的行为和真实执行过的测试；未测试的平台应明确标记。

保持完全离线，不添加遥测、剪贴板监控、在线二维码服务或无必要的大型依赖。主窗口和托盘复用同一个状态对象。不要提交生成文件、私人二维码、Token、证书或个人绝对路径。

提交贡献时，请确保你有权按项目 MIT 许可提供这些改动，并保留所有第三方版权与许可声明。使用 AI 辅助时也请审查生成内容并说明无法确认的外部来源。

版本修改集中在 `Directory.Build.props`，同时维护 CHANGELOG；推送 `v*` 标签会触发对外 Release，请仅由有发布权限的维护者在批准后执行。安装器在完成安装/卸载验收前不进入自动发布流程。
