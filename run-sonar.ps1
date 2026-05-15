param (
    [string]$Token = $env:SONAR_TOKEN,
    [string]$HostUrl = "http://localhost:9000",
    [string]$ProjectKey = "disphub"
)

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "ERROR: No SonarQube token provided. Set SONAR_TOKEN environment variable or pass -Token parameter." -ForegroundColor Red
    exit 1
}

Write-Host "Starting SonarQube Analysis..." -ForegroundColor Cyan

# 1. Start the scanner and configure it to look for coverage reports
dotnet sonarscanner begin /k:"$ProjectKey" /d:sonar.host.url="$HostUrl" /d:sonar.token="$Token" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" /d:sonar.exclusions="**/bin/**,**/obj/**,**/plan/**"

# 2. Build the project
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build DispHub.sln

# 3. Run tests and collect code coverage
Write-Host "Running tests with code coverage..." -ForegroundColor Cyan
dotnet test DispHub.sln --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# 4. End the scanner (this uploads the results to the dashboard)
Write-Host "Finalizing analysis and uploading to SonarQube..." -ForegroundColor Cyan
dotnet sonarscanner end /d:sonar.token="$Token"

Write-Host "Analysis complete! Check the dashboard at $HostUrl/dashboard?id=$ProjectKey" -ForegroundColor Green
