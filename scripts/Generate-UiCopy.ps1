$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repositoryRoot 'content\zh-CN\ui-copy.v1.json'
$dartPath = Join-Path $repositoryRoot 'apps\mobile_flutter\lib\generated\ui_copy.g.dart'
$typescriptPath = Join-Path $repositoryRoot 'apps\admin_web\src\generated\uiCopy.generated.ts'

$json = Get-Content -LiteralPath $sourcePath -Raw
$document = [System.Text.Json.JsonDocument]::Parse($json)
try {
    if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
        throw 'UI copy source must be a JSON object.'
    }

    $names = @($document.RootElement.EnumerateObject() | ForEach-Object Name)
    if ($names | Group-Object -CaseSensitive | Where-Object Count -gt 1) {
        throw 'Duplicate copy key.'
    }
}
finally {
    $document.Dispose()
}

$copy = $json | ConvertFrom-Json -AsHashtable
$pairs = @($copy.GetEnumerator() | Sort-Object Key -CaseSensitive)
$jsonOptions = [System.Text.Json.JsonSerializerOptions]::new()
$jsonOptions.Encoder = [System.Text.Encodings.Web.JavaScriptEncoder]::UnsafeRelaxedJsonEscaping

function ConvertTo-JsonString([string] $value) {
    return [System.Text.Json.JsonSerializer]::Serialize(
        [object]$value,
        [type][string],
        $jsonOptions)
}

$dart = @(
    '// Generated from content/zh-CN/ui-copy.v1.json. Do not edit.',
    'abstract final class UiCopy {',
    '  static const values = <String, String>{'
)
$typescript = @(
    '// Generated from content/zh-CN/ui-copy.v1.json. Do not edit.',
    'export const uiCopy = {'
)
foreach ($pair in $pairs) {
    $key = ConvertTo-JsonString ([string]$pair.Key)
    $value = ConvertTo-JsonString ([string]$pair.Value)
    $dart += "    ${key}: ${value},"
    $typescript += "  ${key}: ${value},"
}
$dart += @(
    '  };',
    '',
    '  static String get(String key) =>',
    "      values[key] ?? (throw StateError('Missing UI copy: `$key'));",
    '}'
)
$typescript += @(
    '} as const',
    '',
    'export type UiCopyKey = keyof typeof uiCopy',
    'export const getUiCopy = (key: UiCopyKey): string => uiCopy[key]'
)

$dartDirectory = Split-Path -Parent $dartPath
$typescriptDirectory = Split-Path -Parent $typescriptPath
[System.IO.Directory]::CreateDirectory($dartDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($typescriptDirectory) | Out-Null
[System.IO.File]::WriteAllLines($dartPath, $dart, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllLines($typescriptPath, $typescript, [System.Text.UTF8Encoding]::new($false))

Write-Host "Generated $($pairs.Count) UI copy entries."
