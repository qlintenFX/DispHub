param (
    [string]$Token = "sqp_57b77fca2c96accdc3473b4b0b8fe0d9f2f9ef22",
    [string]$HostUrl = "http://localhost:9000",
    [string]$ProjectKey = "disphub"
)

Write-Host "Starting SonarQube Analysis..." -ForegroundColor Cyan

# 1. Start the scanner and configure it to look for coverage reports
dotnet sonarscanner begin /k:"$ProjectKey" /d:sonar.host.url="$HostUrl" /d:sonar.token="$Token" /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" /d:sonar.exclusions="**/bin/**,**/obj/**,**/plan/**"

# 2. Build the project
Write-Host "Building project..." -ForegroundColor Cyan
dotnet build DispHub.sln

# 3. Run tests and collect code coverage
Write-Host "Running tests with code coverage..." -ForegroundColor Cyan
dotnet test DispHub.sln --collect:"XPlat Code Coverage" --DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# 4. End the scanner (this uploads the results to the dashboard)
Write-Host "Finalizing analysis and uploading to SonarQube..." -ForegroundColor Cyan
dotnet sonarscanner end /d:sonar.token="$Token"

Write-Host "Analysis complete! Check the dashboard at $HostUrl/dashboard?id=$ProjectKey" -ForegroundColor Green
