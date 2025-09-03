# Setup Azure PostgreSQL for Fishing Regulations App
# This script helps configure Azure Database for PostgreSQL

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$ServerName,
    
    [Parameter(Mandatory=$true)]
    [string]$AdminUsername,
    
    [Parameter(Mandatory=$true)]
    [SecureString]$AdminPassword,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "East US",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "FishingRegsDB",
    
    [Parameter(Mandatory=$false)]
    [string]$SkuName = "Standard_B2s",
    
    [Parameter(Mandatory=$false)]
    [int]$StorageSize = 32,
    
    [Parameter(Mandatory=$false)]
    [string]$PostgreSQLVersion = "15"
)

Write-Host "🐘 Setting up Azure Database for PostgreSQL..." -ForegroundColor Cyan

# Check if Azure CLI is installed
if (-not (Get-Command "az" -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it first: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Login check
$loginStatus = az account show --query "user.name" -o tsv 2>$null
if (-not $loginStatus) {
    Write-Host "⚠️  Please login to Azure first..." -ForegroundColor Yellow
    az login
}

try {
    # Create resource group if it doesn't exist
    Write-Host "📁 Creating/verifying resource group: $ResourceGroupName" -ForegroundColor Green
    az group create --name $ResourceGroupName --location $Location --output none

    # Convert SecureString to plain text for Azure CLI
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($AdminPassword)
    $PlainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

    # Create PostgreSQL server
    Write-Host "🐘 Creating PostgreSQL server: $ServerName" -ForegroundColor Green
    az postgres flexible-server create `
        --resource-group $ResourceGroupName `
        --name $ServerName `
        --admin-user $AdminUsername `
        --admin-password $PlainPassword `
        --location $Location `
        --sku-name $SkuName `
        --storage-size $StorageSize `
        --version $PostgreSQLVersion `
        --public-access 0.0.0.0 `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create PostgreSQL server"
    }

    # Create the database
    Write-Host "🗄️  Creating database: $DatabaseName" -ForegroundColor Green
    az postgres flexible-server db create `
        --resource-group $ResourceGroupName `
        --server-name $ServerName `
        --database-name $DatabaseName `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create database"
    }

    # Configure firewall rule for Azure services
    Write-Host "🔥 Configuring firewall rules..." -ForegroundColor Green
    az postgres flexible-server firewall-rule create `
        --resource-group $ResourceGroupName `
        --name $ServerName `
        --rule-name "AllowAzureServices" `
        --start-ip-address 0.0.0.0 `
        --end-ip-address 0.0.0.0 `
        --output none

    # Get connection string
    $connectionString = "Host=$ServerName.postgres.database.azure.com;Port=5432;Database=$DatabaseName;Username=$AdminUsername;Password=$PlainPassword;SslMode=Require;Trust Server Certificate=true"

    Write-Host "✅ Azure PostgreSQL setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "📋 Connection Details:" -ForegroundColor Cyan
    Write-Host "  Server: $ServerName.postgres.database.azure.com" -ForegroundColor White
    Write-Host "  Database: $DatabaseName" -ForegroundColor White
    Write-Host "  Username: $AdminUsername" -ForegroundColor White
    Write-Host "  Port: 5432" -ForegroundColor White
    Write-Host ""
    Write-Host "🔐 Connection String:" -ForegroundColor Cyan
    Write-Host "  $connectionString" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "⚙️  Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Update your user secrets or app settings with the connection string" -ForegroundColor White
    Write-Host "  2. Configure your firewall rules as needed for your IP" -ForegroundColor White
    Write-Host "  3. Consider using Azure Key Vault for production secrets" -ForegroundColor White
    Write-Host ""
    Write-Host "🔧 To add your IP to firewall:" -ForegroundColor Cyan
    Write-Host "  az postgres flexible-server firewall-rule create --resource-group $ResourceGroupName --name $ServerName --rule-name MyIP --start-ip-address YOUR_IP --end-ip-address YOUR_IP" -ForegroundColor Yellow

    # Clean up the plain text password
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

} catch {
    Write-Error "❌ Failed to setup Azure PostgreSQL: $_"
    exit 1
}
