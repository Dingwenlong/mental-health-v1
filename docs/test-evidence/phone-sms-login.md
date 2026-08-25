# 手机号短信登录测试记录

日期：2026-08-25

## 本地回归范围

- API、管理端和 Android 不再使用邮箱、密码或动态验证器登录。
- 既有 Android 集成流程使用 5 分钟测试令牌，不调用真实验证码或短信服务。
- 测试令牌只写入系统临时目录，脚本结束后删除。
- Docker Compose 只运行 PostgreSQL 和 Redis；API 由本机脚本启动。

本机 `.env` 保存以下私密配置，文件不提交：

```text
MH_ALIYUN_ACCESS_KEY_ID
MH_ALIYUN_ACCESS_KEY_SECRET
MH_ALIYUN_CAPTCHA_EKEY
MH_ALIYUN_SMS_SIGN_NAME
MH_ALIYUN_SMS_TEMPLATE_CODE
MH_CLIENT_PHONE
MH_ADMIN_PHONE
```

仓库内的 PowerShell 验收脚本读取 `.env`，再把这些值传给它们启动的 API 子进程。`dotnet run` 本身不会读取 `.env`。脚本不输出私密值，也不写入用户或系统环境变量。

## 自动化结果

| 检查 | 结果 |
| --- | --- |
| 本机短时 JWT 契约 | 通过；用户与咨询师声明、5 分钟有效期和 HS256 签名均已核对 |
| 本机身份与套餐检查 | 通过；API、套餐、普通用户和医生权限均正常 |
| .NET 恢复、构建、测试 | 锁定恢复通过；构建 0 警告、0 错误；单元 136、契约 96、集成 125 全部通过；性能测试项目没有可发现的测试 |
| 管理端测试与构建 | 7 个测试文件、28 个测试通过；生产构建通过 |
| Flutter 静态分析与测试 | 静态分析通过；默认并行测试触发已知的 Windows 测试进程崩溃；`flutter test --concurrency=1` 串行运行 45 个测试全部通过；认证、验证码 WebView 和联系邮箱 18 个聚焦测试通过 |
| Android Debug APK | 构建通过；225574389 字节；SHA-256 `FBDEFC73CAA499D9DAC97E29BCD769A5E81284FF0FC7672105199E4590793B5D` |
| 旧登录实现与私密值扫描 | 旧接口、旧字段、旧错误码和带值私密配置均未发现 |

默认并行执行 `flutter test` 时，Windows 应用日志记录了 `flutter_tester.exe` 异常代码 `0xc0000005`、错误偏移 `0x35abb0`。改用 `flutter test --concurrency=1` 后，完整套件 45 个测试全部通过。两项结果分别记录：默认并行模式仍受本机测试进程崩溃影响，串行模式通过。

APK：`apps/mobile_flutter/build/app/outputs/flutter-apk/app-debug.apk`

## 真实登录验收

本轮没有执行阿里云、管理端或 Android 16 真实登录验收，因为本机缺少 5 项阿里云私密值。测试账号手机号、数据库、Flutter/Android 工具链和 API 36 AVD 预检均已准备完成；未启动模拟器，也未发送短信。

补齐阿里云配置并确认允许计费后，先启动 PostgreSQL 和 Redis：

```powershell
docker compose --env-file .env -f deploy/docker-compose.yml up -d
```

另开一个 PowerShell，在仓库根目录执行下面的 API 启动命令。它只给当前 PowerShell 和 API 子进程设置环境变量，不打印私密值；API 停止后会恢复原值：

```powershell
$localValues = @{}
Get-Content -LiteralPath .env | ForEach-Object {
    if (-not [string]::IsNullOrWhiteSpace($_) -and
        -not $_.TrimStart().StartsWith('#')) {
        $pair = $_ -split '=', 2
        if ($pair.Count -eq 2) { $localValues[$pair[0].Trim()] = $pair[1] }
    }
}

$required = @(
    'MH_POSTGRES_PASSWORD', 'MH_JWT_SIGNING_KEY',
    'MH_ALIYUN_ACCESS_KEY_ID', 'MH_ALIYUN_ACCESS_KEY_SECRET',
    'MH_ALIYUN_CAPTCHA_EKEY', 'MH_ALIYUN_SMS_SIGN_NAME',
    'MH_ALIYUN_SMS_TEMPLATE_CODE', 'MH_CLIENT_PHONE', 'MH_ADMIN_PHONE'
)
$missing = @($required | Where-Object {
    -not $localValues.ContainsKey($_) -or
    [string]::IsNullOrWhiteSpace($localValues[$_])
})
if ($missing.Count -gt 0) { throw "本机 .env 缺少：$($missing -join ', ')" }

$objectStorageRoot = Join-Path `
    ([IO.Path]::GetTempPath()) `
    'mental-health-v1\real-phone-login\object-storage'
[IO.Directory]::CreateDirectory($objectStorageRoot) | Out-Null

$apiEnvironment = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    ConnectionStrings__MentalHealth = "Host=127.0.0.1;Port=54329;Database=mental_health;Username=mental_health;Password=$($localValues['MH_POSTGRES_PASSWORD'])"
    ConnectionStrings__Redis = '127.0.0.1:56379'
    LocalObjectStorage__RootPath = $objectStorageRoot
    Jwt__Issuer = 'mental-health-v1-local'
    Jwt__Audience = 'mental-health-v1-local'
    Jwt__SigningKey = $localValues['MH_JWT_SIGNING_KEY']
    Jwt__AccessTokenMinutes = '15'
    Database__InitializeOnStartup = 'true'
    IdentitySeed__Enabled = 'true'
    CatalogSeed__Enabled = 'true'
    PhoneLogin__Aliyun__Enabled = 'true'
    PhoneLogin__Aliyun__Prefix = 'xfkdn8'
    PhoneLogin__Aliyun__AdminSceneId = '1lae8yfm'
    PhoneLogin__Aliyun__AndroidSceneId = 'e20maaxh'
    PhoneLogin__Aliyun__AccessKeyId = $localValues['MH_ALIYUN_ACCESS_KEY_ID']
    PhoneLogin__Aliyun__AccessKeySecret = $localValues['MH_ALIYUN_ACCESS_KEY_SECRET']
    PhoneLogin__Aliyun__CaptchaEkey = $localValues['MH_ALIYUN_CAPTCHA_EKEY']
    PhoneLogin__Aliyun__SmsSignName = $localValues['MH_ALIYUN_SMS_SIGN_NAME']
    PhoneLogin__Aliyun__SmsTemplateCode = $localValues['MH_ALIYUN_SMS_TEMPLATE_CODE']
    PhoneLogin__Accounts__ClientPhone = $localValues['MH_CLIENT_PHONE']
    PhoneLogin__Accounts__AdminPhone = $localValues['MH_ADMIN_PHONE']
}
$previousEnvironment = @{}
try {
    foreach ($entry in $apiEnvironment.GetEnumerator()) {
        $oldValue = Get-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        $previousEnvironment[$entry.Key] = if ($null -eq $oldValue) { $null } else { $oldValue.Value }
        Set-Item "Env:$($entry.Key)" $entry.Value
    }
    dotnet run --project src/MentalHealth.Api/MentalHealth.Api.csproj `
        --no-launch-profile --urls http://127.0.0.1:5165
} finally {
    foreach ($entry in $previousEnvironment.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        } else {
            Set-Item "Env:$($entry.Key)" $entry.Value
        }
    }
    $apiEnvironment.Clear()
    $localValues.Clear()
}
```

API 运行后，再分别打开两个 PowerShell 启动管理端和 Android：

```powershell
npm --prefix apps/admin_web run dev
```

```powershell
. .\scripts\Use-Toolchain.ps1
Push-Location apps/mobile_flutter
flutter run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5165/api/v1/
Pop-Location
```

也可以用 .NET Secret Manager 提供相同配置；不要把私密值直接写进命令、仓库或验收记录。管理端使用管理手机号完成一次登录；Android 16 使用客户端手机号完成一次登录。记录中不得写入完整手机号、短信验证码、AccessKey、`ekey`、挑战令牌或 JWT。
