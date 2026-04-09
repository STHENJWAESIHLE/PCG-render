# Azure Deployment Script for PCG DMS
# Run this in PowerShell after installing Azure CLI

# 1. Login to Azure
az login

# 2. Create resource group (change location if needed)
$resourceGroup = "pcg-dms-rg"
$location = "southafricanorth"  # or westeurope, eastus, etc.
$appName = "pcg-dms-$(Get-Random -Minimum 1000 -Maximum 9999)"

Write-Host "Creating resource group..." -ForegroundColor Green
az group create --name $resourceGroup --location $location

# 3. Create App Service plan (Free tier F1)
Write-Host "Creating App Service plan..." -ForegroundColor Green
az appservice plan create --name "$appName-plan" --resource-group $resourceGroup --sku F1 --is-linux

# 4. Create Web App
Write-Host "Creating Web App..." -ForegroundColor Green
az webapp create --name $appName --resource-group $resourceGroup --plan "$appName-plan" --runtime "DOTNETCORE:9.0"

# 5. Configure settings
Write-Host "Configuring app settings..." -ForegroundColor Green
az webapp config appsettings set --name $appName --resource-group $resourceGroup --settings @'
[
  {"name": "ASPNETCORE_ENVIRONMENT", "value": "Production"}
]
'@

# 6. Get publish profile for GitHub Actions
Write-Host "Getting publish profile..." -ForegroundColor Green
$publishProfile = az webapp deployment list-publishing-profiles --name $appName --resource-group $resourceGroup --xml
$publishProfile | Out-File -FilePath "./publish-profile.xml"

Write-Host "`n===================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "App URL: https://$appName.azurewebsites.net"
Write-Host "Resource Group: $resourceGroup"
Write-Host "`nNext steps:"
Write-Host "1. Save the publish-profile.xml content as GitHub secret 'AZUREAPPSERVICE_PUBLISHPROFILE'"
Write-Host "2. Update your connection string in Azure Portal -> Configuration"
Write-Host "3. Add your OpenAI API keys in Azure Portal -> Configuration"
Write-Host "`nTo deploy locally now:"
Write-Host "dotnet publish -c Release -o ./publish"
Write-Host "az webapp deployment source config-zip --resource-group $resourceGroup --name $appName --src ./publish.zip"
