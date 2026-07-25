[CmdletBinding()]
param(
    [string] $ProjectPath = (
        Join-Path $PSScriptRoot "..\tests\fixtures\TuanjieProject"
    )
)

$ErrorActionPreference = "Stop"
$requiredVersion = "2022.3.62t11"
$script:hasFailures = $false

function Write-Check {
    param(
        [ValidateSet("PASS", "FAIL", "WAIT")]
        [string] $Status,
        [string] $Check,
        [string] $Details
    )

    Write-Output ("[{0}] {1}: {2}" -f $Status, $Check, $Details)
    if ($Status -eq "FAIL") {
        $script:hasFailures = $true
    }
}

function Resolve-DisplayIconPath {
    param([string] $DisplayIcon)

    if ([string]::IsNullOrWhiteSpace($DisplayIcon)) {
        return $null
    }

    $expanded = [Environment]::ExpandEnvironmentVariables(
        $DisplayIcon.Trim()
    )
    if ($expanded -match '^"([^"]+)"') {
        return $Matches[1]
    }

    return ($expanded -replace ',\s*-?\d+\s*$', '').Trim().Trim('"')
}

function Test-LoopbackPort {
    param([int] $Port)

    $client = New-Object System.Net.Sockets.TcpClient
    $asyncResult = $null
    try {
        $asyncResult = $client.BeginConnect(
            "127.0.0.1",
            $Port,
            $null,
            $null
        )
        if (-not $asyncResult.AsyncWaitHandle.WaitOne(750)) {
            return $false
        }
        $client.EndConnect($asyncResult)
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $asyncResult) {
            $asyncResult.AsyncWaitHandle.Dispose()
        }
        $client.Dispose()
    }
}

$uninstallRoots = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
)
$installedEditors = @(
    Get-ItemProperty -Path $uninstallRoots -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayVersion -eq $requiredVersion } |
        ForEach-Object {
            $executable = Resolve-DisplayIconPath $_.DisplayIcon
            if (
                -not [string]::IsNullOrWhiteSpace($executable) -and
                [IO.Path]::GetFileName($executable) -ieq "Tuanjie.exe"
            ) {
                [PSCustomObject]@{
                    DisplayName = $_.DisplayName
                    DisplayVersion = $_.DisplayVersion
                    Executable = $executable
                }
            }
        }
)

if ($installedEditors.Count -eq 0) {
    Write-Check "FAIL" "Tuanjie installation" (
        "No uninstall entry for $requiredVersion with a Tuanjie.exe DisplayIcon."
    )
}
else {
    $installedEditor = $installedEditors[0]
    Write-Check "PASS" "Tuanjie installation" (
        "{0} {1}" -f $installedEditor.DisplayName,
        $installedEditor.DisplayVersion
    )
    if (Test-Path -LiteralPath $installedEditor.Executable -PathType Leaf) {
        Write-Check "PASS" "Tuanjie executable" $installedEditor.Executable
    }
    else {
        Write-Check "FAIL" "Tuanjie executable" (
            "DisplayIcon target does not exist: {0}" -f
            $installedEditor.Executable
        )
    }
}

$resolvedProjectPath = $null
try {
    $resolvedProjectPath = (
        Resolve-Path -LiteralPath $ProjectPath -ErrorAction Stop
    ).Path
    $projectVersionPath = Join-Path $resolvedProjectPath (
        "ProjectSettings\ProjectVersion.txt"
    )
    $versionLine = (
        Get-Content -LiteralPath $projectVersionPath -ErrorAction Stop |
            Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
            Select-Object -First 1
    )
    if (
        $null -ne $versionLine -and
        $versionLine -match '^m_EditorVersion:\s*(.+)$' -and
        $Matches[1].Trim() -eq $requiredVersion
    ) {
        Write-Check "PASS" "Fixture version" (
            "$resolvedProjectPath ($requiredVersion)"
        )
    }
    else {
        Write-Check "FAIL" "Fixture version" (
            "ProjectVersion.txt must specify $requiredVersion."
        )
    }
}
catch {
    Write-Check "FAIL" "Fixture version" $_.Exception.Message
}

if ($null -ne $resolvedProjectPath) {
    $instancePath = Join-Path $resolvedProjectPath (
        "Library\EditorInstance.json"
    )
    if (-not (Test-Path -LiteralPath $instancePath -PathType Leaf)) {
        Write-Check "WAIT" "Running fixture Editor" (
            "Open the fixture in Tuanjie; EditorInstance.json is not present."
        )
        Write-Check "WAIT" "Managed debugger port" (
            "Port probing starts after the fixture Editor is running."
        )
    }
    else {
        try {
            $instance = (
                Get-Content -Raw -LiteralPath $instancePath |
                    ConvertFrom-Json
            )
            $processId = [int] $instance.process_id
            if ($processId -le 0) {
                throw "EditorInstance.json has an invalid process_id."
            }

            $process = Get-Process -Id $processId -ErrorAction Stop
            Write-Check "PASS" "Running fixture Editor" (
                "PID {0} ({1})" -f $processId, $process.ProcessName
            )

            $debuggerPort = 56000 + ($processId % 1000)
            if (Test-LoopbackPort $debuggerPort) {
                Write-Check "PASS" "Managed debugger port" (
                    "127.0.0.1:$debuggerPort is reachable."
                )
            }
            else {
                Write-Check "FAIL" "Managed debugger port" (
                    "127.0.0.1:$debuggerPort is closed. " +
                    "Set Code Optimization to Debug."
                )
            }
        }
        catch {
            Write-Check "FAIL" "Running fixture Editor" $_.Exception.Message
            Write-Check "FAIL" "Managed debugger port" (
                "Could not derive and probe the Editor port."
            )
        }
    }
}

if ($script:hasFailures) {
    exit 1
}
exit 0
