# AgentBus Local Development Setup Script (Windows PowerShell)
# Usage: .\setup-local-dev.ps1

param(
    [switch]$StartServices,
    [switch]$BuildAndRun,
    [switch]$StopServices,
    [switch]$ViewLogs
)

$ErrorActionPreference = "Stop"

# Colors for output
$successColor = "Green"
$errorColor = "Red"
$infoColor = "Cyan"
$warningColor = "Yellow"

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor $infoColor
    Write-Host "║ $Message" -ForegroundColor $infoColor
    Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor $infoColor
    Write-Host ""
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor $successColor
}

function Write-Error-Custom {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor $errorColor
}

function Write-Info {
    param([string]$Message)
    Write-Host "ℹ $Message" -ForegroundColor $infoColor
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor $warningColor
}

function Check-Docker {
    try {
        $dockerVersion = docker version --format "{{.Client.Version}}"
        Write-Success "Docker is installed: $dockerVersion"
        return $true
    }
    catch {
        Write-Error-Custom "Docker is not installed or not in PATH"
        Write-Info "Download Docker Desktop from: https://www.docker.com/products/docker-desktop"
        return $false
    }
}

function Check-DotNet {
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK is installed: $dotnetVersion"
        return $true
    }
    catch {
        Write-Error-Custom ".NET SDK is not installed"
        Write-Info "Download from: https://dotnet.microsoft.com/en-us/download/dotnet/9.0"
        return $false
    }
}

function Start-Local-Services {
    Write-Header "Starting Local Services (Cosmos DB + Service Bus)"
    
    if (!(Check-Docker)) {
        throw "Docker required to start services"
    }
    
    try {
        Write-Info "Pulling latest images..."
        docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
        docker pull mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
        
        Write-Info "Starting services with docker-compose..."
        docker-compose up -d
        
        Write-Info "Waiting for services to be ready (30-60 seconds)..."
        Start-Sleep -Seconds 10
        
        # Check Cosmos DB
        Write-Info "Checking Cosmos DB Emulator..."
        $maxRetries = 12
        $retries = 0
        while ($retries -lt $maxRetries) {
            try {
                $response = curl.exe -s --insecure https://localhost:8081/ -o $null -w "%{http_code}"
                if ($response -eq "200") {
                    Write-Success "Cosmos DB Emulator is ready on https://localhost:8081"
                    break
                }
            }
            catch {
                Start-Sleep -Seconds 5
                $retries++
            }
        }
        
        # Check Service Bus
        Write-Info "Checking Service Bus Emulator..."
        $retries = 0
        while ($retries -lt $maxRetries) {
            try {
                $response = curl.exe -s http://localhost:9600/health -o $null -w "%{http_code}"
                if ($response -eq "200") {
                    Write-Success "Service Bus Emulator is ready on http://localhost:9600"
                    break
                }
            }
            catch {
                Start-Sleep -Seconds 5
                $retries++
            }
        }
        
        Write-Success "All services started successfully!"
        Write-Info "Run 'docker-compose logs -f' to view logs"
    }
    catch {
        Write-Error-Custom "Failed to start services: $_"
        throw
    }
}

function Stop-Local-Services {
    Write-Header "Stopping Local Services"
    
    try {
        Write-Info "Stopping docker-compose services..."
        docker-compose down
        Write-Success "Services stopped"
    }
    catch {
        Write-Error-Custom "Failed to stop services: $_"
    }
}

function View-Service-Logs {
    Write-Header "Viewing Service Logs"
    Write-Info "Press Ctrl+C to exit"
    docker-compose logs -f
}

function Build-And-Run {
    Write-Header "Building and Running AgentBus.Broker"
    
    if (!(Check-DotNet)) {
        throw ".NET SDK required to build and run"
    }
    
    try {
        $brokerPath = "src\AgentBus.Broker"
        
        if (!(Test-Path $brokerPath)) {
            Write-Error-Custom "AgentBus.Broker project not found at $brokerPath"
            throw "Project path invalid"
        }
        
        Write-Info "Restoring NuGet packages..."
        Set-Location $brokerPath
        dotnet restore
        
        Write-Info "Building project..."
        dotnet build -c Debug
        
        Write-Success "Build completed!"
        Write-Info "Starting AgentBus.Broker..."
        Write-Info "Press Ctrl+C to stop the application"
        
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run
    }
    catch {
        Write-Error-Custom "Build/Run failed: $_"
        throw
    }
    finally {
        Set-Location $PSScriptRoot
    }
}

function Show-Menu {
    Write-Host ""
    Write-Host "AgentBus Local Development Setup" -ForegroundColor $infoColor
    Write-Host "=================================" -ForegroundColor $infoColor
    Write-Host "1. Start local services (Cosmos DB + Service Bus)"
    Write-Host "2. Stop local services"
    Write-Host "3. View service logs"
    Write-Host "4. Build and run AgentBus.Broker"
    Write-Host "5. Check prerequisites"
    Write-Host "6. Full setup (start services → build → run)"
    Write-Host "0. Exit"
    Write-Host ""
}

function Check-Prerequisites {
    Write-Header "Checking Prerequisites"
    
    $allGood = $true
    
    if (!(Check-Docker)) { $allGood = $false }
    if (!(Check-DotNet)) { $allGood = $false }
    
    if ($allGood) {
        Write-Success "All prerequisites are installed!"
    }
    else {
        Write-Warning-Custom "Please install missing prerequisites above"
    }
}

# Main script logic
if ($StartServices) {
    Start-Local-Services
}
elseif ($StopServices) {
    Stop-Local-Services
}
elseif ($ViewLogs) {
    View-Service-Logs
}
elseif ($BuildAndRun) {
    Build-And-Run
}
else {
    # Interactive menu
    Check-Prerequisites
    
    do {
        Show-Menu
        $choice = Read-Host "Select option"
        
        switch ($choice) {
            "1" { Start-Local-Services }
            "2" { Stop-Local-Services }
            "3" { View-Service-Logs }
            "4" { Build-And-Run }
            "5" { Check-Prerequisites }
            "6" {
                Start-Local-Services
                Write-Info "Services started. Waiting 15 seconds before build..."
                Start-Sleep -Seconds 15
                Build-And-Run
            }
            "0" { 
                Write-Info "Goodbye!"
                exit 0
            }
            default { Write-Warning-Custom "Invalid option. Please try again." }
        }
    } while ($true)
}
