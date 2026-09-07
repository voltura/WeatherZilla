$ErrorActionPreference = 'Stop'
# Read the same connection used by the deployed app; never fall back to a developer database.
$connection = & az webapp config connection-string list --name $env:AZURE_WEBAPP_NAME --resource-group $env:AZURE_APIM_RESOURCEGROUP --query "[?name=='DefaultConnection'].connectionString | [0]" -o tsv
if ($LASTEXITCODE -ne 0) { throw 'Could not read Azure connection settings.' }
if ([string]::IsNullOrWhiteSpace($connection)) {
    $connection = & az webapp config appsettings list --name $env:AZURE_WEBAPP_NAME --resource-group $env:AZURE_APIM_RESOURCEGROUP --query "[?name=='ConnectionStrings__DefaultConnection' || name=='ConnectionStrings:DefaultConnection'].value | [0]" -o tsv
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Azure application settings.' }
}
if ([string]::IsNullOrWhiteSpace($connection)) { throw 'Configure DefaultConnection on the Azure Web App before deploying.' }
# Mask before running any tool that could mention the configured connection.
Write-Output "::add-mask::$connection"
$previousConnection = $env:ConnectionStrings__DefaultConnection
try {
    $env:ConnectionStrings__DefaultConnection = $connection
    Push-Location (Join-Path $PSScriptRoot '../WeatherZilla.WebApp')
    try {
        & dotnet ef database update --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) { throw 'Database migration failed; application deployment must stop.' }
    } finally { Pop-Location }
} finally { $env:ConnectionStrings__DefaultConnection = $previousConnection }
