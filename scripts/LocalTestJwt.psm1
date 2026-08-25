Set-StrictMode -Version Latest

function ConvertTo-Base64Url
{
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-LocalTestJwt
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][Guid]$UserId,
        [Parameter(Mandatory)]
        [ValidateSet('User', 'Counselor', 'Doctor')]
        [string]$Role,
        [Parameter(Mandatory)][Guid]$BusinessId,
        [Parameter(Mandatory)][string]$SigningKey
    )

    if ($UserId -eq [Guid]::Empty)
    {
        throw '测试 JWT 需要有效的用户 ID。'
    }
    if ($BusinessId -eq [Guid]::Empty)
    {
        throw '测试 JWT 需要有效的业务 ID。'
    }
    if ([Text.Encoding]::UTF8.GetByteCount($SigningKey) -lt 32)
    {
        throw '本机 JWT 密钥至少需要 32 字节。'
    }

    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $header = [ordered]@{
        alg = 'HS256'
        typ = 'JWT'
    }
    $payload = [ordered]@{
        sub = $UserId.ToString()
        'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier' =
            $UserId.ToString()
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' = $Role
        scope = 'api'
        iss = 'mental-health-v1-local'
        aud = 'mental-health-v1-local'
        nbf = $now
        iat = $now
        exp = $now + 300
    }
    if ($Role -eq 'User')
    {
        $payload.subject_id = $BusinessId.ToString()
    }
    else
    {
        $payload.practitioner_id = $BusinessId.ToString()
    }

    $headerPart = ConvertTo-Base64Url (
        [Text.Encoding]::UTF8.GetBytes(($header | ConvertTo-Json -Compress)))
    $payloadPart = ConvertTo-Base64Url (
        [Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json -Compress)))
    $unsignedToken = "$headerPart.$payloadPart"
    $hmac = [Security.Cryptography.HMACSHA256]::new(
        [Text.Encoding]::UTF8.GetBytes($SigningKey))
    try
    {
        $signature = ConvertTo-Base64Url (
            $hmac.ComputeHash([Text.Encoding]::ASCII.GetBytes($unsignedToken)))
        return "$unsignedToken.$signature"
    }
    finally
    {
        $hmac.Dispose()
    }
}

Export-ModuleMember -Function New-LocalTestJwt
