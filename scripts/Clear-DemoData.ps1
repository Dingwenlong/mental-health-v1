[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$AccessToken,

    [uri]$ApiBaseUrl = 'http://127.0.0.1:5165/api/v1/'
)

$ErrorActionPreference = 'Stop'

if (-not $ApiBaseUrl.IsAbsoluteUri) {
    throw 'ApiBaseUrl 必须是完整地址。'
}

$baseText = $ApiBaseUrl.AbsoluteUri.TrimEnd('/') + '/'
$target = [uri]::new([uri]$baseText, 'data-rights/demo-data')
if (-not $PSCmdlet.ShouldProcess(
        $target.AbsoluteUri,
        '清除当前账号的数据')) {
    return
}

$headers = @{ Authorization = "Bearer $AccessToken" }
try {
    Invoke-WebRequest `
        -Uri $target `
        -Method Delete `
        -Headers $headers `
        -UseBasicParsing | Out-Null
    Write-Host '当前账号的数据已清除。'
}
finally {
    $headers.Clear()
}
