#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$DeviceId = 'emulator-5554',
    [string]$LanIp,
    [string]$PlaywrightWrapper = $env:MH_PLAYWRIGHT_CLI_WRAPPER,
    [string]$BashPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $repoRoot 'apps\mobile_flutter'
$webRoot = Join-Path $repoRoot 'apps\admin_web'
$apiProject = Join-Path $repoRoot 'src\MentalHealth.Api\MentalHealth.Api.csproj'
$envPath = Join-Path $repoRoot '.env'
$composePath = Join-Path $repoRoot 'deploy\docker-compose.yml'
$certificatePath = Join-Path $repoRoot 'deploy\certs\server.pfx'
$apiProcess = $null
$webProcess = $null
$flutterProcess = $null
$playwrightOpened = $false
$reversePort = $null
$taskDirectory = $null
$playwrightSession = "task11-$([Guid]::NewGuid().ToString('N'))"

function Read-LocalValues
{
    $values = @{}
    foreach ($line in [IO.File]::ReadAllLines($envPath))
    {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#'))
        {
            continue
        }
        $parts = $line.Split('=', 2)
        if ($parts.Count -eq 2) { $values[$parts[0].Trim()] = $parts[1] }
    }
    return $values
}

function Get-LocalValue
{
    param([Parameter(Mandatory)][string]$Name)
    if ($localValues.ContainsKey($Name)) { return [string]$localValues[$Name] }
    return ''
}

function Get-LocalUserId
{
    param([Parameter(Mandatory)][string]$Email)
    $query = "SELECT ""Id"" FROM ""AspNetUsers"" WHERE ""Email"" = '$Email';"
    $output = & docker compose --env-file $envPath -f $composePath `
        exec -T postgres psql -U mental_health -d mental_health `
        -tA -v ON_ERROR_STOP=1 -c $query 2>&1
    if ($LASTEXITCODE -ne 0) { throw '无法读取本机测试账号 ID。' }
    $userId = [Guid]::Empty
    if (-not [Guid]::TryParse(($output -join '').Trim(), [ref]$userId))
    {
        throw '本机测试账号没有准备完成。'
    }
    return $userId
}

function Get-FreePort
{
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try { return $listener.LocalEndpoint.Port }
    finally { $listener.Stop() }
}

function Start-WithEnvironment
{
    param(
        [hashtable]$Environment,
        [string]$FilePath,
        [string[]]$ArgumentList,
        [string]$WorkingDirectory,
        [string]$StandardOutput,
        [string]$StandardError
    )

    $previous = @{}
    foreach ($entry in $Environment.GetEnumerator())
    {
        $existing = Get-Item "Env:$($entry.Key)" -ErrorAction SilentlyContinue
        $previous[$entry.Key] = if ($null -eq $existing) { $null } else { $existing.Value }
        Set-Item "Env:$($entry.Key)" $entry.Value
    }
    try
    {
        return Start-Process -FilePath $FilePath -ArgumentList $ArgumentList `
            -WorkingDirectory $WorkingDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput $StandardOutput -RedirectStandardError $StandardError
    }
    finally
    {
        foreach ($entry in $previous.GetEnumerator())
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
}

function Wait-HttpOk
{
    param([string]$Uri, [Diagnostics.Process]$Process, [int]$Attempts = 120)
    foreach ($attempt in 1..$Attempts)
    {
        if ($Process.HasExited) { throw "服务提前退出：$($Process.ExitCode)" }
        try
        {
            $response = Invoke-WebRequest -UseBasicParsing -SkipCertificateCheck `
                -Uri $Uri -TimeoutSec 1
            if ($response.StatusCode -eq 200) { return }
        }
        catch { Start-Sleep -Milliseconds 250 }
    }
    throw "服务没有按时就绪：$Uri"
}

function Invoke-Api
{
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body,
        [string]$Token
    )

    $parameters = @{
        Method = $Method
        Uri = "$script:apiBaseUrl$Path"
        SkipCertificateCheck = $true
        ContentType = 'application/json'
    }
    if (-not [string]::IsNullOrWhiteSpace($Token))
    {
        $parameters.Headers = @{ Authorization = "Bearer $Token" }
    }
    if ($null -ne $Body)
    {
        $parameters.Body = $Body | ConvertTo-Json -Depth 8 -Compress
    }
    return Invoke-RestMethod @parameters
}

function Invoke-Playwright
{
    param([string[]]$Arguments)
    $allArguments = @("-s=$playwrightSession") + $Arguments
    Push-Location $taskDirectory
    try
    {
        $output = & $bashPath $playwrightWrapper @allArguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally
    {
        Pop-Location
    }
    if ($exitCode -ne 0)
    {
        $safeTail = ($output | Select-Object -Last 8) -join [Environment]::NewLine
        throw "浏览器验收命令失败：$($Arguments[0])。$([Environment]::NewLine)$safeTail"
    }
    return $output
}

if ([string]::IsNullOrWhiteSpace($PlaywrightWrapper))
{
    throw '请用 -PlaywrightWrapper 指定 playwright_cli.sh。'
}
if ([string]::IsNullOrWhiteSpace($BashPath))
{
    $gitCommand = Get-Command git.exe -ErrorAction Stop
    $gitRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $gitCommand.Source) '..'))
    $BashPath = @(
        (Join-Path $gitRoot 'bin\bash.exe'),
        (Join-Path $gitRoot 'usr\bin\bash.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($BashPath))
    {
        throw '没有找到 Git Bash，请用 -BashPath 指定 bash.exe。'
    }
}
if (-not (Test-Path -LiteralPath $envPath -PathType Leaf))
{
    throw '缺少本机 .env，请先运行 scripts/Initialize-LocalSecrets.ps1。'
}
foreach ($requiredFile in @($certificatePath, $PlaywrightWrapper, $BashPath))
{
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf))
    {
        throw "缺少验收文件：$requiredFile"
    }
}

$localValues = Read-LocalValues
foreach ($required in @(
    'MH_POSTGRES_PASSWORD',
    'MH_JWT_SIGNING_KEY',
    'MH_DEMO_CERT_PASSWORD',
    'MH_CLIENT_PHONE',
    'MH_ADMIN_PHONE'))
{
    if (-not $localValues.ContainsKey($required) -or
        [string]::IsNullOrWhiteSpace($localValues[$required]))
    {
        throw "本机 .env 缺少 $required。"
    }
}

Import-Module (Join-Path $PSScriptRoot 'LocalTestJwt.psm1') -Force

try
{
    . (Join-Path $PSScriptRoot 'Use-Toolchain.ps1')
    $deviceState = (& adb -s $DeviceId get-state 2>$null).Trim()
    if ($deviceState -ne 'device') { throw "Android 设备不可用：$DeviceId" }
    if (-not $DeviceId.StartsWith('emulator-') -and
        [string]::IsNullOrWhiteSpace($LanIp))
    {
        throw '真机验收请用 -LanIp 指定服务器的局域网 IPv4 地址。'
    }

    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $taskDirectory = [IO.Path]::GetFullPath(
        (Join-Path $tempBase "mental-health-task11-$([Guid]::NewGuid().ToString('N'))"))
    if (-not $taskDirectory.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase))
    {
        throw 'Task 11 临时目录不在系统临时目录内。'
    }
    [IO.Directory]::CreateDirectory($taskDirectory) | Out-Null
    $objectStorageRoot = Join-Path $taskDirectory 'object-storage'
    $apiPort = Get-FreePort
    $webPort = Get-FreePort
    $script:apiBaseUrl = "https://127.0.0.1:$apiPort/api/v1/"

    $apiEnvironment = @{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'ConnectionStrings__MentalHealth' = "Host=127.0.0.1;Port=54329;Database=mental_health;Username=mental_health;Password=$($localValues['MH_POSTGRES_PASSWORD'])"
        'ConnectionStrings__Redis' = '127.0.0.1:56379'
        'LocalObjectStorage__RootPath' = $objectStorageRoot
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
        'Kestrel__Certificates__Default__Path' = $certificatePath
        'Kestrel__Certificates__Default__Password' = $localValues['MH_DEMO_CERT_PASSWORD']
        'Cors__AllowedOrigins__0' = "http://127.0.0.1:$webPort"
    }
    $apiProcess = Start-WithEnvironment -Environment $apiEnvironment `
        -FilePath (Get-Command dotnet).Source `
        -ArgumentList @(
            'run', '--project', $apiProject, '--no-build', '--no-launch-profile',
            '--urls', "https://0.0.0.0:$apiPort") `
        -WorkingDirectory $repoRoot `
        -StandardOutput (Join-Path $taskDirectory 'api.out.log') `
        -StandardError (Join-Path $taskDirectory 'api.err.log')
    Wait-HttpOk -Uri "https://127.0.0.1:$apiPort/health" -Process $apiProcess
    if ($DeviceId.StartsWith('emulator-'))
    {
        & adb -s $DeviceId reverse "tcp:$apiPort" "tcp:$apiPort" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '建立 Android 本机端口映射失败。' }
        $reversePort = $apiPort
    }

    $userToken = New-LocalTestJwt `
        -UserId (Get-LocalUserId 'abc@qq.com') `
        -Role User `
        -BusinessId ([Guid]'10000000-0000-0000-0000-000000000001') `
        -SigningKey $localValues['MH_JWT_SIGNING_KEY']
    $counselorToken = New-LocalTestJwt `
        -UserId (Get-LocalUserId 'counselor@demo.local') `
        -Role Counselor `
        -BusinessId ([Guid]'20000000-0000-0000-0000-000000000001') `
        -SigningKey $localValues['MH_JWT_SIGNING_KEY']
    foreach ($kind in @('Service', 'Recording', 'AiAnalysis'))
    {
        Invoke-Api -Method POST -Path 'consents' -Token $userToken -Body @{
            kind = $kind
            textVersion = 'ui-copy-v1'
        } | Out-Null
    }
    $order = Invoke-Api -Method POST -Path 'orders' -Token $userToken -Body @{
        planId = '30000000-0000-0000-0000-000000000002'
        idempotencyKey = "task11-order-$([Guid]::NewGuid().ToString('N'))"
    }
    Invoke-Api -Method POST -Path "orders/$($order.id)/confirm" `
        -Token $userToken -Body @{} | Out-Null
    $session = Invoke-Api -Method POST -Path 'consultations' -Token $userToken -Body @{
        orderId = $order.id
        assignedPractitionerId = '20000000-0000-0000-0000-000000000001'
        scheduledAt = [DateTimeOffset]::UtcNow.AddMinutes(1).ToString('O')
        idempotencyKey = "task11-session-$([Guid]::NewGuid().ToString('N'))"
    }
    Invoke-Api -Method POST -Path "consultations/$($session.id)/start" `
        -Token $userToken -Body @{} | Out-Null

    $webEnvironment = @{
        'VITE_API_BASE_URL' = $script:apiBaseUrl
        'VITE_RTC_HUB_URL' = "https://127.0.0.1:$apiPort/hubs/rtc"
        'VITE_CHAT_HUB_URL' = "https://127.0.0.1:$apiPort/hubs/chat"
    }
    $viteEntry = Join-Path $webRoot 'node_modules\vite\bin\vite.js'
    $webProcess = Start-WithEnvironment -Environment $webEnvironment `
        -FilePath (Get-Command node.exe).Source `
        -ArgumentList @($viteEntry, '--host', '127.0.0.1', '--port', "$webPort", '--strictPort') `
        -WorkingDirectory $webRoot `
        -StandardOutput (Join-Path $taskDirectory 'web.out.log') `
        -StandardError (Join-Path $taskDirectory 'web.err.log')
    Wait-HttpOk -Uri "http://127.0.0.1:$webPort" -Process $webProcess

    $androidApiHost = if ($DeviceId.StartsWith('emulator-')) { '127.0.0.1' } else { $LanIp }
    $definesPath = Join-Path $taskDirectory 'defines.json'
    $defines = @{
        API_BASE_URL = "https://$androidApiHost`:$apiPort/api/v1/"
        RTC_HUB_URL = "https://$androidApiHost`:$apiPort/hubs/rtc"
        USER_ACCESS_TOKEN = $userToken
        SESSION_ID = $session.id
    } | ConvertTo-Json
    [IO.File]::WriteAllText($definesPath, $defines, [Text.UTF8Encoding]::new($false))

    $flutterProcess = Start-Process -FilePath (Get-Command flutter.bat).Source `
        -ArgumentList @(
            'test', 'integration_test/task11_video_test.dart', '-d', $DeviceId,
            "--dart-define-from-file=$definesPath") `
        -WorkingDirectory $mobileRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $taskDirectory 'flutter.out.log') `
        -RedirectStandardError (Join-Path $taskDirectory 'flutter.err.log')
    $androidReady = $false
    foreach ($attempt in 1..720)
    {
        if ($flutterProcess.HasExited)
        {
            $flutterTail = @(
                Get-Content -LiteralPath (Join-Path $taskDirectory 'flutter.out.log') `
                    -Tail 100 -ErrorAction SilentlyContinue
                Get-Content -LiteralPath (Join-Path $taskDirectory 'flutter.err.log') `
                    -Tail 100 -ErrorAction SilentlyContinue
            ) -join [Environment]::NewLine
            throw "Android 视频验收提前退出：$($flutterProcess.ExitCode)$([Environment]::NewLine)$flutterTail"
        }
        $flutterLog = Get-Content -LiteralPath (Join-Path $taskDirectory 'flutter.out.log') `
            -Raw -ErrorAction SilentlyContinue
        if ($flutterLog -match 'TASK11_ANDROID_WAITING')
        {
            $androidReady = $true
            break
        }
        Start-Sleep -Milliseconds 250
    }
    if (-not $androidReady) { throw 'Android 视频验收在 180 秒内未就绪。' }
    & adb -s $DeviceId shell pm grant `
        com.example.mentalhealth.mobile_flutter android.permission.CAMERA 2>$null
    if ($LASTEXITCODE -ne 0) { throw '授予 Android 相机权限失败。' }
    & adb -s $DeviceId shell pm grant `
        com.example.mentalhealth.mobile_flutter android.permission.RECORD_AUDIO 2>$null
    if ($LASTEXITCODE -ne 0) { throw '授予 Android 麦克风权限失败。' }

    $playwrightConfig = Join-Path $taskDirectory 'playwright-cli.json'
    $config = @{
        browser = @{
            launchOptions = @{
                headless = $true
                args = @(
                    '--use-fake-ui-for-media-stream',
                    '--use-fake-device-for-media-stream',
                    '--autoplay-policy=no-user-gesture-required')
            }
            contextOptions = @{
                ignoreHTTPSErrors = $true
                viewport = @{ width = 1280; height = 800 }
            }
        }
    } | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText(
        $playwrightConfig,
        $config,
        [Text.UTF8Encoding]::new($false))
    $configForNode = $playwrightConfig.Replace('\', '/')
    Invoke-Playwright -Arguments @(
        'open', "http://127.0.0.1:$webPort", '--browser', 'chrome',
        '--config', $configForNode) | Out-Null
    $playwrightOpened = $true
    Invoke-Playwright -Arguments @(
        'sessionstorage-set', 'mh_access_token', $counselorToken) | Out-Null
    Invoke-Playwright -Arguments @('reload') | Out-Null
    Invoke-Playwright -Arguments @('snapshot') | Out-Null

    $sessionId = $session.id
    $joinCode = @"
async (page) => {
  await page.getByRole('button', { name: '视频咨询' }).click();
  await page.getByTestId('video-session-id').fill('$sessionId');
  await page.locator('form.video-connect button[type="submit"]').click();
  await page.locator('[data-testid="video-room"][data-room-ready="true"]').waitFor({ timeout: 10000 });
}
"@
    $joinCodePath = Join-Path $taskDirectory 'join-video.js'
    [IO.File]::WriteAllText(
        $joinCodePath,
        $joinCode,
        [Text.UTF8Encoding]::new($false))
    Invoke-Playwright -Arguments @(
        'run-code', '--filename', $joinCodePath.Replace('\', '/')) | Out-Null
    Invoke-Api -Method POST -Path "consultations/$sessionId/messages" `
        -Token $counselorToken -Body @{
            text = '网页视频端已就绪'
            clientMessageId = 'task11-web-ready'
        } | Out-Null

    $connectedCode = @"
async (page) => {
  await page.getByTestId('rtc-status').filter({ hasText: '视频已连接' }).waitFor({ timeout: 20000 });
}
"@
    $connectedCodePath = Join-Path $taskDirectory 'wait-connected.js'
    [IO.File]::WriteAllText(
        $connectedCodePath,
        $connectedCode,
        [Text.UTF8Encoding]::new($false))
    Invoke-Playwright -Arguments @(
        'run-code', '--filename', $connectedCodePath.Replace('\', '/')) | Out-Null
    $evidenceDirectory = Join-Path $repoRoot 'tests\output\playwright'
    [IO.Directory]::CreateDirectory($evidenceDirectory) | Out-Null
    $screenshotPath = Join-Path $evidenceDirectory 'task11-video-connected.png'
    Invoke-Playwright -Arguments @(
        'screenshot', '--filename', $screenshotPath.Replace('\', '/'), '--full-page') | Out-Null
    Invoke-Api -Method POST -Path "consultations/$sessionId/messages" `
        -Token $counselorToken -Body @{
            text = '网页已确认连接'
            clientMessageId = 'task11-web-confirmed'
        } | Out-Null

    if (-not $flutterProcess.WaitForExit(120000))
    {
        throw 'Android 视频验收没有按时结束。'
    }
    if ($flutterProcess.ExitCode -ne 0)
    {
        $flutterTail = @(
            Get-Content -LiteralPath (Join-Path $taskDirectory 'flutter.out.log') `
                -Tail 100 -ErrorAction SilentlyContinue
            Get-Content -LiteralPath (Join-Path $taskDirectory 'flutter.err.log') `
                -Tail 100 -ErrorAction SilentlyContinue
        ) -join [Environment]::NewLine
        throw "Android 视频验收失败，退出码：$($flutterProcess.ExitCode)$([Environment]::NewLine)$flutterTail"
    }
    $storedMedia = @(Get-ChildItem -LiteralPath $objectStorageRoot `
        -Recurse -File -Filter '*.media' -ErrorAction SilentlyContinue)
    if ($storedMedia.Count -ne 1 -or $storedMedia[0].Length -le 0)
    {
        throw "媒体上传验收失败，完整媒体文件数：$($storedMedia.Count)"
    }

    Write-Host "Android 与网页已完成一次局域网视频连接，媒体字节数：$($storedMedia[0].Length)。"
    Write-Host "截图：$screenshotPath"
}
finally
{
    if ($playwrightOpened)
    {
        try { Invoke-Playwright -Arguments @('close') | Out-Null } catch {}
    }
    if ($null -ne $reversePort)
    {
        & adb -s $DeviceId reverse --remove "tcp:$reversePort" 2>$null
    }
    if ($null -ne $flutterProcess -and -not $flutterProcess.HasExited)
    {
        & adb -s $DeviceId shell am force-stop `
            com.example.mentalhealth.mobile_flutter 2>$null
    }
    foreach ($process in @($flutterProcess, $webProcess, $apiProcess))
    {
        if ($null -ne $process -and -not $process.HasExited)
        {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
    }
    if ($null -ne $taskDirectory -and (Test-Path -LiteralPath $taskDirectory))
    {
        $checkedTaskDirectory = [IO.Path]::GetFullPath($taskDirectory)
        $checkedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $checkedTaskDirectory.StartsWith(
            $checkedTemp,
            [StringComparison]::OrdinalIgnoreCase))
        {
            throw 'Task 11 临时目录不在系统临时目录内，停止清理。'
        }
        try
        {
            Remove-Item -LiteralPath $checkedTaskDirectory -Recurse -Force
        }
        catch
        {
            Write-Warning "Task 11 临时目录清理失败：$checkedTaskDirectory"
        }
    }
}
