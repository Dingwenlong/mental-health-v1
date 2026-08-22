[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'

if (Test-Path -LiteralPath $envPath)
{
    throw '.env 已存在；脚本不会覆盖。'
}

function New-LocalSecret
{
    param([int]$Bytes = 48)

    return [Convert]::ToBase64String(
        [Security.Cryptography.RandomNumberGenerator]::GetBytes($Bytes))
}

function New-LocalDemoPassword
{
    return "Aa1!$(New-LocalSecret -Bytes 24)"
}

$content = @(
    "MH_POSTGRES_PASSWORD=$(New-LocalSecret -Bytes 32)"
    "MH_JWT_SIGNING_KEY=$(New-LocalSecret -Bytes 64)"
    "MH_DEMO_INITIAL_PASSWORD=$(New-LocalDemoPassword)"
) -join [Environment]::NewLine

[IO.File]::WriteAllText(
    $envPath,
    $content + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

Write-Host '已创建本机 .env，未输出其中的值。'
