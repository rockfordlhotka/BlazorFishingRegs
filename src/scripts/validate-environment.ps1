#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates and tests environment configuration for the Blazor Fishing Regulations application.

.DESCRIPTION
    This script validates that all required environment variables and secrets are properly configured
    for the specified environment (Development, Staging, Production).

.PARAMETER Environment
    Target environment to validate (Development, Staging, Production)

.PARAMETER KeyVaultName
    Azure Key Vault name (for Production/Staging validation)

.PARAMETER CheckConnections
    Switch to test actual connections to services

.EXAMPLE
    .\validate-environment.ps1 -Environment Development
    Validates local development environment

.EXAMPLE
    .\validate-environment.ps1 -Environment Production -KeyVaultName "fishing-regs-kv" -CheckConnections
    Validates production environment and tests connections
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,
    
    [string]$KeyVaultName = "",
    [switch]$CheckConnections
)

$ErrorActionPreference = "Stop"

Write-Host "🔍 Environment Validation - Blazor Fishing Regulations" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# Validation results
$validationResults = @()

# Function to add validation result
function Add-ValidationResult {
    param(
        [string]$Component,
        [string]$Check,
        [bool]$Passed,
        [string]$Message = "",
        [string]$Value = ""
    )
    
    $result = [PSCustomObject]@{
        Component = $Component
        Check = $Check
        Passed = $Passed
        Message = $Message
        Value = $Value
    }
    
    $script:validationResults += $result
    
    $status = if ($Passed) { "✅" } else { "❌" }
    $color = if ($Passed) { "Green" } else { "Red" }
    
    Write-Host "$status $Component - $Check" -ForegroundColor $color
    if ($Message) {
        Write-Host "   $Message" -ForegroundColor Gray
    }
}

# Function to check environment variable
function Test-EnvironmentVariable {
    param(
        [string]$Name,
        [string]$Component,
        [bool]$Required = $true,
        [string]$Pattern = ""
    )
    
    $value = [Environment]::GetEnvironmentVariable($Name)
    $exists = -not [string]::IsNullOrWhiteSpace($value)
    
    if ($Required) {
        if ($exists) {
            if ($Pattern -and $value -notmatch $Pattern) {
                Add-ValidationResult $Component "Environment Variable: $Name" $false "Value doesn't match expected pattern" $value
            } else {
                Add-ValidationResult $Component "Environment Variable: $Name" $true "Set correctly" "***"
            }
        } else {
            Add-ValidationResult $Component "Environment Variable: $Name" $false "Missing required environment variable"
        }
    } else {
        if ($exists) {
            Add-ValidationResult $Component "Environment Variable: $Name" $true "Optional variable set" "***"
        } else {
            Add-ValidationResult $Component "Environment Variable: $Name" $true "Optional variable not set (OK)"
        }
    }
    
    return $value
}

# Function to test Key Vault secret
function Test-KeyVaultSecret {
    param(
        [string]$SecretName,
        [string]$Component,
        [bool]$Required = $true
    )
    
    if (-not $KeyVaultName) {
        Add-ValidationResult $Component "Key Vault Secret: $SecretName" $false "Key Vault name not provided"
        return $null
    }
    
    try {
        $secret = az keyvault secret show --vault-name $KeyVaultName --name $SecretName --query "value" -o tsv 2>$null
        if ($secret) {
            Add-ValidationResult $Component "Key Vault Secret: $SecretName" $true "Secret exists" "***"
            return $secret
        } else {
            Add-ValidationResult $Component "Key Vault Secret: $SecretName" $false "Secret not found"
            return $null
        }
    } catch {
        Add-ValidationResult $Component "Key Vault Secret: $SecretName" $false "Error accessing secret: $_"
        return $null
    }
}

# Function to test connection
function Test-Connection {
    param(
        [string]$Component,
        [string]$ConnectionString,
        [string]$Type
    )
    
    if (-not $CheckConnections -or [string]::IsNullOrWhiteSpace($ConnectionString)) {
        return
    }
    
    Write-Host "🔌 Testing $Component connection..." -ForegroundColor Yellow
    
    switch ($Type) {
        "SqlServer" {
            try {
                # Test SQL Server connection (simplified)
                if ($ConnectionString -match "Server=([^;]+)") {
                    $server = $matches[1]
                    $testResult = Test-NetConnection -ComputerName $server.Split(',')[0] -Port 1433 -InformationLevel Quiet
                    Add-ValidationResult $Component "Connection Test" $testResult "SQL Server connectivity"
                }
            } catch {
                Add-ValidationResult $Component "Connection Test" $false "Error testing SQL connection: $_"
            }
        }
        "Redis" {
            try {
                # Test Redis connection (simplified)
                if ($ConnectionString -match "([^:]+):(\d+)") {
                    $server = $matches[1]
                    $port = $matches[2]
                    $testResult = Test-NetConnection -ComputerName $server -Port $port -InformationLevel Quiet
                    Add-ValidationResult $Component "Connection Test" $testResult "Redis connectivity"
                }
            } catch {
                Add-ValidationResult $Component "Connection Test" $false "Error testing Redis connection: $_"
            }
        }
        "HttpEndpoint" {
            try {
                # Test HTTP endpoint
                $response = Invoke-WebRequest -Uri $ConnectionString -Method Head -TimeoutSec 10 -UseBasicParsing
                $testResult = $response.StatusCode -eq 200
                Add-ValidationResult $Component "Connection Test" $testResult "HTTP endpoint accessibility"
            } catch {
                Add-ValidationResult $Component "Connection Test" $false "Error testing HTTP endpoint: $_"
            }
        }
    }
}

Write-Host "`n🔧 Validating $Environment environment configuration..." -ForegroundColor Cyan

# Common validations for all environments
Write-Host "`n📋 Basic Configuration" -ForegroundColor Magenta
Test-EnvironmentVariable "ASPNETCORE_ENVIRONMENT" "Application"
Test-EnvironmentVariable "BLAZOR_FISHING_ENV" "Application"

# Environment-specific validations
switch ($Environment) {
    "Development" {
        Write-Host "`n🏠 Development Environment Validation" -ForegroundColor Magenta
        
        # Database
        $sqlConnection = Test-EnvironmentVariable "SQL_CONNECTION_STRING" "Database"
        Test-Connection "Database" $sqlConnection "SqlServer"
        
        # Redis
        $redisConnection = Test-EnvironmentVariable "REDIS_CONNECTION_STRING" "Cache"
        Test-Connection "Cache" $redisConnection "Redis"
        
        # Storage (local)
        Test-EnvironmentVariable "AZURE_STORAGE_CONNECTION_STRING" "Storage"
        Test-EnvironmentVariable "AZURE_STORAGE_CONTAINER_NAME" "Storage"
        
        # Mock AI services
        Test-EnvironmentVariable "USE_MOCK_AI_SERVICES" "AI Services"
        $mockUrl = Test-EnvironmentVariable "MOCK_AI_SERVICE_URL" "AI Services"
        Test-Connection "Mock AI Service" $mockUrl "HttpEndpoint"
        
        # Application secrets
        Test-EnvironmentVariable "FISHING_REGS_API_KEY" "Security"
        Test-EnvironmentVariable "FISHING_REGS_JWT_SECRET" "Security"
        
        # Logging
        $seqUrl = Test-EnvironmentVariable "SEQ_SERVER_URL" "Logging"
        Test-Connection "Seq Logging" $seqUrl "HttpEndpoint"
    }
    
    "Production" {
        Write-Host "`n🏭 Production Environment Validation" -ForegroundColor Magenta
        
        if ($KeyVaultName) {
            Write-Host "`n🔐 Validating Key Vault secrets..." -ForegroundColor Cyan
            
            # Azure services
            $openaiEndpoint = Test-KeyVaultSecret "azure-openai-endpoint" "Azure OpenAI"
            Test-KeyVaultSecret "azure-openai-api-key" "Azure OpenAI"
            Test-KeyVaultSecret "azure-openai-deployment-name" "Azure OpenAI"
            Test-Connection "Azure OpenAI" $openaiEndpoint "HttpEndpoint"
            
            $docIntelEndpoint = Test-KeyVaultSecret "azure-document-intelligence-endpoint" "Document Intelligence"
            Test-KeyVaultSecret "azure-document-intelligence-api-key" "Document Intelligence"
            Test-Connection "Document Intelligence" $docIntelEndpoint "HttpEndpoint"
            
            # Database and cache
            $sqlConnection = Test-KeyVaultSecret "sql-connection-string" "Database"
            $redisConnection = Test-KeyVaultSecret "redis-connection-string" "Cache"
            Test-Connection "Database" $sqlConnection "SqlServer"
            Test-Connection "Cache" $redisConnection "Redis"
            
            # Storage
            Test-KeyVaultSecret "azure-storage-connection-string" "Storage"
            
            # Application secrets
            Test-KeyVaultSecret "fishing-regs-api-key" "Security"
            Test-KeyVaultSecret "fishing-regs-jwt-secret" "Security"
            
            # Optional secrets
            Test-KeyVaultSecret "seq-api-key" "Logging" $false
            Test-KeyVaultSecret "application-insights-connection-string" "Monitoring" $false
            
        } else {
            # Check environment variables for production
            Write-Host "`n⚠️  Production validation without Key Vault - checking environment variables" -ForegroundColor Yellow
            
            Test-EnvironmentVariable "AZURE_OPENAI_ENDPOINT" "Azure OpenAI"
            Test-EnvironmentVariable "AZURE_OPENAI_API_KEY" "Azure OpenAI"
            Test-EnvironmentVariable "AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT" "Document Intelligence"
            Test-EnvironmentVariable "AZURE_DOCUMENT_INTELLIGENCE_API_KEY" "Document Intelligence"
            Test-EnvironmentVariable "SQL_CONNECTION_STRING" "Database"
            Test-EnvironmentVariable "REDIS_CONNECTION_STRING" "Cache"
            Test-EnvironmentVariable "AZURE_STORAGE_CONNECTION_STRING" "Storage"
            Test-EnvironmentVariable "FISHING_REGS_API_KEY" "Security"
            Test-EnvironmentVariable "FISHING_REGS_JWT_SECRET" "Security"
        }
        
        # Production-specific environment variables
        Test-EnvironmentVariable "ASPNETCORE_ALLOWED_HOSTS" "Security"
        Test-EnvironmentVariable "CORS_ALLOWED_ORIGINS" "Security"
        Test-EnvironmentVariable "USE_MOCK_AI_SERVICES" "AI Services" $true "^false$"
    }
}

# Security validations
Write-Host "`n🔒 Security Configuration" -ForegroundColor Magenta

$jwtSecret = [Environment]::GetEnvironmentVariable("FISHING_REGS_JWT_SECRET")
if ($jwtSecret) {
    $isSecureLength = $jwtSecret.Length -ge 32
    Add-ValidationResult "Security" "JWT Secret Length" $isSecureLength "JWT secret should be at least 32 characters"
}

if ($Environment -eq "Production") {
    $allowedHosts = [Environment]::GetEnvironmentVariable("ASPNETCORE_ALLOWED_HOSTS")
    $isSecureHosts = $allowedHosts -and $allowedHosts -ne "*"
    Add-ValidationResult "Security" "Allowed Hosts" $isSecureHosts "Production should not use wildcard allowed hosts"
}

# Generate summary report
Write-Host "`n📊 VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan

$totalChecks = $validationResults.Count
$passedChecks = ($validationResults | Where-Object { $_.Passed }).Count
$failedChecks = $totalChecks - $passedChecks

Write-Host "Environment: $Environment" -ForegroundColor White
Write-Host "Total Checks: $totalChecks" -ForegroundColor White
Write-Host "Passed: $passedChecks" -ForegroundColor Green
Write-Host "Failed: $failedChecks" -ForegroundColor Red

if ($failedChecks -gt 0) {
    Write-Host "`n❌ Failed Checks:" -ForegroundColor Red
    $validationResults | Where-Object { -not $_.Passed } | ForEach-Object {
        Write-Host "   $($_.Component) - $($_.Check): $($_.Message)" -ForegroundColor Red
    }
    
    Write-Host "`n🔧 REMEDIATION STEPS" -ForegroundColor Yellow
    Write-Host "===================" -ForegroundColor Yellow
    
    switch ($Environment) {
        "Development" {
            Write-Host "1. Run setup-dev-environment.ps1 to configure missing variables" -ForegroundColor Gray
            Write-Host "2. Start Docker containers: docker-compose up -d" -ForegroundColor Gray
            Write-Host "3. Check container status: docker-compose ps" -ForegroundColor Gray
        }
        "Production" {
            Write-Host "1. Run setup-azure-keyvault.ps1 to configure Key Vault secrets" -ForegroundColor Gray
            Write-Host "2. Update application configuration to use Key Vault" -ForegroundColor Gray
            Write-Host "3. Configure Managed Identity for app service" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "`n✅ All validation checks passed!" -ForegroundColor Green
    
    Write-Host "`n🚀 ENVIRONMENT READY" -ForegroundColor Green
    Write-Host "===================" -ForegroundColor Green
    
    switch ($Environment) {
        "Development" {
            Write-Host "Your development environment is properly configured!" -ForegroundColor Gray
            Write-Host "Next steps:" -ForegroundColor Gray
            Write-Host "1. Start development: docker-compose up -d" -ForegroundColor Gray
            Write-Host "2. Access application: https://localhost:8443" -ForegroundColor Gray
        }
        "Production" {
            Write-Host "Your production environment is properly configured!" -ForegroundColor Gray
            Write-Host "You can proceed with deployment." -ForegroundColor Gray
        }
    }
}

# Export results to file
$reportFile = "environment-validation-$Environment-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$validationResults | ConvertTo-Json -Depth 3 | Out-File $reportFile
Write-Host "`n📄 Validation report saved to: $reportFile" -ForegroundColor Cyan

exit $failedChecks
