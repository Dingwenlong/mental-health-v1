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

    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
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
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
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
