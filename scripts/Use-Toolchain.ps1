$ErrorActionPreference = 'Stop'

if ($MyInvocation.InvocationName -ne '.') {
    throw '请在仓库根目录使用点调用：. .\scripts\Use-Toolchain.ps1'
}

$flutterRoot = 'D:\Toolchains\flutter-3.47.0'
$androidStudioRoot = 'D:\Toolchains\android-studio-2026.1.3.8'
$androidSdkRoot = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$javaRoot = Join-Path $androidStudioRoot 'jbr'

$requiredFiles = @(
    (Join-Path $flutterRoot 'bin\flutter.bat'),
    (Join-Path $androidSdkRoot 'platform-tools\adb.exe'),
    (Join-Path $javaRoot 'bin\java.exe')
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "缺少工具链文件：$requiredFile"
    }
}

$env:FLUTTER_ROOT = $flutterRoot
$env:ANDROID_HOME = $androidSdkRoot
$env:ANDROID_SDK_ROOT = $androidSdkRoot
$env:JAVA_HOME = $javaRoot

$toolPaths = @(
    (Join-Path $flutterRoot 'bin'),
    (Join-Path $androidSdkRoot 'platform-tools'),
    (Join-Path $androidSdkRoot 'emulator'),
    (Join-Path $javaRoot 'bin')
)
$existingPaths = $env:Path -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$remainingPaths = $existingPaths | Where-Object { $_ -notin $toolPaths }
$env:Path = (@($toolPaths) + @($remainingPaths)) -join ';'

Write-Host '当前 PowerShell 已加载 Flutter、Android SDK 和 Java。'
