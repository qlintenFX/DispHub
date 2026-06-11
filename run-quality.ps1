param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$SolutionPath = '.\DispHub.sln',
    [string]$TestsProjectPath = '.\DispHub.Tests\DispHub.Tests.csproj',
    [switch]$WithSonar
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$Tool,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed."
    }
}

function Get-VulnerablePackages {
    param(
        [Parameter(Mandatory = $true)]
        [object]$AuditReport
    )

    $results = @()
    foreach ($project in $AuditReport.projects) {
        if (-not $project.PSObject.Properties.Name.Contains('frameworks')) {
            continue
        }

        foreach ($framework in $project.frameworks) {
            foreach ($packageCollectionName in @('topLevelPackages', 'transitivePackages')) {
                $packages = $framework.$packageCollectionName
                if ($null -eq $packages) {
                    continue
                }

                foreach ($pkg in $packages) {
                    if ($null -eq $pkg.vulnerabilities -or @($pkg.vulnerabilities).Count -eq 0) {
                        continue
                    }

                    $severity = @($pkg.vulnerabilities | ForEach-Object { $_.severity } | Select-Object -Unique) -join ','
                    $results += [pscustomobject]@{
                        Project   = $project.path
                        Framework = $framework.framework
                        Package   = $pkg.id
                        Resolved  = $pkg.resolvedVersion
                        Severity  = $severity
                    }
                }
            }
        }
    }

    return $results
}

if (-not (Test-Path $SolutionPath)) {
    throw "Solution path '$SolutionPath' was not found."
}

if (-not (Test-Path $TestsProjectPath)) {
    throw "Test project path '$TestsProjectPath' was not found."
}

$artifactsPath = '.\artifacts'
New-Item -ItemType Directory -Path $artifactsPath -Force | Out-Null

Invoke-Step -Name 'Restore solution' -Tool 'dotnet' -Arguments @('restore', $SolutionPath, '--nologo')

$toolManifestExists = (Test-Path '.\dotnet-tools.json') -or (Test-Path '.\.config\dotnet-tools.json')
if ($toolManifestExists) {
    Invoke-Step -Name 'Restore local tools' -Tool 'dotnet' -Arguments @('tool', 'restore')
}

Invoke-Step -Name 'Verify formatting' -Tool 'dotnet' -Arguments @('format', $SolutionPath, '--verify-no-changes', '--verbosity', 'minimal')
Invoke-Step -Name 'Build (Release)' -Tool 'dotnet' -Arguments @('build', $SolutionPath, '-c', $Configuration, '--no-restore')
Invoke-Step -Name 'Run tests with coverage' -Tool 'dotnet' -Arguments @(
    'test',
    $TestsProjectPath,
    '-c',
    $Configuration,
    '--no-build',
    '--verbosity',
    'minimal',
    '--collect:XPlat Code Coverage',
    '--results-directory',
    '.\TestResults',
    '--',
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover'
)

Write-Host ""
Write-Host '==> Dependency vulnerability audit' -ForegroundColor Cyan
$auditReportPath = Join-Path $artifactsPath 'dependency-audit.json'
$auditJson = dotnet package list --project $SolutionPath --vulnerable --include-transitive --format json --output-version 1 --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    throw 'Dependency vulnerability audit command failed.'
}

$auditJson | Out-File -FilePath $auditReportPath -Encoding utf8
$auditReport = $auditJson | ConvertFrom-Json
$vulnerablePackages = Get-VulnerablePackages -AuditReport $auditReport
if (@($vulnerablePackages).Count -gt 0) {
    Write-Host 'Vulnerable packages were detected:' -ForegroundColor Red
    $vulnerablePackages | Sort-Object Project, Framework, Package | Format-Table -AutoSize | Out-Host
    throw "Dependency vulnerability audit failed. See $auditReportPath."
}

if ($WithSonar) {
    Write-Host ""
    Write-Host '==> Run SonarQube analysis' -ForegroundColor Cyan
    & '.\run-sonar.ps1' -Configuration $Configuration
}

Write-Host ""
Write-Host 'Quality checks completed successfully.' -ForegroundColor Green
