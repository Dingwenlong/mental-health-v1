#requires -Version 7.0

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$composePath = Join-Path $repoRoot 'deploy\docker-compose.yml'
$apiProject = Join-Path $repoRoot 'src\MentalHealth.Api\MentalHealth.Api.csproj'
$apiProcess = $null

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf))
{
    throw '缺少本机 .env，请先运行 scripts/Initialize-LocalSecrets.ps1。'
}

Import-Module (Join-Path $PSScriptRoot 'LocalTestJwt.psm1') -Force

$localValues = @{}
foreach ($line in Get-Content -LiteralPath $envPath)
{
    if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#'))
    {
        continue
    }
    $pair = $line -split '=', 2
    if ($pair.Count -eq 2)
    {
        $localValues[$pair[0].Trim()] = $pair[1]
    }
}

foreach ($required in @(
    'MH_POSTGRES_PASSWORD',
    'MH_JWT_SIGNING_KEY',
    'MH_CLIENT_PHONE',
    'MH_ADMIN_PHONE'))
{
    if (-not $localValues.ContainsKey($required) -or
        [string]::IsNullOrWhiteSpace($localValues[$required]))
    {
        throw "本机 .env 缺少 $required。"
    }
}

function Get-LocalValue
{
    param([Parameter(Mandatory)][string]$Name)

    if ($localValues.ContainsKey($Name))
    {
        return [string]$localValues[$Name]
    }
    return ''
}

function Get-LocalUserId
{
    param([Parameter(Mandatory)][string]$Email)

    $query = "SELECT ""Id"" FROM ""AspNetUsers"" WHERE ""Email"" = '$Email';"
    $output = & docker compose --env-file $envPath -f $composePath `
        exec -T postgres psql -U mental_health -d mental_health `
        -tA -v ON_ERROR_STOP=1 -c $query 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        throw '无法读取本机测试账号 ID。'
    }
    $value = ($output -join '').Trim()
    $userId = [Guid]::Empty
    if (-not [Guid]::TryParse($value, [ref]$userId))
    {
        throw '本机测试账号没有准备完成。'
    }
    return $userId
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

try
{
    $previousEnvironment = @{}
    foreach ($entry in $apiEnvironment.GetEnumerator())
    {
        $existing = Get-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        $previousEnvironment[$entry.Key] = if ($null -eq $existing)
        {
            $null
        }
        else
        {
            $existing.Value
        }
        Set-Item "Env:$($entry.Key)" $entry.Value
    }

    try
    {
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
    finally
    {
        foreach ($entry in $previousEnvironment.GetEnumerator())
        {
            if ($null -eq $entry.Value)
            {
                Remove-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
            }
            else
            {
                Set-Item "Env:$($entry.Key)" $entry.Value
            }
        }
    }

    $ready = $false
    foreach ($attempt in 1..60)
    {
        if ($apiProcess.HasExited)
        {
            throw "本机 API 启动失败，退出码：$($apiProcess.ExitCode)"
        }
        try
        {
            $health = Invoke-WebRequest `
                -Uri "http://127.0.0.1:$apiPort/health" `
                -TimeoutSec 1
            if ($health.StatusCode -eq 200)
            {
                $ready = $true
                break
            }
        }
        catch
        {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ready)
    {
        throw '本机 API 在 30 秒内未就绪。'
    }

    $userToken = New-LocalTestJwt `
        -UserId (Get-LocalUserId 'abc@qq.com') `
        -Role User `
        -BusinessId ([Guid]'10000000-0000-0000-0000-000000000001') `
        -SigningKey $localValues['MH_JWT_SIGNING_KEY']
    $catalog = Invoke-WebRequest `
        -Uri "http://127.0.0.1:$apiPort/api/v1/catalog/plans" `
        -Headers @{ Authorization = "Bearer $userToken" } `
        -SkipHttpErrorCheck
    $catalogResult = $catalog.Content | ConvertFrom-Json
    if ($catalog.StatusCode -ne 200 -or $catalogResult.Count -lt 4)
    {
        throw '本机演示套餐未准备完成。'
    }

    $doctorToken = New-LocalTestJwt `
        -UserId (Get-LocalUserId 'doctor@demo.local') `
        -Role Doctor `
        -BusinessId ([Guid]'20000000-0000-0000-0000-000000000002') `
        -SigningKey $localValues['MH_JWT_SIGNING_KEY']
    $riskCases = Invoke-WebRequest `
        -Uri "http://127.0.0.1:$apiPort/api/v1/risk-cases" `
        -Headers @{ Authorization = "Bearer $doctorToken" } `
        -SkipHttpErrorCheck
    if ($riskCases.StatusCode -ne 200)
    {
        throw '医生测试身份不能读取风险病例。'
    }

    Write-Host '本机 API、演示套餐和短时测试身份均通过。'
}
finally
{
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited)
    {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
}
