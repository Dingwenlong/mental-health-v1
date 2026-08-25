#requires -Version 7.0

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$modulePath = Join-Path $repoRoot 'scripts\LocalTestJwt.psm1'

Import-Module $modulePath -Force

function ConvertFrom-Base64Url
{
    param([Parameter(Mandatory)][string]$Value)

    $padded = $Value.Replace('-', '+').Replace('_', '/')
    switch ($padded.Length % 4)
    {
        2 { $padded += '==' }
        3 { $padded += '=' }
    }
    return [Convert]::FromBase64String($padded)
}

function Assert-Equal
{
    param($Expected, $Actual, [string]$Message)

    if ($Expected -ne $Actual)
    {
        throw "$Message Expected: $Expected; actual: $Actual"
    }
}

$signingKey = 'local-test-signing-key-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ'
$userId = [Guid]'11111111-1111-1111-1111-111111111111'
$subjectId = [Guid]'22222222-2222-2222-2222-222222222222'
$token = New-LocalTestJwt `
    -UserId $userId `
    -Role User `
    -BusinessId $subjectId `
    -SigningKey $signingKey

$parts = $token.Split('.')
Assert-Equal 3 $parts.Count 'JWT 必须包含三个部分。'

$header = [Text.Encoding]::UTF8.GetString((ConvertFrom-Base64Url $parts[0])) |
    ConvertFrom-Json
$payload = [Text.Encoding]::UTF8.GetString((ConvertFrom-Base64Url $parts[1])) |
    ConvertFrom-Json

Assert-Equal 'HS256' $header.alg 'JWT 必须使用 HS256。'
Assert-Equal $userId.ToString() $payload.sub 'JWT sub 不正确。'
Assert-Equal $userId.ToString() `
    $payload.'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier' `
    'JWT NameIdentifier 不正确。'
Assert-Equal 'User' `
    $payload.'http://schemas.microsoft.com/ws/2008/06/identity/claims/role' `
    'JWT 角色不正确。'
Assert-Equal $subjectId.ToString() $payload.subject_id 'JWT subject_id 不正确。'
Assert-Equal 'api' $payload.scope 'JWT scope 不正确。'
Assert-Equal 'mental-health-v1-local' $payload.iss 'JWT issuer 不正确。'
Assert-Equal 'mental-health-v1-local' $payload.aud 'JWT audience 不正确。'
Assert-Equal 300 ([long]$payload.exp - [long]$payload.iat) 'JWT 必须在 5 分钟后过期。'

$hmac = [Security.Cryptography.HMACSHA256]::new(
    [Text.Encoding]::UTF8.GetBytes($signingKey))
try
{
    $actualSignature = ConvertFrom-Base64Url $parts[2]
    $expectedSignature = $hmac.ComputeHash(
        [Text.Encoding]::ASCII.GetBytes("$($parts[0]).$($parts[1])"))
    if ([Convert]::ToHexString($actualSignature) -ne
        [Convert]::ToHexString($expectedSignature))
    {
        throw 'JWT 签名不正确。'
    }
}
finally
{
    $hmac.Dispose()
}

$practitionerId = [Guid]'33333333-3333-3333-3333-333333333333'
$practitionerPayload = (New-LocalTestJwt `
        -UserId $userId `
        -Role Counselor `
        -BusinessId $practitionerId `
        -SigningKey $signingKey).Split('.')[1]
$practitionerClaims = [Text.Encoding]::UTF8.GetString(
    (ConvertFrom-Base64Url $practitionerPayload)) | ConvertFrom-Json
Assert-Equal $practitionerId.ToString() `
    $practitionerClaims.practitioner_id `
    '咨询师 JWT practitioner_id 不正确。'
if ($null -ne $practitionerClaims.subject_id)
{
    throw '咨询师 JWT 不应包含 subject_id。'
}

Write-Host 'LocalTestJwt 测试通过。'
