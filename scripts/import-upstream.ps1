$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$tempBase = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::GetTempPath())
$tempRoot = Join-Path $tempBase (
    'unity-debugger-import-' + [guid]::NewGuid().ToString('N'))

$sources = @(
    @{
        Name = 'vscode-mono-debug'
        Repository = 'https://github.com/Unity-Technologies/vscode-mono-debug.git'
        Revision = 'd233b366b0c67ae4d61488f7e974e2a5b9da2e3b'
        Paths = @(
            'src/DebugSession.cs',
            'src/Protocol.cs',
            'LICENSE.txt',
            'ThirdPartyNotices.txt'
        )
    },
    @{
        Name = 'debugger-libs'
        Repository = 'https://github.com/Unity-Technologies/debugger-libs.git'
        Revision = 'cd005e941d18c92ddf0c50084c59ddaae6bf4c5d'
        Paths = @(
            'Mono.Debugger.Soft',
            'Mono.Debugging',
            'Mono.Debugging.Soft',
            'Mono.Debugging.settings',
            'LICENSE'
        )
    },
    @{
        Name = 'nrefactory'
        Repository = 'https://github.com/icsharpcode/NRefactory.git'
        Revision = '0607a4ad96ebdd16817e47dcae85b1cfcb5b5bf5'
        Paths = @(
            'ICSharpCode.NRefactory',
            'ICSharpCode.NRefactory.CSharp',
            'ICSharpCode.NRefactory.snk',
            'doc/license.txt'
        )
    }
)

New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    foreach ($source in $sources) {
        $checkout = Join-Path $tempRoot $source.Name
        & git clone --no-checkout `
            $source.Repository $checkout
        if ($LASTEXITCODE -ne 0) {
            throw "Clone failed for $($source.Name)."
        }

        & git -C $checkout fetch --no-tags origin $source.Revision
        if ($LASTEXITCODE -ne 0) {
            throw "Revision fetch failed for $($source.Name)."
        }

        $checkoutArguments = @(
            '-C',
            $checkout,
            'checkout',
            'FETCH_HEAD',
            '--'
        ) + $source.Paths
        & git @checkoutArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Checkout failed for $($source.Name)."
        }

        $destination = Join-Path $repoRoot (
            'adapter/vendor/' + $source.Name)
        if (
            (Test-Path -LiteralPath $destination) -and
            (Get-ChildItem -LiteralPath $destination -Force |
                Select-Object -First 1)
        ) {
            throw "Destination is not empty: $destination"
        }

        New-Item -ItemType Directory `
            -Path $destination -Force | Out-Null
        foreach ($sourcePath in $source.Paths) {
            $item = Join-Path $checkout $sourcePath
            Copy-Item -LiteralPath $item `
                -Destination $destination -Recurse -Force
        }
    }
}
finally {
    $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
    $expectedPrefix = $tempBase.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedTemp.StartsWith(
        $expectedPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedTemp"
    }
    Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
}
