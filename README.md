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

OpenCV 的 Windows 运行库已经锁进 NuGet 依赖，不需要单独安装。当前只验收本机 Windows；以后部署到 Linux 时再更换对应运行库包。

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

Android 登录、目录、授权和模拟订单验收：

```powershell
pwsh .\scripts\Test-Task7Android.ps1 -DeviceId emulator-5554
```

脚本会临时启动本机 API，在 Android 设备上完成普通用户登录、选择模拟收费的 AI 文字套餐、逐项确认三项授权、创建订单并确认模拟收费。账号参数只写入系统临时目录，运行结束后立即删除。

管理端开发服务器：

```powershell
npm --prefix apps/admin_web run dev
```

管理端默认连接 `http://127.0.0.1:5165/api/v1/`。Android 模拟器默认连接 `http://10.0.2.2:5165/api/v1/`，也可以通过 `VITE_API_BASE_URL` 或 Flutter 的 `API_BASE_URL` 编译参数覆盖。

首次运行本机数据库时：

```powershell
pwsh .\scripts\Initialize-LocalSecrets.ps1
docker compose --env-file .env -f deploy/docker-compose.yml up -d
pwsh .\scripts\Test-LocalIdentity.ps1
```

`Test-LocalIdentity.ps1` 会应用迁移并创建虚构测试账户与演示套餐，然后检查套餐目录、普通用户登录和医生 MFA 门禁。脚本不会输出 `.env` 中的密码或密钥，检查结束后会停止临时 API 进程。

本地 API 启动后，另开一个 PowerShell 运行分析任务：

```powershell
pwsh .\scripts\Run-AnalysisWorker.ps1
```

脚本从本机 `.env` 读取数据库密码，只传给当前分析任务进程，不修改用户或系统环境变量。按 `Ctrl+C` 停止。

本地运行、测试账号和验收步骤会随对应功能补入 `docs`。云部署不在 v1 开发阶段内。
