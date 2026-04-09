# Azure Deployment Guide

## Quick Deploy (PowerShell)

```powershell
# Install Azure CLI first: https://aka.ms/installazurecliwindows
# Then run:
.\deploy-azure.ps1
```

This creates:
- Free App Service (F1 tier)
- SQL Server database (you'll need to add connection string manually)
- Automatic GitHub Actions deployment setup

## Manual Deploy (ZIP)

```powershell
# 1. Publish
mkdir publish
dotnet publish PCG/PCG.csproj -c Release -o ./publish

# 2. Create ZIP
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

# 3. Deploy via Azure Portal:
# - Go to your App Service -> Deployment Center
# - Upload publish.zip
```

## Required Azure Settings

After deployment, add these in Azure Portal -> Configuration:

| Setting Name | Value |
|-------------|-------|
| `ConnectionStrings__DefaultConnection` | `Server=...;Database=PCG;User Id=...;Password=...;` |
| `InvoiceExtraction__OpenAIApiKey` | Your OpenAI key (optional) |
| `Insights__OpenAIApiKey` | Your OpenAI key (optional) |

## Database Setup

```powershell
# Run migrations on production database
dotnet ef database update --connection "your-connection-string"
```

Or use Azure Portal -> App Service -> Console:
```
dotnet ef database update
```

## URLs After Deploy

- Website: `https://<app-name>.azurewebsites.net`
- Demo login with any demo account from the login page

## Troubleshooting

**500 errors?** Check Application Insights or App Service Logs.
**Database connection failed?** Verify connection string format and firewall rules.
**Slow uploads?** Free tier has limited resources - upgrade to Basic ($13/mo) for production.
