# Database Setup Script for Fishing Regulations App
# This script sets up the PostgreSQL database schema and sample data

param(
    [Parameter(Mandatory=$false)]
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=fishing_regs;Username=someone;Password=jfd92JQ%1mx01",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "fishing_regs",
    
    [Parameter(Mandatory=$false)]
    [switch]$DropExisting,
    
    [Parameter(Mandatory=$false)]
    [switch]$SampleDataOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipSampleData
)

$ErrorActionPreference = "Stop"

Write-Host @"
🐟 ============================================================= 🐟
   Fishing Regulations Database Setup
🐟 ============================================================= 🐟
"@ -ForegroundColor Cyan

# Check if psql is available
if (-not (Get-Command "psql" -ErrorAction SilentlyContinue)) {
    Write-Error "❌ PostgreSQL client (psql) is not installed or not in PATH."
    Write-Host "Please install PostgreSQL client tools first."
    exit 1
}

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$schemaFile = Join-Path $scriptDir "schema.sql"
$sampleDataFile = Join-Path $scriptDir "sample-data.sql"

# Verify files exist
if (-not (Test-Path $schemaFile) -and -not $SampleDataOnly) {
    Write-Error "❌ Schema file not found: $schemaFile"
    exit 1
}

if (-not (Test-Path $sampleDataFile) -and -not $SkipSampleData) {
    Write-Error "❌ Sample data file not found: $sampleDataFile"
    exit 1
}

Write-Host "📂 Using files:" -ForegroundColor Green
if (-not $SampleDataOnly) {
    Write-Host "   Schema: $schemaFile" -ForegroundColor Gray
}
if (-not $SkipSampleData) {
    Write-Host "   Sample Data: $sampleDataFile" -ForegroundColor Gray
}

try {
    # Parse connection string to get connection info
    $connParts = @{}
    $ConnectionString.Split(';') | ForEach-Object {
        if ($_ -match '(.+)=(.+)') {
            $connParts[$matches[1]] = $matches[2]
        }
    }

    $pgHost = $connParts['Host'] ?? 'localhost'
    $port = $connParts['Port'] ?? '5432'
    $username = $connParts['Username'] ?? 'postgres'
    $database = $connParts['Database'] ?? $DatabaseName

    Write-Host "🔌 Connection Details:" -ForegroundColor Cyan
    Write-Host "   Host: $pgHost" -ForegroundColor White
    Write-Host "   Port: $port" -ForegroundColor White
    Write-Host "   Database: $database" -ForegroundColor White
    Write-Host "   Username: $username" -ForegroundColor White
    Write-Host ""

    # Test connection to PostgreSQL server (connect to postgres db first)
    Write-Host "🔍 Testing PostgreSQL connection..." -ForegroundColor Cyan
    $testConnString = "host=$pgHost port=$port dbname=postgres user=$username"
    
    & psql $testConnString -c "SELECT version();" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Cannot connect to PostgreSQL server. Please check your connection details and ensure PostgreSQL is running."
        Write-Host "Connection string used: $testConnString" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host "✅ PostgreSQL connection successful" -ForegroundColor Green

    # Check if database exists
    $dbCheckResult = & psql $testConnString -t -c "SELECT 1 FROM pg_database WHERE datname='$database';" 2>$null
    $databaseExists = $dbCheckResult -and $dbCheckResult.Trim() -eq "1"

    if ($DropExisting -and $databaseExists) {
        Write-Host "🗑️  Dropping existing database '$database'..." -ForegroundColor Yellow
        & psql $testConnString -c "DROP DATABASE IF EXISTS $database;" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to drop database '$database'"
            exit 1
        }
        $databaseExists = $false
    }

    # Create database if it doesn't exist
    if (-not $databaseExists -and -not $SampleDataOnly) {
        Write-Host "📦 Creating database '$database'..." -ForegroundColor Green
        & psql $testConnString -c "CREATE DATABASE $database;" | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to create database '$database'"
            exit 1
        }
        Write-Host "✅ Database '$database' created successfully" -ForegroundColor Green
    }
    elseif (-not $databaseExists -and $SampleDataOnly) {
        Write-Error "❌ Database '$database' does not exist. Cannot load sample data only."
        exit 1
    }

    # Connection string for the target database
    $targetConnString = "host=$pgHost port=$port dbname=$database user=$username"

    # Run schema setup
    if (-not $SampleDataOnly) {
        Write-Host "🏗️  Setting up database schema..." -ForegroundColor Green
        $schemaResult = & psql $targetConnString -f $schemaFile 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to execute schema file"
            Write-Host "Error output:" -ForegroundColor Red
            Write-Host $schemaResult -ForegroundColor Red
            exit 1
        }
        Write-Host "✅ Database schema created successfully" -ForegroundColor Green
    }

    # Load sample data
    if (-not $SkipSampleData) {
        Write-Host "📊 Loading sample data..." -ForegroundColor Green
        $sampleResult = & psql $targetConnString -f $sampleDataFile 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "❌ Failed to load sample data"
            Write-Host "Error output:" -ForegroundColor Red
            Write-Host $sampleResult -ForegroundColor Red
            exit 1
        }
        Write-Host "✅ Sample data loaded successfully" -ForegroundColor Green
    }

    # Verify setup
    Write-Host "✅ Verifying database setup..." -ForegroundColor Cyan
    $tableCount = & psql $targetConnString -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>$null
    $tableCount = $tableCount.Trim()
    
    Write-Host "📊 Database Statistics:" -ForegroundColor Cyan
    Write-Host "   Tables created: $tableCount" -ForegroundColor White
    
    if (-not $SkipSampleData) {
        $stats = & psql $targetConnString -t -c @"
SELECT 
    (SELECT COUNT(*) FROM states) as states,
    (SELECT COUNT(*) FROM counties) as counties,
    (SELECT COUNT(*) FROM fish_species) as fish_species,
    (SELECT COUNT(*) FROM water_bodies) as water_bodies,
    (SELECT COUNT(*) FROM fishing_regulations) as regulations;
"@ 2>$null
        
        if ($stats) {
            $statsParts = $stats.Trim() -split '\|'
            Write-Host "   States: $($statsParts[0].Trim())" -ForegroundColor White
            Write-Host "   Counties: $($statsParts[1].Trim())" -ForegroundColor White
            Write-Host "   Fish Species: $($statsParts[2].Trim())" -ForegroundColor White
            Write-Host "   Water Bodies: $($statsParts[3].Trim())" -ForegroundColor White
            Write-Host "   Fishing Regulations: $($statsParts[4].Trim())" -ForegroundColor White
        }
    }

    Write-Host ""
    Write-Host "🎉 Database setup completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🔗 Connection String for your application:" -ForegroundColor Cyan
    Write-Host "   $ConnectionString" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "🛠️  Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Update your appsettings.json with the connection string" -ForegroundColor White
    Write-Host "   2. Run Entity Framework migrations if using code-first approach" -ForegroundColor White
    Write-Host "   3. Test the connection from your Blazor application" -ForegroundColor White

} catch {
    Write-Host ""
    Write-Host "❌ Database setup failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "🔧 Troubleshooting Tips:" -ForegroundColor Yellow
    Write-Host "   1. Ensure PostgreSQL server is running" -ForegroundColor White
    Write-Host "   2. Verify connection details (host, port, username, password)" -ForegroundColor White
    Write-Host "   3. Check that the user has CREATE DATABASE privileges" -ForegroundColor White
    Write-Host "   4. Ensure the database name doesn't conflict with existing databases" -ForegroundColor White
    Write-Host "   5. Check PostgreSQL logs for detailed error messages" -ForegroundColor White
    
    exit 1
}
