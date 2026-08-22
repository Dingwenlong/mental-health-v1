# 心理健康智能系统 v1

这是比赛演示版。首轮验收只覆盖 Android 客户端、管理网页、本地 API 和本地分析任务。

系统提供真人或虚拟人咨询、对话与音视频资料分析、关注指数、重点观察和医生回访排程。分析结果只用于辅助医生复核，不能替代诊断。演示和测试只使用合成资料。

## 本地要求

- .NET SDK 10.0.302
- Flutter 3.47.0 stable
- Android SDK 36 和 API 36 模拟器或 Android 设备
- Node.js 24、npm 11
- Docker Desktop
- FFmpeg 8

工具链采用便携安装，不写入用户或系统 `Path`。打开 PowerShell 后先在仓库根目录执行：

```powershell
. .\scripts\Use-Toolchain.ps1
```

## 验证骨架

```powershell
dotnet restore MentalHealth.slnx
dotnet build MentalHealth.slnx --no-restore
dotnet test MentalHealth.slnx --no-build
Push-Location apps/mobile_flutter
flutter analyze
flutter test
Pop-Location
npm --prefix apps/admin_web run build
```

本地运行、测试账号和验收步骤会随对应功能补入 `docs`。云部署不在 v1 开发阶段内。
