# 真实截图

只接受从真实运行的 QuickQR 窗口捕获的截图，使用示例文字或 example.com，避免私人二维码内容。

文件说明：

- `main-light.png`：实际运行的浅色 WPF 主窗口截图。
- `main-dark.png`：实际运行的深色 WPF 主窗口截图。
- `tray-panel.png`：预留；尚未捕获，主 README 不引用该文件。
- `qr-preview.png`：实际 QRCodeService / ImageExportService 生成的 1024px PNG，属于导出效果图，不冒充屏幕截图。

在实际捕获前，主 README 不引用不存在的图片。不要用设计稿、生成式图片或浏览器页面冒充运行截图。

维护者可在构建后运行 `dotnet QuickQR.Tests/bin/Release/net10.0-windows/QuickQR.Tests.dll --preview light`（或 dark / tray），在隔离的示例宿主中加载真实视图，仅使用 example.com 和合成文字；约三分钟后自动退出。托盘示例在截图时暂缓失焦隐藏，不修改生产应用行为。当前工具无法枚举无任务栏按钮的工具窗口，因此托盘截图待手工补充。
