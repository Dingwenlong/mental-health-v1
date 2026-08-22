[CmdletBinding()]
param(
    [string]$LanIp,
    [string]$OpenSslPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repoRoot '.env'
$certRoot = Join-Path $repoRoot 'deploy\certs'
$androidRoot = Join-Path $repoRoot 'apps\mobile_flutter\android\app\src\main\res\raw'
$flutterAssetRoot = Join-Path $repoRoot 'apps\mobile_flutter\assets\certs'

if ([string]::IsNullOrWhiteSpace($OpenSslPath))
{
    $gitCommand = Get-Command git.exe -ErrorAction Stop
    $gitRoot = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $gitCommand.Source) '..'))
    $OpenSslPath = @(
        (Join-Path $gitRoot 'usr\bin\openssl.exe'),
        (Join-Path $gitRoot 'mingw64\bin\openssl.exe')
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($OpenSslPath) -or
    -not (Test-Path -LiteralPath $OpenSslPath -PathType Leaf))
{
    throw '找不到 Git 附带的 OpenSSL，请用 -OpenSslPath 明确指定。'
}

if (-not (Test-Path -LiteralPath $envPath -PathType Leaf))
{
    throw '缺少本机 .env，请先运行 scripts/Initialize-LocalSecrets.ps1。'
}

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
        if ($parts.Count -eq 2)
        {
            $values[$parts[0].Trim()] = $parts[1]
        }
    }

    return $values
}

function New-LocalSecret
{
    return [Convert]::ToBase64String(
        [Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
}

function Resolve-LanIp
{
    if (-not [string]::IsNullOrWhiteSpace($LanIp))
    {
        $parsed = [Net.IPAddress]::Parse($LanIp)
        if ($parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or
            [Net.IPAddress]::IsLoopback($parsed))
        {
            throw 'LanIp 必须是可供同一局域网设备访问的 IPv4 地址。'
        }

        return $parsed.IPAddressToString
    }

    $excluded = 'Loopback|vEthernet|Default Switch|WSL|Mihomo|TAP|TUN|VPN'
    $routes = Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' |
        Sort-Object RouteMetric, InterfaceMetric
    foreach ($route in $routes)
    {
        $candidate = Get-NetIPAddress -AddressFamily IPv4 `
            -InterfaceIndex $route.InterfaceIndex -ErrorAction SilentlyContinue |
            Where-Object {
                $_.AddressState -eq 'Preferred' -and
                $_.InterfaceAlias -notmatch $excluded -and
                $_.IPAddress -notlike '127.*' -and
                $_.IPAddress -notlike '169.254.*'
            } |
            Select-Object -First 1
        if ($null -ne $candidate)
        {
            return $candidate.IPAddress
        }
    }

    throw '没有找到可用的局域网 IPv4 地址；请使用 -LanIp 明确指定。'
}

$values = Read-LocalValues
if (-not $values.ContainsKey('MH_DEMO_CERT_PASSWORD') -or
    [string]::IsNullOrWhiteSpace($values['MH_DEMO_CERT_PASSWORD']))
{
    $generatedPassword = New-LocalSecret
    [IO.File]::AppendAllText(
        $envPath,
        "MH_DEMO_CERT_PASSWORD=$generatedPassword$([Environment]::NewLine)",
        [Text.UTF8Encoding]::new($false))
    $values['MH_DEMO_CERT_PASSWORD'] = $generatedPassword
}

$resolvedIp = Resolve-LanIp
[IO.Directory]::CreateDirectory($certRoot) | Out-Null
[IO.Directory]::CreateDirectory($androidRoot) | Out-Null
[IO.Directory]::CreateDirectory($flutterAssetRoot) | Out-Null

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$tempRoot = [IO.Path]::GetFullPath(
    (Join-Path $tempBase "mental-health-cert-$([Guid]::NewGuid().ToString('N'))"))
if (-not $tempRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase))
{
    throw '临时证书目录不在系统临时目录内。'
}
[IO.Directory]::CreateDirectory($tempRoot) | Out-Null

$rootKey = Join-Path $certRoot 'demo-root.key'
$rootCertificate = Join-Path $certRoot 'demo-root.crt'
$tempRootKey = Join-Path $tempRoot 'demo-root.key'
$tempRootCertificate = Join-Path $tempRoot 'demo-root.crt'
$serverConfig = Join-Path $tempRoot 'server.cnf'
$serverKey = Join-Path $tempRoot 'server.key'
$serverRequest = Join-Path $tempRoot 'server.csr'
$serverCertificate = Join-Path $tempRoot 'server.crt'
$serverBundle = Join-Path $tempRoot 'server.pfx'

try
{
    if ((Test-Path -LiteralPath $rootKey -PathType Leaf) -and
        (Test-Path -LiteralPath $rootCertificate -PathType Leaf))
    {
        Copy-Item -LiteralPath $rootKey -Destination $tempRootKey
        Copy-Item -LiteralPath $rootCertificate -Destination $tempRootCertificate
    }
    else
    {
        & $OpenSslPath req -x509 -newkey rsa:3072 -sha256 -days 3650 -nodes `
            -subj '/CN=Mental Health Demo Root' `
            -keyout $tempRootKey -out $tempRootCertificate
        if ($LASTEXITCODE -ne 0) { throw '生成演示根证书失败。' }
    }

    $config = @"
[req]
prompt = no
distinguished_name = subject
req_extensions = extensions

[subject]
CN = Mental Health Demo API

[extensions]
subjectAltName = @names
extendedKeyUsage = serverAuth
keyUsage = digitalSignature, keyEncipherment

[names]
DNS.1 = localhost
IP.1 = 127.0.0.1
IP.2 = $resolvedIp
IP.3 = 10.0.2.2
"@
    [IO.File]::WriteAllText(
        $serverConfig,
        $config,
        [Text.UTF8Encoding]::new($false))

    & $OpenSslPath req -new -newkey rsa:3072 -nodes -sha256 `
        -config $serverConfig -keyout $serverKey -out $serverRequest
    if ($LASTEXITCODE -ne 0) { throw '生成演示服务器证书请求失败。' }

    & $OpenSslPath x509 -req -sha256 -days 90 `
        -in $serverRequest -CA $tempRootCertificate -CAkey $tempRootKey `
        -CAcreateserial -extensions extensions -extfile $serverConfig `
        -out $serverCertificate
    if ($LASTEXITCODE -ne 0) { throw '签发演示服务器证书失败。' }

    $env:MH_OPENSSL_CERT_PASSWORD = $values['MH_DEMO_CERT_PASSWORD']
    & $OpenSslPath pkcs12 -export -out $serverBundle `
        -inkey $serverKey -in $serverCertificate -certfile $tempRootCertificate `
        -name 'mental-health-demo' -passout env:MH_OPENSSL_CERT_PASSWORD
    if ($LASTEXITCODE -ne 0) { throw '生成演示服务器 PFX 失败。' }

    & $OpenSslPath verify -CAfile $tempRootCertificate $serverCertificate
    if ($LASTEXITCODE -ne 0) { throw '演示服务器证书链验证失败。' }
    & $OpenSslPath x509 -checkend 86400 -noout -in $serverCertificate
    if ($LASTEXITCODE -ne 0) { throw '演示服务器证书有效期不足 24 小时。' }
    $certificateText = & $OpenSslPath x509 -noout -text -in $serverCertificate
    if (($certificateText -join [Environment]::NewLine) -notmatch
        [Regex]::Escape("IP Address:$resolvedIp"))
    {
        throw '演示服务器证书没有包含当前局域网地址。'
    }

    Copy-Item -LiteralPath $tempRootKey -Destination $rootKey -Force
    Copy-Item -LiteralPath $tempRootCertificate -Destination $rootCertificate -Force
    Copy-Item -LiteralPath $serverKey -Destination (Join-Path $certRoot 'server.key') -Force
    Copy-Item -LiteralPath $serverRequest -Destination (Join-Path $certRoot 'server.csr') -Force
    Copy-Item -LiteralPath $serverConfig -Destination (Join-Path $certRoot 'server.cnf') -Force
    Copy-Item -LiteralPath $serverCertificate -Destination (Join-Path $certRoot 'server.crt') -Force
    Copy-Item -LiteralPath $serverBundle -Destination (Join-Path $certRoot 'server.pfx') -Force
    Copy-Item -LiteralPath $tempRootCertificate `
        -Destination (Join-Path $androidRoot 'demo_root.crt') -Force
    Copy-Item -LiteralPath $tempRootCertificate `
        -Destination (Join-Path $flutterAssetRoot 'demo_root.crt') -Force
}
finally
{
    Remove-Item Env:\MH_OPENSSL_CERT_PASSWORD -ErrorAction SilentlyContinue
    $checkedTemp = [IO.Path]::GetFullPath($tempRoot)
    if ($checkedTemp.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $checkedTemp))
    {
        Remove-Item -LiteralPath $checkedTemp -Recurse -Force
    }
}

Write-Host "已生成局域网演示证书，地址：$resolvedIp"
Write-Host '未修改 Windows 或 Android 系统证书库。'
