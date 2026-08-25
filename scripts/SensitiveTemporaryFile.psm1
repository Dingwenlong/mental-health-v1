#requires -Version 7.0

Set-StrictMode -Version Latest

function Remove-SensitiveTemporaryFile
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -ErrorAction Stop))
    {
        return
    }

    $removalFailure = $null
    try
    {
        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
    }
    catch
    {
        $removalFailure = $_
    }

    $stillExists = $true
    try
    {
        $stillExists = Test-Path -LiteralPath $Path -ErrorAction Stop
    }
    catch
    {
        throw "敏感临时文件删除后无法确认：$Path"
    }

    if ($null -ne $removalFailure)
    {
        throw "敏感临时文件删除失败：$Path"
    }
    if ($stillExists)
    {
        throw "敏感临时文件删除后仍然存在：$Path"
    }
}

Export-ModuleMember -Function Remove-SensitiveTemporaryFile
