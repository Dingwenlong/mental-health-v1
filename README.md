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

Android 目录、授权和模拟订单验收：

```powershell
pwsh .\scripts\Test-Task7Android.ps1 -DeviceId emulator-5554
```

脚本会临时启动本机 API，用 5 分钟测试令牌进入客户端，再选择模拟收费的 AI 文字套餐、逐项确认三项授权、创建订单并确认模拟收费。令牌只写入系统临时目录，运行结束后立即删除。

管理端开发服务器：

```powershell
npm --prefix apps/admin_web run dev
```

管理端默认连接 `http://127.0.0.1:5165/api/v1/`。Android 模拟器默认连接 `http://10.0.2.2:5165/api/v1/`，也可以通过 `VITE_API_BASE_URL` 或 Flutter 的 `API_BASE_URL` 编译参数覆盖。

首次运行本机数据库时：

```powershell
pwsh .\scripts\Initialize-LocalSecrets.ps1
# 在 .env 中填写阿里云配置和两个登录手机号，不要提交该文件
docker compose --env-file .env -f deploy/docker-compose.yml up -d
pwsh .\scripts\Test-LocalIdentity.ps1
```

Compose 只运行 PostgreSQL 和 Redis，API 由本机脚本启动。`Initialize-LocalSecrets.ps1` 只生成数据库、JWT 和演示证书密钥；阿里云配置与两个手机号保持空白，必须在本机填写。`Test-LocalIdentity.ps1` 会应用迁移、准备测试账号与套餐，并用 5 分钟测试令牌检查普通用户和医生权限。脚本不会输出手机号、令牌或密钥，检查结束后会停止临时 API。

管理端和 Android 的正式登录顺序都是：手机号、人机验证、6 位短信验证码、登录。邮箱只是可选联系信息，不能用于登录。真实登录需要以下私密配置：

```text
PhoneLogin:Aliyun:AccessKeyId
PhoneLogin:Aliyun:AccessKeySecret
PhoneLogin:Aliyun:CaptchaEkey
PhoneLogin:Aliyun:SmsSignName
PhoneLogin:Aliyun:SmsTemplateCode
PhoneLogin:Accounts:ClientPhone
PhoneLogin:Accounts:AdminPhone
```

启用真实阿里云登录时，还要给 API 进程设置以下非私密固定项和一个可写的对象存储目录：

```text
PhoneLogin__Aliyun__Prefix=xfkdn8
PhoneLogin__Aliyun__AdminSceneId=1lae8yfm
PhoneLogin__Aliyun__AndroidSceneId=e20maaxh
LocalObjectStorage__RootPath=<本机临时目录>
```

`.env` 只由仓库内的 PowerShell 脚本读取。`Test-LocalIdentity.ps1` 和 Android 验收脚本会把这些值映射到它们启动的 API 子进程；`dotnet run` 不会自动读取 `.env`。这些自动化脚本会关闭阿里云调用，只检查本地流程。

真实短信会计费。确认两个号码已绑定并明确允许发送后，再按 [手机号短信登录证据](docs/test-evidence/phone-sms-login.md) 创建本机临时目录、补齐固定项并把 `.env` 映射到当前 API 进程，然后执行管理端和 Android 16 验收；该步骤不会打印私密值。本机测试令牌不能代替真实短信验收。

本地 API 启动后，另开一个 PowerShell 运行分析任务：

```powershell
pwsh .\scripts\Run-AnalysisWorker.ps1
```

脚本从本机 `.env` 读取数据库密码，只传给当前分析任务进程，不修改用户或系统环境变量。按 `Ctrl+C` 停止。

本地运行、测试账号和验收步骤会随对应功能补入 `docs`。云部署不在 v1 开发阶段内。
