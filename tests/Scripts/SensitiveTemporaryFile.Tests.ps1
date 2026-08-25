#requires -Version 7.0

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$modulePath = Join-Path $repoRoot 'scripts\SensitiveTemporaryFile.psm1'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) `
    "mental-health-sensitive-file-test-$([Guid]::NewGuid().ToString('N'))"
$definesPath = Join-Path $testDirectory 'defines.json'

Import-Module $modulePath -Force

New-Item -ItemType Directory -Path $testDirectory | Out-Null
try
{
    [IO.File]::WriteAllText($definesPath, 'temporary-test-value')
    Remove-SensitiveTemporaryFile -Path $definesPath
    if (Test-Path -LiteralPath $definesPath)
    {
        throw '正常清理后 defines.json 仍然存在。'
    }

    [IO.File]::WriteAllText($definesPath, 'temporary-test-value')
    $handle = [IO.File]::Open(
        $definesPath,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::None)
    try
    {
        $cleanupFailure = $null
        try
        {
            Remove-SensitiveTemporaryFile -Path $definesPath
        }
        catch
        {
            $cleanupFailure = $_
        }

        if ($null -eq $cleanupFailure)
        {
            throw 'defines.json 删除失败时不应被吞掉。'
        }
        if (-not (Test-Path -LiteralPath $definesPath))
        {
            throw '锁定文件应保留到测试释放文件句柄。'
        }
    }
    finally
    {
        $handle.Dispose()
    }
}
finally
{
    $checkedTestDirectory = [IO.Path]::GetFullPath($testDirectory)
    $checkedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $checkedTestDirectory.StartsWith(
        $checkedTemp,
        [StringComparison]::OrdinalIgnoreCase))
    {
        throw '测试临时目录不在系统临时目录内，停止清理。'
    }
    Remove-Item -LiteralPath $checkedTestDirectory -Recurse -Force
}

Write-Host 'SensitiveTemporaryFile 测试通过。'
