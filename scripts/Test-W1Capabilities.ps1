[CmdletBinding()]
param(
    [string]$DeviceId = 'emulator-5554'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$mobileRoot = Join-Path $repoRoot 'apps\mobile_flutter'
$apiProject = Join-Path $repoRoot 'src\MentalHealth.Api\MentalHealth.Api.csproj'
$apkPath = Join-Path $mobileRoot 'build\app\outputs\flutter-apk\app-debug.apk'
$packageName = 'com.example.mentalhealth.mobile_flutter'
$cameraPermission = 'android.permission.CAMERA'
$apiProcess = $null
$apiEnvironment = @{
    'ASPNETCORE_ENVIRONMENT' = 'Development'
    'ConnectionStrings__MentalHealth' =
        'Host=127.0.0.1;Port=1;Database=probe;Username=probe;Password=synthetic-probe-password'
    'ConnectionStrings__Redis' = '127.0.0.1:1,abortConnect=false'
    'LocalObjectStorage__RootPath' = Join-Path $repoRoot 'tests\output\w1\object-storage'
    'Jwt__Issuer' = 'mental-health-v1-probe'
    'Jwt__Audience' = 'mental-health-v1-probe'
    'Jwt__SigningKey' = 'synthetic-w1-probe-signing-key-with-at-least-32-bytes'
    'Jwt__AccessTokenMinutes' = '15'
    'Database__InitializeOnStartup' = 'false'
    'IdentitySeed__Enabled' = 'false'
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath 执行失败，退出码：$LASTEXITCODE"
    }
}

function Test-AppInstalled {
    $packages = & adb -s $DeviceId shell pm list packages $packageName
    return $LASTEXITCODE -eq 0 -and $packages -contains "package:$packageName"
}

try {
    . (Join-Path $PSScriptRoot 'Use-Toolchain.ps1')

    $deviceState = (& adb -s $DeviceId get-state 2>$null).Trim()
    if ($deviceState -ne 'device') {
        throw "Android 设备不可用：$DeviceId"
    }

    $portProbe = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        0
    )
    $portProbe.Start()
    $apiPort = $portProbe.LocalEndpoint.Port
    $portProbe.Stop()

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
            $response = Invoke-WebRequest `
                -UseBasicParsing `
                -Uri "http://127.0.0.1:$apiPort/health" `
                -TimeoutSec 1
            if ($response.StatusCode -eq 200) {
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

    Push-Location $mobileRoot
    try {
        Invoke-Checked -FilePath 'flutter' -Arguments @('build', 'apk', '--debug')
        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'install', '-r', $apkPath
        )

        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'shell', 'pm', 'clear-permission-flags',
            $packageName, $cameraPermission, 'user-set'
        )
        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'shell', 'pm', 'clear-permission-flags',
            $packageName, $cameraPermission, 'user-fixed'
        )
        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'shell', 'pm', 'revoke', '--user', '0',
            $packageName, $cameraPermission
        )
        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'shell', 'pm', 'set-permission-flags',
            $packageName, $cameraPermission, 'user-set'
        )
        Invoke-Checked -FilePath 'adb' -Arguments @(
            '-s', $DeviceId, 'shell', 'pm', 'set-permission-flags',
            $packageName, $cameraPermission, 'user-fixed'
        )

        Invoke-Checked -FilePath 'flutter' -Arguments @(
            'test',
            'integration_test/w1_capability_probe_test.dart',
            '-d', $DeviceId,
            "--dart-define=PROBE_HUB_URL=http://10.0.2.2:$apiPort/hubs/development-probe"
        )
    }
    finally {
        Pop-Location
    }
}
finally {
    if (Get-Command adb -ErrorAction SilentlyContinue) {
        if (Test-AppInstalled) {
            & adb -s $DeviceId shell cmd appops reset --user 0 $packageName | Out-Null
            & adb -s $DeviceId shell pm clear-permission-flags `
                $packageName $cameraPermission user-set | Out-Null
            & adb -s $DeviceId shell pm clear-permission-flags `
                $packageName $cameraPermission user-fixed | Out-Null
            & adb -s $DeviceId shell pm revoke --user 0 `
                $packageName $cameraPermission | Out-Null
        }
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
}
