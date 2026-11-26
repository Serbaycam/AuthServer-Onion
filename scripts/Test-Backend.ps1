#requires -Version 7.0
<#
.SYNOPSIS
Runs backend integration tests against a new, disposable PostgreSQL 16 cluster.
.DESCRIPTION
Requires existing PostgreSQL binaries; this script does not download software,
register services, change global settings, or connect to an existing database.
Every run uses a new directory under artifacts/test-runs and loopback port 55432.
The cluster is stopped in finally. Artifacts are retained; no recursive deletion
is performed. The generated password is never printed or saved in run metadata.
For non-ASCII Windows paths, existing 8.3 aliases are used without changing the
filesystem or registering a drive mapping. Missing aliases produce an error.
.EXAMPLE
pwsh -File scripts/Test-Backend.ps1 -PostgresBinPath ./artifacts/postgresql/16.16-2/server/pgsql/bin
.EXAMPLE
pwsh -File scripts/Test-Backend.ps1 -PostgresBinPath C:/tools/pgsql/bin -Filter 'FullyQualifiedName~ConcurrentRefresh'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $PostgresBinPath,

    [string] $Filter,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if (-not $IsWindows) {
    throw 'This runner requires PowerShell 7 on Windows and PostgreSQL 16 Windows binaries.'
}

# PostgreSQL's Windows UTF8 bootstrap needs ASCII executable/data paths.
# These Win32 calls only read existing aliases; they create no drive mappings.
if (-not ('AuthServer.TestRunner.WindowsPaths' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
using System.Text;
namespace AuthServer.TestRunner {
    public static class WindowsPaths {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetShortPathNameW(string path, StringBuilder buffer, uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetLongPathNameW(string path, StringBuilder buffer, uint size);
    }
}
'@
}
function Get-NativeAsciiPath {
    param([string] $Path)
    $buffer = [Text.StringBuilder]::new(32768)
    $length = [AuthServer.TestRunner.WindowsPaths]::GetShortPathNameW($Path, $buffer, $buffer.Capacity)
    if ($length -eq 0 -or $length -ge $buffer.Capacity -or $buffer.ToString() -match '[^\x00-\x7F]') {
        throw 'PostgreSQL needs an ASCII path or an existing ASCII 8.3 alias. No filesystem aliases will be created.'
    }
    return $buffer.ToString()
}
function Get-ExpandedWindowsPath {
    param([string] $Path)
    $buffer = [Text.StringBuilder]::new(32768)
    $length = [AuthServer.TestRunner.WindowsPaths]::GetLongPathNameW($Path, $buffer, $buffer.Capacity)
    if ($length -eq 0 -or $length -ge $buffer.Capacity) { throw 'Unable to resolve the PostgreSQL data directory identity.' }
    return [IO.Path]::GetFullPath($buffer.ToString())
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repositoryPrefix = $repositoryRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$runsRoot = Join-Path $artifactsRoot 'test-runs'
$postgresBin = (Resolve-Path -LiteralPath $PostgresBinPath).ProviderPath
$postgresExe = Join-Path $postgresBin 'postgres.exe'
$initdbExe = Join-Path $postgresBin 'initdb.exe'
$pgCtlExe = Join-Path $postgresBin 'pg_ctl.exe'
$testProject = Join-Path $repositoryRoot 'AuthServer.Identity.Tests/AuthServer.Identity.Tests.csproj'
$port = 55432

foreach ($requiredFile in @($postgresExe, $initdbExe, $pgCtlExe, $testProject)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required file was not found: $requiredFile"
    }
}
$dotnetExe = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
$nativeBin = Get-NativeAsciiPath $postgresBin
$postgresExe = Join-Path $nativeBin 'postgres.exe'
$initdbExe = Join-Path $nativeBin 'initdb.exe'
$pgCtlExe = Join-Path $nativeBin 'pg_ctl.exe'
$postgresVersion = (& $postgresExe --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $postgresVersion -notmatch 'PostgreSQL\)\s+16\.') {
    throw 'PostgresBinPath must contain working PostgreSQL 16 binaries.'
}

foreach ($directory in @($artifactsRoot, $runsRoot)) {
    if (-not [IO.Path]::GetFullPath($directory).StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Test artifacts must remain inside the repository.'
    }
    if (Test-Path -LiteralPath $directory) {
        $item = Get-Item -LiteralPath $directory
        if (-not $item.PSIsContainer -or $item.LinkType) {
            throw "Test artifacts cannot use a file, symbolic link, or junction: $directory"
        }
    }
}

# Refuse an occupied port instead of falling back to an existing database.
$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $port)
try {
    $portProbe.Start()
}
catch {
    throw 'Loopback port 55432 is unavailable. No existing PostgreSQL instance will be used.'
}
finally {
    $portProbe.Stop()
}

$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$runDirectory = [IO.Path]::GetFullPath((Join-Path $runsRoot $runId))
if (-not $runDirectory.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    (Test-Path -LiteralPath $runDirectory)) {
    throw 'A fresh test directory inside the repository is required.'
}
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

# The temporary password file and test database files inherit a private run ACL.
$currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User
$runAcl = [Security.AccessControl.DirectorySecurity]::new()
$runAcl.SetAccessRuleProtection($true, $false)
$accessRule = [Security.AccessControl.FileSystemAccessRule]::new(
    $currentUserSid,
    [Security.AccessControl.FileSystemRights]::FullControl,
    [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit',
    [Security.AccessControl.PropagationFlags]::None,
    [Security.AccessControl.AccessControlType]::Allow
)
$runAcl.AddAccessRule($accessRule)
Set-Acl -LiteralPath $runDirectory -AclObject $runAcl

$nativeRunDirectory = Get-NativeAsciiPath $runDirectory
$dataDirectory = Join-Path $runDirectory 'data'
$nativeDataDirectory = Join-Path $nativeRunDirectory 'data'
$resultsDirectory = Join-Path $runDirectory 'results'
$passwordFile = Join-Path $runDirectory 'initdb-password.tmp'
$nativePasswordFile = Join-Path $nativeRunDirectory 'initdb-password.tmp'
$postgresLog = Join-Path $nativeRunDirectory 'postgresql.log'
$databaseName = 'authserver_' + [Guid]::NewGuid().ToString('N') + '_tests'
if ($databaseName -notmatch '^authserver_[a-f0-9]{32}_tests$') {
    throw 'The generated test database name is invalid.'
}
$randomBytes = [byte[]]::new(32)
[Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
$password = [Convert]::ToHexString($randomBytes)
[Array]::Clear($randomBytes, 0, $randomBytes.Length)
$savedDatabaseEnvironment = [Environment]::GetEnvironmentVariable('AUTH_TEST_DATABASE', 'Process')
$savedKeysEnvironment = [Environment]::GetEnvironmentVariable('DataProtection__KeysPath', 'Process')
$startupAttempted = $false
$testsStarted = $false
$exitCode = 1
$failureMessage = $null
$cleanupWarnings = [Collections.Generic.List[string]]::new()

function Invoke-LoggedNative {
    param(
        [string] $Executable,
        [string[]] $Arguments,
        [string] $LogFile
    )
    & $Executable @Arguments 2>&1 | ForEach-Object {
        $line = $_.ToString().Replace($password, '[REDACTED]')
        Add-Content -LiteralPath $LogFile -Value $line -Encoding utf8
        Write-Host $line
    }
    return $LASTEXITCODE
}

function Invoke-PostgresControl {
    param([string[]] $Arguments, [string] $LogFile)
    # Start-Process -Wait waits for descendants on Windows. Wait for only the
    # pg_ctl process, so a successfully started PostgreSQL server may stay up.
    $controlId = [Guid]::NewGuid().ToString('N')
    $stdoutFile = Join-Path $nativeRunDirectory "$controlId.pgctl.stdout.log"
    $stderrFile = Join-Path $nativeRunDirectory "$controlId.pgctl.stderr.log"
    $quotedArguments = foreach ($argument in $Arguments) {
        if ($argument.Contains('"') -or $argument.EndsWith('\')) {
            throw 'Unexpected PostgreSQL control argument.'
        }
        '"' + $argument + '"'
    }
    $control = Start-Process -FilePath $pgCtlExe -ArgumentList ($quotedArguments -join ' ') -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile
    try {
        if (-not $control.WaitForExit(45000)) {
            # Stop only the process tree created by this control invocation,
            # including a child that has not written postmaster.pid yet. The
            # outer finally also checks this run's unique PostgreSQL data path.
            try { if (-not $control.HasExited) { $control.Kill($true) } }
            catch [InvalidOperationException] { } # The process exited during the check.
            if (-not $control.WaitForExit(5000)) {
                throw 'The timed-out PostgreSQL control process could not be stopped.'
            }
            throw 'The isolated PostgreSQL control command timed out; its control process was stopped.'
        }
        $controlExitCode = $control.ExitCode
    }
    finally { $control.Dispose() }
    $message = "pg_ctl $($Arguments[-1]): exit $controlExitCode"
    Add-Content -LiteralPath $LogFile -Value $message -Encoding utf8
    Write-Host $message
    return $controlExitCode
}

Write-Host "PostgreSQL: $postgresVersion"
Write-Host "Test artifacts: $runDirectory"

try {
    [IO.File]::WriteAllText($passwordFile, $password, [Text.UTF8Encoding]::new($false))
    $initializeExitCode = Invoke-LoggedNative -Executable $initdbExe -Arguments @(
        '-D', $nativeDataDirectory, '-U', 'test', '--encoding=UTF8', '--locale=C',
        '--auth-host=scram-sha-256', '--auth-local=scram-sha-256', "--pwfile=$nativePasswordFile"
    ) -LogFile (Join-Path $runDirectory 'initdb.log')
    if ($initializeExitCode -ne 0) {
        throw "The isolated PostgreSQL cluster could not be initialized (exit $initializeExitCode)."
    }
    Remove-Item -LiteralPath $passwordFile

    $startupAttempted = $true
    $startExitCode = Invoke-PostgresControl -Arguments @(
        '-D', $nativeDataDirectory, '-l', $postgresLog, '-o', "-h 127.0.0.1 -p $port", '-w', '-t', '30', 'start'
    ) -LogFile (Join-Path $runDirectory 'start.log')
    if ($startExitCode -ne 0) {
        throw "The isolated PostgreSQL cluster did not start (exit $startExitCode)."
    }

    # Check that the server started from our fresh data directory on our port.
    $pidFile = [IO.File]::ReadAllLines((Join-Path $dataDirectory 'postmaster.pid'))
    if ($pidFile.Length -lt 6 -or
        -not (Get-ExpandedWindowsPath $pidFile[1]).Equals($dataDirectory, [StringComparison]::OrdinalIgnoreCase) -or
        $pidFile[3] -ne "$port" -or $pidFile[5] -ne '127.0.0.1') {
        throw 'The PostgreSQL process identity did not match the isolated test cluster.'
    }

    [Environment]::SetEnvironmentVariable('AUTH_TEST_DATABASE',
        "Host=127.0.0.1;Port=$port;Database=$databaseName;Username=test;Password=$password", 'Process')
    [Environment]::SetEnvironmentVariable('DataProtection__KeysPath', (Join-Path $runDirectory 'keys'), 'Process')

    $testArguments = @(
        'test', $testProject, '--configuration', $Configuration,
        '--results-directory', $resultsDirectory, '--logger', 'trx;LogFileName=backend.trx'
    )
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $testArguments += @('--filter', $Filter)
    }
    $testsStarted = $true
    $exitCode = Invoke-LoggedNative -Executable $dotnetExe -Arguments $testArguments -LogFile (Join-Path $runDirectory 'tests.log')
}
catch {
    $failureMessage = $_.Exception.Message.Replace($password, '[REDACTED]')
    Write-Warning $failureMessage
    if (-not $testsStarted -or $exitCode -eq 0) { $exitCode = 1 }
}
finally {
    [Environment]::SetEnvironmentVariable('AUTH_TEST_DATABASE', $savedDatabaseEnvironment, 'Process')
    [Environment]::SetEnvironmentVariable('DataProtection__KeysPath', $savedKeysEnvironment, 'Process')

    if (Test-Path -LiteralPath $passwordFile) {
        try { Remove-Item -LiteralPath $passwordFile }
        catch {
            $cleanupMessage = $_.Exception.Message.Replace($password, '[REDACTED]')
            $cleanupWarnings.Add($cleanupMessage)
            Write-Warning $cleanupMessage
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
    }
    if ($startupAttempted) {
        try {
            $statusExitCode = Invoke-PostgresControl -Arguments @(
                '-D', $nativeDataDirectory, 'status'
            ) -LogFile (Join-Path $runDirectory 'stop.log')
            if ($statusExitCode -eq 0) {
                $stopExitCode = Invoke-PostgresControl -Arguments @(
                    '-D', $nativeDataDirectory, '-m', 'fast', '-w', '-t', '30', 'stop'
                ) -LogFile (Join-Path $runDirectory 'stop.log')
                if ($stopExitCode -ne 0) {
                    throw "The isolated PostgreSQL cluster did not stop (exit $stopExitCode)."
                }
            }
            elseif ($statusExitCode -ne 3) {
                throw "Unable to establish whether the isolated cluster stopped (exit $statusExitCode)."
            }
        }
        catch {
            $cleanupMessage = $_.Exception.Message.Replace($password, '[REDACTED]')
            $cleanupWarnings.Add($cleanupMessage)
            Write-Warning $cleanupMessage
            if ($exitCode -eq 0) { $exitCode = 1 }
        }
    }

    # Test framework artifacts must not retain the generated credential either.
    try {
        if (Test-Path -LiteralPath $resultsDirectory) {
            Get-ChildItem -LiteralPath $resultsDirectory -Filter '*.trx' -File | ForEach-Object {
                $content = [IO.File]::ReadAllText($_.FullName)
                if ($content.Contains($password)) {
                    [IO.File]::WriteAllText($_.FullName, $content.Replace($password, '[REDACTED]'), [Text.UTF8Encoding]::new($false))
                }
            }
        }
    }
    catch {
        $cleanupMessage = $_.Exception.Message.Replace($password, '[REDACTED]')
        $cleanupWarnings.Add($cleanupMessage)
        Write-Warning $cleanupMessage
        if ($exitCode -eq 0) { $exitCode = 1 }
    }
    try {
        [ordered]@{
            runId = $runId
            completedAtUtc = [DateTime]::UtcNow.ToString('o')
            postgresVersion = $postgresVersion
            databaseName = $databaseName
            port = $port
            configuration = $Configuration
            testsStarted = $testsStarted
            exitCode = $exitCode
            failure = $failureMessage
            cleanupWarnings = $cleanupWarnings.ToArray()
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
    }
    catch {
        Write-Warning $_.Exception.Message.Replace($password, '[REDACTED]')
        if ($exitCode -eq 0) { $exitCode = 1 }
    }
    $password = $null
}

Write-Host "Backend test runner exit code: $exitCode"
exit $exitCode
