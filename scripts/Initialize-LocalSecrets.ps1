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

$content = @(
    "MH_POSTGRES_PASSWORD=$(New-LocalSecret -Bytes 32)"
    "MH_JWT_SIGNING_KEY=$(New-LocalSecret -Bytes 64)"
    "MH_DEMO_CERT_PASSWORD=$(New-LocalSecret -Bytes 48)"
    'MH_ALIYUN_ACCESS_KEY_ID='
    'MH_ALIYUN_ACCESS_KEY_SECRET='
    'MH_ALIYUN_CAPTCHA_EKEY='
    'MH_ALIYUN_SMS_SIGN_NAME='
    'MH_ALIYUN_SMS_TEMPLATE_CODE='
    ('MH_CLIENT_PHONE' + '=')
    ('MH_ADMIN_PHONE' + '=')
) -join [Environment]::NewLine

[IO.File]::WriteAllText(
    $envPath,
    $content + [Environment]::NewLine,
    [Text.UTF8Encoding]::new($false))

Write-Host '已创建本机 .env，未输出其中的值。请填写阿里云配置和两个登录手机号。'
