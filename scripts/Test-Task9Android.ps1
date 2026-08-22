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
    'MH_DEMO_INITIAL_PASSWORD'
)) {
    if ([string]::IsNullOrWhiteSpace($localValues[$required])) {
        throw "本机 .env 缺少 $required。"
    }
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
        'Jwt__MfaSetupTokenMinutes' = '5'
        'Database__InitializeOnStartup' = 'true'
        'IdentitySeed__Enabled' = 'true'
        'CatalogSeed__Enabled' = 'true'
        'DemoAccounts__InitialPassword' = $localValues['MH_DEMO_INITIAL_PASSWORD']
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
        "mental-health-v1-task9-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($definesDirectory) | Out-Null
    $definesPath = Join-Path $definesDirectory 'defines.json'
    $defines = @{
        API_BASE_URL = "http://10.0.2.2:$apiPort/api/v1/"
        CHAT_HUB_URL = "http://10.0.2.2:$apiPort/hubs/chat"
        DEMO_USER_EMAIL = 'user@demo.local'
        DEMO_COUNSELOR_EMAIL = 'counselor@demo.local'
        DEMO_PASSWORD = $localValues['MH_DEMO_INITIAL_PASSWORD']
    } | ConvertTo-Json
    [IO.File]::WriteAllText(
        $definesPath,
        $defines,
        [Text.UTF8Encoding]::new($false))

    Push-Location $mobileRoot
    try {
        & flutter test `
            integration_test/task9_realtime_chat_test.dart `
            -d $DeviceId `
            "--dart-define-from-file=$definesPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Android 实时文字咨询失败，退出码：$LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Android 用户与咨询师共发送 20 条合成消息，落库顺序一致。'
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
