#requires -Version 7.0

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$workerProject = Join-Path $repoRoot 'src\MentalHealth.AnalysisWorker\MentalHealth.AnalysisWorker.csproj'

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

if ([string]::IsNullOrWhiteSpace($localValues['MH_POSTGRES_PASSWORD'])) {
    throw '本机 .env 缺少 MH_POSTGRES_PASSWORD。'
}

$environmentName = 'ConnectionStrings__MentalHealth'
$previous = Get-Item "Env:$environmentName" -ErrorAction SilentlyContinue
try {
    Set-Item "Env:$environmentName" (
        "Host=127.0.0.1;Port=54329;Database=mental_health;" +
        "Username=mental_health;Password=$($localValues['MH_POSTGRES_PASSWORD'])")
    & dotnet run --project $workerProject --no-launch-profile
    if ($LASTEXITCODE -ne 0) {
        throw "分析任务进程退出，退出码：$LASTEXITCODE"
    }
}
finally {
    if ($null -eq $previous) {
        Remove-Item "Env:$environmentName" -ErrorAction SilentlyContinue
    }
    else {
        Set-Item "Env:$environmentName" $previous.Value
    }
}
