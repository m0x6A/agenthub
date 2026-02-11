#!/bin/bash
# AgentBus Local Development Setup Script (Bash - Linux/macOS)
# Usage: chmod +x setup-local-dev.sh && ./setup-local-dev.sh

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
header() {
    echo -e "${BLUE}"
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║ $1                                             "
    echo "╚════════════════════════════════════════════════════════════╝"
    echo -e "${NC}"
}

success() {
    echo -e "${GREEN}✓ $1${NC}"
}

error() {
    echo -e "${RED}✗ $1${NC}"
}

info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

check_docker() {
    if command -v docker &> /dev/null; then
        local docker_version=$(docker --version)
        success "Docker is installed: $docker_version"
        return 0
    else
        error "Docker is not installed"
        info "Download Docker Desktop from: https://www.docker.com/products/docker-desktop"
        return 1
    fi
}

check_dotnet() {
    if command -v dotnet &> /dev/null; then
        local dotnet_version=$(dotnet --version)
        success ".NET SDK is installed: $dotnet_version"
        return 0
    else
        error ".NET SDK is not installed"
        info "Download from: https://dotnet.microsoft.com/en-us/download/dotnet/9.0"
        return 1
    fi
}

start_services() {
    header "Starting Local Services (Cosmos DB + Service Bus)"
    
    if ! check_docker; then
        error "Docker required to start services"
        exit 1
    fi
    
    info "Pulling latest images..."
    docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
    docker pull mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
    
    info "Starting services with docker-compose..."
    docker-compose up -d
    
    info "Waiting for services to be ready (30-60 seconds)..."
    sleep 10
    
    # Check Cosmos DB
    info "Checking Cosmos DB Emulator..."
    for i in {1..12}; do
        if curl -s --insecure https://localhost:8081/ > /dev/null 2>&1; then
            success "Cosmos DB Emulator is ready on https://localhost:8081"
            break
        fi
        sleep 5
    done
    
    # Check Service Bus
    info "Checking Service Bus Emulator..."
    for i in {1..12}; do
        if curl -s http://localhost:9600/health > /dev/null 2>&1; then
            success "Service Bus Emulator is ready on http://localhost:9600"
            break
        fi
        sleep 5
    done
    
    success "All services started successfully!"
    info "Run 'docker-compose logs -f' to view logs"
}

stop_services() {
    header "Stopping Local Services"
    info "Stopping docker-compose services..."
    docker-compose down
    success "Services stopped"
}

build_and_run() {
    header "Building and Running AgentBus.Broker"
    
    if ! check_dotnet; then
        error ".NET SDK required to build and run"
        exit 1
    fi
    
    local broker_path="src/AgentBus.Broker"
    
    if [ ! -d "$broker_path" ]; then
        error "AgentBus.Broker project not found at $broker_path"
        exit 1
    fi
    
    info "Restoring NuGet packages..."
    cd "$broker_path"
    dotnet restore
    
    info "Building project..."
    dotnet build -c Debug
    
    success "Build completed!"
    info "Starting AgentBus.Broker..."
    info "Press Ctrl+C to stop the application"
    
    export ASPNETCORE_ENVIRONMENT=Development
    dotnet run
}

check_prerequisites() {
    header "Checking Prerequisites"
    local all_good=true
    
    if ! check_docker; then all_good=false; fi
    if ! check_dotnet; then all_good=false; fi
    
    if [ "$all_good" = true ]; then
        success "All prerequisites are installed!"
    else
        warning "Please install missing prerequisites above"
    fi
}

show_menu() {
    echo ""
    echo -e "${BLUE}AgentBus Local Development Setup${NC}"
    echo "================================="
    echo "1. Start local services (Cosmos DB + Service Bus)"
    echo "2. Stop local services"
    echo "3. View service logs"
    echo "4. Build and run AgentBus.Broker"
    echo "5. Check prerequisites"
    echo "6. Full setup (start services → build → run)"
    echo "0. Exit"
    echo ""
}

# Main script logic
if [ $# -gt 0 ]; then
    case "$1" in
        --start-services)
            start_services
            ;;
        --stop-services)
            stop_services
            ;;
        --build-and-run)
            build_and_run
            ;;
        --check)
            check_prerequisites
            ;;
        *)
            echo "Usage: $0 [--start-services|--stop-services|--build-and-run|--check]"
            exit 1
            ;;
    esac
else
    check_prerequisites
    
    while true; do
        show_menu
        read -p "Select option: " choice
        
        case $choice in
            1)
                start_services
                ;;
            2)
                stop_services
                ;;
            3)
                header "Viewing Service Logs"
                info "Press Ctrl+C to exit"
                docker-compose logs -f
                ;;
            4)
                build_and_run
                ;;
            5)
                check_prerequisites
                ;;
            6)
                start_services
                info "Services started. Waiting 15 seconds before build..."
                sleep 15
                build_and_run
                ;;
            0)
                info "Goodbye!"
                exit 0
                ;;
            *)
                warning "Invalid option. Please try again."
                ;;
        esac
    done
fi
