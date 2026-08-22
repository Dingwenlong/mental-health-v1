#requires -Version 7.0

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$apiProject = Join-Path $repoRoot 'src\MentalHealth.Api\MentalHealth.Api.csproj'
$apiProcess = $null

if (-not (Test-Path -LiteralPath $envPath)) {
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

function ConvertFrom-ResponseJson
{
    param([Parameter(Mandatory)]$Response)

    $text = if ($Response.Content -is [byte[]]) {
        [Text.Encoding]::UTF8.GetString($Response.Content)
    }
    else {
        [string]$Response.Content
    }

    return $text | ConvertFrom-Json
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
    'DemoAccounts__InitialPassword' = $localValues['MH_DEMO_INITIAL_PASSWORD']
}

try {
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
                '--urls', "http://127.0.0.1:$apiPort"
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

    $userBody = @{
        email = 'user@demo.local'
        password = $localValues['MH_DEMO_INITIAL_PASSWORD']
    } | ConvertTo-Json -Compress
    $userLogin = Invoke-WebRequest `
        -Uri "http://127.0.0.1:$apiPort/api/v1/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body $userBody `
        -SkipHttpErrorCheck
    $userResult = ConvertFrom-ResponseJson -Response $userLogin
    if ($userLogin.StatusCode -ne 200 -or
        [string]::IsNullOrWhiteSpace($userResult.accessToken)) {
        throw '普通用户本机登录失败。'
    }

    $doctorBody = @{
        email = 'doctor@demo.local'
        password = $localValues['MH_DEMO_INITIAL_PASSWORD']
    } | ConvertTo-Json -Compress
    $doctorLogin = Invoke-WebRequest `
        -Uri "http://127.0.0.1:$apiPort/api/v1/auth/login" `
        -Method Post `
        -ContentType 'application/json' `
        -Body $doctorBody `
        -SkipHttpErrorCheck
    $doctorResult = ConvertFrom-ResponseJson -Response $doctorLogin
    if ($doctorLogin.StatusCode -ne 401 -or
        $doctorResult.code -ne 'MFA_REQUIRED') {
        throw "医生本机 MFA 门禁失败：HTTP $($doctorLogin.StatusCode)，业务码 $($doctorResult.code)。"
    }

    Write-Host '本机 API 健康检查、普通用户登录和医生 MFA 门禁均通过。'
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
}
