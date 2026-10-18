# 发布维护说明

仓库：`you2032zhu/QuickQR`。公开仓库与首次 main 推送已经维护者明确授权。正式标签和 Release 仍须单独确认版本和目标提交；不要将推送授权视为正式版本发布授权。

## 日常检查

```powershell
pwsh ./scripts/Build.ps1
pwsh ./scripts/Build.ps1 -Interactive # 需要可交互桌面，会改写合成测试剪贴板内容
pwsh ./scripts/Package.ps1 -Tag v1.0.0
```

`Build.ps1` 使用 `--locked-mode` 和警告视为错误；不忽略任何 dotnet 非零退出码。交互桌面测试不作为 GitHub hosted runner 的必需步骤，CI 运行可在 Windows 后台环境完成的测试。

`Package.ps1` 默认输出 `artifacts/release`，拒绝复用已有版本目录，防止残留文件混入。生成：

- `QuickQR-v1.0.0-win-x64.zip`
- `QuickQR-v1.0.0-win-arm64.zip`
- `SHA256SUMS.txt`
- `RELEASE-NOTES.md`

ZIP 包含整个应用目录、WPF / .NET 运行库和许可证。每个 ZIP 内文件均与原始发布目录逐一比较 SHA256；EXE 验证对应 PE 架构和 GUI 子系统；runtimeconfig 必须是自包含部署。ARM64 只声明交叉构建，不声明已通过真机验证。

直接发布命令仍可使用：

```powershell
dotnet publish QuickQR.Windows/QuickQR.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/win-x64
dotnet publish QuickQR.Windows/QuickQR.Windows.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/win-arm64
```

WPF 版本不进行 trimming，也不发布未经验证的单文件部署。

## 正式版本流程

1. 更新 `Directory.Build.props` 版本及 CHANGELOG，更新测试记录和已知限制。
2. 通过本地检查，至少在干净 Windows x64 环境验证完整 ZIP；需要扩大兼容性声明时先补对应真机测试。
3. 审核最终提交、README 下载入口和 GitHub Actions 运行结果。
4. 获得维护者对版本、提交和公开发布的确认后，创建并推送签名或带注释的 SemVer 标签。
5. `release.yml` 运行恢复、构建和测试；打包任务只读仓库，只有发布任务可写 Release。
6. 发布任务再次核对 SHA256，通过 `gh release create --verify-tag` 创建 Release。已有同名 Release 时命令失败，不覆盖已有附件。

不要强制推送、覆盖远程历史，或为了重跑而删除已有正式版本。失败版本应调查原因，必要时用新版本发布。

## GitHub CLI 认证

```powershell
gh auth status
gh auth login --hostname github.com --git-protocol https --web
```

授权在 GitHub 官方页面由用户完成。不索取密码或明文 Token，不把认证信息写进仓库。GitHub Actions 使用自动提供的 `github.token`，无需维护者添加个人 Token。

## 候选 Inno Setup 安装器

`packaging/QuickQR.iss` 与 `scripts/Build-Installer.ps1` 已准备。使用官方 Inno Setup 6 编译器和已验证的 x64 自包含目录：

```powershell
pwsh ./scripts/Build-Installer.ps1 -PublishDirectory ./artifacts/release/publish/QuickQR-v1.0.0-win-x64 -IsccPath 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

候选名称 `QuickQR-Setup-v1.0.0-x64.exe`，默认普通用户目录、可选安装目录、开始菜单和可选桌面快捷方式，无管理员权限、额外软件或广告。卸载仅清理安装文件和属于该安装目录的登录启动值，保留 LocalAppData 偏好和导出文件。

当前环境尚无 Inno Setup 编译器，未完成编译、安装或卸载验证。**工作流不构建或上传安装器**。在干净 VM 完成普通用户安装、更改目录、重复安装、运行中卸载阻止、正常卸载、数据保留等验收后，才能考虑加入正式 Release。

## 许可证与用户数据

应用 MIT 许可在根目录 `LICENSE`。不得用应用 MIT 覆盖 `licenses/dotnet-LICENSE.txt`、`licenses/windowsdesktop-LICENSE.txt`、`licenses/Apache-2.0.txt` 或运行库第三方说明。运行时不采集二维码文本和剪贴板内容。

发布前的待验收范围以 [TESTING.md](TESTING.md) 为准。未签名文件的代码签名工作由正式发布者提供证书完成，仓库不得包含私钥。
