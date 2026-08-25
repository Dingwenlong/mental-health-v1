#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$DeviceId = 'emulator-5554'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $repoRoot 'apps\mobile_flutter'
$apiProject = Join-Path $repoRoot 'src\MentalHealth.Api\MentalHealth.Api.csproj'
$envPath = Join-Path $repoRoot '.env'
$composePath = Join-Path $repoRoot 'deploy\docker-compose.yml'
$apiProcess = $null
$definesDirectory = $null

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    throw '缺少本机 .env，请先运行 scripts/Initialize-LocalSecrets.ps1。'
}

$localValues = @{}
foreach ($line in Get-Content -LiteralPath $envPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
        continue
    }
    $pair = $line -split '=', 2
    if ($pair.Count -eq 2) {
        $localValues[$pair[0].Trim()] = $pair[1]
    }
}

foreach ($required in @(
    'MH_POSTGRES_PASSWORD',
    'MH_JWT_SIGNING_KEY',
    'MH_CLIENT_PHONE',
    'MH_ADMIN_PHONE'
)) {
    if ([string]::IsNullOrWhiteSpace($localValues[$required])) {
        throw "本机 .env 缺少 $required。"
    }
}

Import-Module (Join-Path $PSScriptRoot 'LocalTestJwt.psm1') -Force

function Get-LocalValue {
    param([Parameter(Mandatory)][string]$Name)
    if ($localValues.ContainsKey($Name)) { return [string]$localValues[$Name] }
    return ''
}

function Get-LocalUserId {
    param([Parameter(Mandatory)][string]$Email)
    $query = "SELECT ""Id"" FROM ""AspNetUsers"" WHERE ""Email"" = '$Email';"
    $output = & docker compose --env-file $envPath -f $composePath `
        exec -T postgres psql -U mental_health -d mental_health `
        -tA -v ON_ERROR_STOP=1 -c $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw '无法读取本机测试账号 ID。' }
    $userId = [Guid]::Empty
    if (-not [Guid]::TryParse(($output -join '').Trim(), [ref]$userId)) {
        throw '本机测试账号没有准备完成。'
    }
    return $userId
}

try {
    . (Join-Path $PSScriptRoot 'Use-Toolchain.ps1')

    $deviceState = (& adb -s $DeviceId get-state 2>$null).Trim()
    if ($deviceState -ne 'device') {
        throw "Android 设备不可用：$DeviceId"
    }

    $portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $portProbe.Start()
    $apiPort = $portProbe.LocalEndpoint.Port
    $portProbe.Stop()

    $apiEnvironment = @{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'ConnectionStrings__MentalHealth' = "Host=127.0.0.1;Port=54329;Database=mental_health;Username=mental_health;Password=$($localValues['MH_POSTGRES_PASSWORD'])"
        'ConnectionStrings__Redis' = '127.0.0.1:56379'
        'LocalObjectStorage__RootPath' = Join-Path $repoRoot 'tests\output\local-object-storage'
        'Jwt__Issuer' = 'mental-health-v1-local'
        'Jwt__Audience' = 'mental-health-v1-local'
        'Jwt__SigningKey' = $localValues['MH_JWT_SIGNING_KEY']
        'Jwt__AccessTokenMinutes' = '15'
        'Database__InitializeOnStartup' = 'true'
        'IdentitySeed__Enabled' = 'true'
        'CatalogSeed__Enabled' = 'true'
        'PhoneLogin__Aliyun__Enabled' = 'false'
        'PhoneLogin__Aliyun__AccessKeyId' = Get-LocalValue 'MH_ALIYUN_ACCESS_KEY_ID'
        'PhoneLogin__Aliyun__AccessKeySecret' = Get-LocalValue 'MH_ALIYUN_ACCESS_KEY_SECRET'
        'PhoneLogin__Aliyun__CaptchaEkey' = Get-LocalValue 'MH_ALIYUN_CAPTCHA_EKEY'
        'PhoneLogin__Aliyun__SmsSignName' = Get-LocalValue 'MH_ALIYUN_SMS_SIGN_NAME'
        'PhoneLogin__Aliyun__SmsTemplateCode' = Get-LocalValue 'MH_ALIYUN_SMS_TEMPLATE_CODE'
        'PhoneLogin__Accounts__ClientPhone' = $localValues['MH_CLIENT_PHONE']
        'PhoneLogin__Accounts__AdminPhone' = $localValues['MH_ADMIN_PHONE']
    }

    $previousEnvironment = @{}
    foreach ($entry in $apiEnvironment.GetEnumerator()) {
        $existing = Get-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        $previousEnvironment[$entry.Key] = if ($null -eq $existing) {
            $null
        }
        else {
            $existing.Value
        }
        Set-Item "Env:$($entry.Key)" $entry.Value
    }
    try {
        $apiProcess = Start-Process `
            -FilePath (Get-Command dotnet).Source `
            -ArgumentList @(
                'run',
                '--project', $apiProject,
                '--no-build',
                '--no-launch-profile',
                '--urls', "http://0.0.0.0:$apiPort"
            ) `
            -WorkingDirectory $repoRoot `
            -WindowStyle Hidden `
            -PassThru
    }
    finally {
        foreach ($entry in $previousEnvironment.GetEnumerator()) {
            if ($null -eq $entry.Value) {
                Remove-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item "Env:$($entry.Key)" $entry.Value
            }
        }
    }

    $ready = $false
    foreach ($attempt in 1..60) {
        if ($apiProcess.HasExited) {
            throw "本机 API 启动失败，退出码：$($apiProcess.ExitCode)"
        }
        try {
            $health = Invoke-WebRequest `
                -UseBasicParsing `
                -Uri "http://127.0.0.1:$apiPort/health" `
                -TimeoutSec 1
            if ($health.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $ready) {
        throw '本机 API 在 30 秒内未就绪。'
    }

    $definesDirectory = Join-Path `
        ([IO.Path]::GetTempPath()) `
        "mental-health-v1-task7-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($definesDirectory) | Out-Null
    $definesPath = Join-Path $definesDirectory 'defines.json'
    $userToken = New-LocalTestJwt `
        -UserId (Get-LocalUserId 'abc@qq.com') `
        -Role User `
        -BusinessId ([Guid]'10000000-0000-0000-0000-000000000001') `
        -SigningKey $localValues['MH_JWT_SIGNING_KEY']
    $defines = @{
        API_BASE_URL = "http://10.0.2.2:$apiPort/api/v1/"
        USER_ACCESS_TOKEN = $userToken
    } | ConvertTo-Json
    [IO.File]::WriteAllText(
        $definesPath,
        $defines,
        [Text.UTF8Encoding]::new($false))

    Push-Location $mobileRoot
    try {
        & flutter test `
            integration_test/task7_catalog_flow_test.dart `
            -d $DeviceId `
            "--dart-define-from-file=$definesPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Android 目录与订单流程失败，退出码：$LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Android 登录、套餐、三项授权、模拟订单和确认流程均通过。'
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }

    if ($null -ne $definesDirectory -and (Test-Path -LiteralPath $definesDirectory)) {
        $resolvedDirectory = [IO.Path]::GetFullPath($definesDirectory)
        $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedDirectory.StartsWith(
            $resolvedTemp,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw '临时验收目录不在系统临时目录中，停止清理。'
        }
        Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
    }
}
