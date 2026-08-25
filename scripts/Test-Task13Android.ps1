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
$apiPort = $null
$reverseCreated = $false
$networkMutationStarted = $false
$previousAirplaneMode = $null
$previousWifiEnabled = $null
$restoreFailure = $null
$primaryFailure = $null
$cleanupFailure = $null

function Invoke-AdbChecked {
    param([string[]]$Arguments)
    $output = & adb -s $DeviceId @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "adb $($Arguments -join ' ') 执行失败：$($output -join [Environment]::NewLine)"
    }
    return $output
}

function Read-AirplaneMode {
    $value = ((Invoke-AdbChecked -Arguments @(
        'shell', 'settings', 'get', 'global', 'airplane_mode_on'
    )) -join '').Trim()
    if ($value -notin @('0', '1')) {
        throw "无法确认模拟器飞行模式状态：$value"
    }
    return $value
}

function Read-WifiEnabled {
    $status = (Invoke-AdbChecked -Arguments @(
        'shell', 'cmd', 'wifi', 'status'
    )) -join [Environment]::NewLine
    if ($status -match '^Wifi is enabled') { return $true }
    if ($status -match '^Wifi is disabled') { return $false }
    throw '无法确认模拟器 Wi-Fi 状态。'
}

function Set-AirplaneMode {
    param([bool]$Enabled)
    $action = if ($Enabled) { 'enable' } else { 'disable' }
    Invoke-AdbChecked -Arguments @(
        'shell', 'cmd', 'connectivity', 'airplane-mode', $action
    ) | Out-Null
}

function Set-WifiEnabled {
    param([bool]$Enabled)
    $action = if ($Enabled) { 'enable' } else { 'disable' }
    Invoke-AdbChecked -Arguments @('shell', 'svc', 'wifi', $action) | Out-Null
}

function Wait-NetworkState {
    param(
        [string]$AirplaneMode,
        [bool]$WifiEnabled,
        [int]$Attempts = 60
    )
    foreach ($attempt in 1..$Attempts) {
        try {
            if ((Read-AirplaneMode) -eq $AirplaneMode -and
                (Read-WifiEnabled) -eq $WifiEnabled) {
                return $true
            }
        }
        catch {
            # Android services can briefly reject reads while changing state.
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf)) {
    throw '缺少本机 .env，请先运行 scripts/Initialize-LocalSecrets.ps1。'
}

$localValues = @{}
foreach ($line in Get-Content -LiteralPath $envPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
        continue
    }
    $pair = $line -split '=', 2
    if ($pair.Count -eq 2) { $localValues[$pair[0].Trim()] = $pair[1] }
}
foreach ($required in @(
    'MH_POSTGRES_PASSWORD',
    'MH_JWT_SIGNING_KEY',
    'MH_DEMO_INITIAL_PASSWORD'
)) {
    if (-not $localValues.ContainsKey($required) -or
        [string]::IsNullOrWhiteSpace($localValues[$required])) {
        throw "本机 .env 缺少 $required。"
    }
}

try {
    . (Join-Path $PSScriptRoot 'Use-Toolchain.ps1')
    $deviceState = ((& adb -s $DeviceId get-state 2>$null) -join '').Trim()
    if ($deviceState -ne 'device') {
        throw "Android 设备不可用：$DeviceId"
    }
    $isEmulator = ((Invoke-AdbChecked -Arguments @(
        'shell', 'getprop', 'ro.kernel.qemu'
    )) -join '').Trim()
    if ($isEmulator -ne '1') {
        throw 'Task 13 只允许自动切换 Android 模拟器的网络状态。'
    }

    # 先保存状态并验证恢复命令存在，之后才允许改网络。
    $previousAirplaneMode = Read-AirplaneMode
    $previousWifiEnabled = Read-WifiEnabled
    # Android 16 prints valid help but returns a non-zero code for this command.
    $connectivityHelp = (& adb -s $DeviceId shell cmd connectivity help 2>&1) -join `
        [Environment]::NewLine
    if ($connectivityHelp -notmatch 'airplane-mode \[enable\|disable\]') {
        throw '模拟器不支持可恢复的飞行模式命令。'
    }
    $queriedAirplaneMode = ((Invoke-AdbChecked -Arguments @(
        'shell', 'cmd', 'connectivity', 'airplane-mode'
    )) -join '').Trim()
    $expectedAirplaneMode = if ($previousAirplaneMode -eq '1') {
        'enabled'
    }
    else {
        'disabled'
    }
    if ($queriedAirplaneMode -ne $expectedAirplaneMode) {
        throw '飞行模式的两个只读来源不一致，停止验收。'
    }

    $definesDirectory = Join-Path `
        ([IO.Path]::GetTempPath()) `
        "mental-health-v1-task13-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($definesDirectory) | Out-Null
    $networkStatePath = Join-Path $definesDirectory 'device-network-state.json'
    $networkState = @{
        deviceId = $DeviceId
        airplaneMode = $previousAirplaneMode
        wifiEnabled = $previousWifiEnabled
        restoreAirplaneAction = if ($previousAirplaneMode -eq '1') {
            'enable'
        }
        else {
            'disable'
        }
        restoreWifiAction = if ($previousWifiEnabled) { 'enable' } else { 'disable' }
    } | ConvertTo-Json
    [IO.File]::WriteAllText(
        $networkStatePath,
        $networkState,
        [Text.UTF8Encoding]::new($false))
    $savedNetworkState = Get-Content -LiteralPath $networkStatePath -Raw |
        ConvertFrom-Json
    if ($savedNetworkState.deviceId -ne $DeviceId -or
        $savedNetworkState.airplaneMode -ne $previousAirplaneMode -or
        [bool]$savedNetworkState.wifiEnabled -ne $previousWifiEnabled) {
        throw '模拟器网络恢复副本回读不一致，停止验收。'
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
        $apiOutputPath = Join-Path $definesDirectory 'api.out.log'
        $apiErrorPath = Join-Path $definesDirectory 'api.err.log'
        $apiProcess = Start-Process `
            -FilePath (Get-Command dotnet).Source `
            -ArgumentList @(
                'run', '--project', $apiProject, '--no-build',
                '--no-launch-profile', '--urls', "http://127.0.0.1:$apiPort"
            ) `
            -WorkingDirectory $repoRoot `
            -WindowStyle Hidden `
            -RedirectStandardOutput $apiOutputPath `
            -RedirectStandardError $apiErrorPath `
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
            $errorTail = if (Test-Path -LiteralPath $apiErrorPath) {
                (Get-Content -LiteralPath $apiErrorPath -Tail 12) -join `
                    [Environment]::NewLine
            }
            else {
                ''
            }
            throw "本机 API 启动失败，退出码：$($apiProcess.ExitCode)。$([Environment]::NewLine)$errorTail"
        }
        try {
            $health = Invoke-WebRequest -UseBasicParsing `
                -Uri "http://127.0.0.1:$apiPort/health" -TimeoutSec 1
            if ($health.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $ready) { throw '本机 API 在 30 秒内未就绪。' }

    Invoke-AdbChecked -Arguments @(
        'reverse', "tcp:$apiPort", "tcp:$apiPort"
    ) | Out-Null
    $reverseCreated = $true

    $networkMutationStarted = $true
    Set-AirplaneMode -Enabled $true
    Start-Sleep -Seconds 2
    Set-WifiEnabled -Enabled $false
    if (-not (Wait-NetworkState -AirplaneMode '1' -WifiEnabled $false)) {
        throw '模拟器没有进入飞行模式，停止验收。'
    }
    & adb -s $DeviceId shell ping -c 1 -W 1 8.8.8.8 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        throw '模拟器仍可访问外网，停止离线语音验收。'
    }

    $definesPath = Join-Path $definesDirectory 'defines.json'
    $defines = @{
        API_BASE_URL = "http://127.0.0.1:$apiPort/api/v1/"
        DEMO_USER_EMAIL = 'user@demo.local'
        DEMO_USER_PASSWORD = $localValues['MH_DEMO_INITIAL_PASSWORD']
    } | ConvertTo-Json
    [IO.File]::WriteAllText(
        $definesPath,
        $defines,
        [Text.UTF8Encoding]::new($false))

    Push-Location $mobileRoot
    try {
        & flutter test integration_test/task13_ai_consultation_test.dart `
            -d $DeviceId "--dart-define-from-file=$definesPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Android AI 咨询验收失败，退出码：$LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host 'Android 模拟器在飞行模式下完成本地语音或提示音降级，并通过危机页锁定检查。'
}
catch {
    $primaryFailure = $_
}
finally {
    if ($reverseCreated -and $null -ne $apiPort -and
        (Get-Command adb -ErrorAction SilentlyContinue)) {
        & adb -s $DeviceId reverse --remove "tcp:$apiPort" 2>$null | Out-Null
    }

    try {
        if ($networkMutationStarted -and
            $null -ne $previousAirplaneMode -and
            $null -ne $previousWifiEnabled -and
            (Get-Command adb -ErrorAction SilentlyContinue)) {
            Set-AirplaneMode -Enabled ($previousAirplaneMode -eq '1')
            Start-Sleep -Seconds 2
            Set-WifiEnabled -Enabled $previousWifiEnabled
            if (-not (Wait-NetworkState `
                -AirplaneMode $previousAirplaneMode `
                -WifiEnabled $previousWifiEnabled)) {
                throw '模拟器网络状态没有恢复到验收前的值。'
            }
        }
    }
    catch {
        $restoreFailure = $_.Exception.Message
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
    if ($null -ne $apiProcess) {
        $apiProcess.Dispose()
        $apiProcess = $null
    }

    if ($null -ne $definesDirectory -and
        (Test-Path -LiteralPath $definesDirectory)) {
        $resolvedDirectory = [IO.Path]::GetFullPath($definesDirectory)
        $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $resolvedDirectory.StartsWith(
            $resolvedTemp,
            [StringComparison]::OrdinalIgnoreCase)) {
            throw '临时验收目录不在系统临时目录中，停止清理。'
        }

        if ($null -ne $restoreFailure) {
            foreach ($fileName in @('defines.json', 'api.out.log', 'api.err.log')) {
                $sensitivePath = Join-Path $resolvedDirectory $fileName
                if (Test-Path -LiteralPath $sensitivePath -PathType Leaf) {
                    try {
                        Remove-Item -LiteralPath $sensitivePath -Force
                    }
                    catch {
                        $cleanupFailure =
                            "临时验收文件清理失败：$sensitivePath。$($_.Exception.Message)"
                        break
                    }
                }
            }
        }
        else {
            foreach ($attempt in 1..20) {
                try {
                    Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
                    break
                }
                catch {
                    if ($attempt -eq 20) {
                        $cleanupFailure =
                            "临时验收目录清理失败：$resolvedDirectory。$($_.Exception.Message)"
                        break
                    }
                    Start-Sleep -Milliseconds 250
                }
            }
        }
    }

    if ($null -ne $restoreFailure) {
        $recoveryPath = if ($null -ne $definesDirectory) {
            Join-Path $definesDirectory 'device-network-state.json'
        }
        else {
            '未建立恢复文件'
        }
        $primaryMessage = if ($null -ne $primaryFailure) {
            "；原始失败：$($primaryFailure.Exception.Message)"
        }
        else {
            ''
        }
        $cleanupMessage = if ($null -ne $cleanupFailure) {
            "；$cleanupFailure"
        }
        else {
            ''
        }
        throw "$restoreFailure；恢复记录：$recoveryPath$primaryMessage$cleanupMessage"
    }
    if ($null -ne $cleanupFailure) {
        $primaryMessage = if ($null -ne $primaryFailure) {
            "；原始失败：$($primaryFailure.Exception.Message)"
        }
        else {
            ''
        }
        throw "$cleanupFailure$primaryMessage"
    }
    if ($null -ne $primaryFailure) {
        throw $primaryFailure
    }
}
