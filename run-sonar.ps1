param (
    [string]$Token = $env:SONAR_TOKEN,
    [string]$HostUrl = $(if ([string]::IsNullOrWhiteSpace($env:SONAR_HOST_URL)) { "http://localhost:9000" } else { $env:SONAR_HOST_URL }),
    [string]$ProjectKey = "disphub",
    [string]$SolutionPath = ".\DispHub.sln",
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = "Release",
    [int]$QualityGateTimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Tool {
    param (
        [Parameter(Mandatory = $true)]
        [string]$Tool,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$FailureMessage
    )

    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "ERROR: No SonarQube token provided. Set SONAR_TOKEN or pass -Token." -ForegroundColor Red
    exit 1
}

if ($QualityGateTimeoutSeconds -lt 1) {
    Write-Host "ERROR: QualityGateTimeoutSeconds must be greater than zero." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $SolutionPath)) {
    Write-Host "ERROR: Solution path '$SolutionPath' was not found." -ForegroundColor Red
    exit 1
}

Write-Host "Checking SonarQube server at $HostUrl..." -ForegroundColor Cyan
try {
    $serverStatus = Invoke-RestMethod -Method Get -Uri "$HostUrl/api/system/status" -TimeoutSec 20
} catch {
    Write-Host "ERROR: Could not connect to SonarQube at $HostUrl. Ensure the server is running." -ForegroundColor Red
    exit 1
}

if ($serverStatus.status -ne 'UP') {
    Write-Host "ERROR: SonarQube status is '$($serverStatus.status)'. Wait for it to become 'UP'." -ForegroundColor Red
    exit 1
}

$scannerPrefix = @()
$toolManifestExists = (Test-Path '.\dotnet-tools.json') -or (Test-Path '.\.config\dotnet-tools.json')
if ($toolManifestExists) {
    Write-Host 'Restoring local .NET tools...' -ForegroundColor Cyan
    Invoke-Tool -Tool 'dotnet' -Arguments @('tool', 'restore') -FailureMessage 'dotnet tool restore failed.'
    $scannerPrefix = @('tool', 'run', 'dotnet-sonarscanner')
} else {
    $sonarScannerCommand = Get-Command dotnet-sonarscanner -ErrorAction SilentlyContinue
    if ($null -eq $sonarScannerCommand) {
        Write-Host "ERROR: dotnet-sonarscanner is not installed. Install with 'dotnet tool install --global dotnet-sonarscanner'." -ForegroundColor Red
        exit 1
    }

    $scannerPrefix = @('sonarscanner')
}

# Exclude UI code-behind and hardware/interop integration surfaces from line coverage.
# These paths are validated primarily via integration/manual verification, while unit coverage
# gates focus on deterministic domain and service logic.
$coverageExclusions = @(
    '**/*.xaml.cs',
    '**/NVIDIA/**/*.cs',
    '**/Services/Display/DisplayManager.cs',
    '**/Services/Display/NvidiaVibranceService.cs',
    '**/Services/Hotkeys/DynamicControls.cs'
) -join ','

Write-Host "Starting SonarQube analysis..." -ForegroundColor Cyan
$beginArgs = @($scannerPrefix + @(
    'begin',
    "/k:$ProjectKey",
    "/d:sonar.host.url=$HostUrl",
    "/d:sonar.token=$Token",
    '/d:sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml',
    '/d:sonar.exclusions=**/bin/**,**/obj/**,**/plan/**',
    "/d:sonar.coverage.exclusions=$coverageExclusions",
    '/d:sonar.qualitygate.wait=true',
    "/d:sonar.qualitygate.timeout=$QualityGateTimeoutSeconds"
))
Invoke-Tool -Tool 'dotnet' -Arguments $beginArgs -FailureMessage 'SonarScanner begin step failed.'

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
Invoke-Tool -Tool 'dotnet' -Arguments @('restore', $SolutionPath) -FailureMessage 'dotnet restore failed.'

Write-Host "Building project ($Configuration)..." -ForegroundColor Cyan
Invoke-Tool -Tool 'dotnet' -Arguments @('build', $SolutionPath, '-c', $Configuration, '--no-restore') -FailureMessage 'dotnet build failed.'

Write-Host "Running tests with coverage..." -ForegroundColor Cyan
Invoke-Tool -Tool 'dotnet' -Arguments @(
    'test',
    $SolutionPath,
    '-c',
    $Configuration,
    '--no-build',
    '--verbosity',
    'minimal',
    '--logger',
    'trx;LogFileName=test-results.trx',
    '--collect:XPlat Code Coverage',
    '--results-directory',
    '.\TestResults',
    '--',
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover'
) -FailureMessage 'dotnet test failed.'

$coverageReports = Get-ChildItem -Path '.\TestResults' -Recurse -Filter 'coverage.opencover.xml' -ErrorAction SilentlyContinue
if (@($coverageReports).Count -eq 0) {
    Write-Host 'ERROR: No OpenCover coverage reports were generated in .\TestResults.' -ForegroundColor Red
    exit 1
}

Write-Host "Finalizing analysis and waiting for Quality Gate..." -ForegroundColor Cyan
$endArgs = @($scannerPrefix + @('end', "/d:sonar.token=$Token"))
Invoke-Tool -Tool 'dotnet' -Arguments $endArgs -FailureMessage 'SonarScanner end step failed or quality gate did not pass.'

Write-Host "Analysis complete. Dashboard: $HostUrl/dashboard?id=$ProjectKey" -ForegroundColor Green
