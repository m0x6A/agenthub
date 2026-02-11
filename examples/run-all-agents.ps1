#!/usr/bin/env pwsh
# Run all example agents in separate terminals

Write-Host "🚀 Starting AgentBus Example Agents Demo" -ForegroundColor Cyan
Write-Host ""

$examplesRoot = $PSScriptRoot
$projectRoot = Split-Path $examplesRoot -Parent

# Check if .NET is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "❌ .NET SDK not found. Please install .NET 9.0 SDK" -ForegroundColor Red
    exit 1
}

# Check if running in VS Code integrated terminal
$isVSCode = $env:TERM_PROGRAM -eq "vscode"

function Start-Agent {
    param(
        [string]$Name,
        [string]$Project,
        [int]$Delay
    )
    
    Write-Host "⏳ Starting $Name (waiting ${Delay}s)..." -ForegroundColor Yellow
    Start-Sleep -Seconds $Delay
    
    $projectPath = Join-Path $examplesRoot $Project
    
    if ($isVSCode) {
        # Use VS Code terminal API
        $terminalName = "🤖 $Name"
        # Note: This requires VS Code terminal integration
        Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$projectPath'; Write-Host '🤖 $Name' -ForegroundColor Cyan; dotnet run"
    } else {
        # Use Windows Terminal or new PowerShell window
        Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$projectPath'; `$host.UI.RawUI.WindowTitle='$Name'; dotnet run"
    }
    
    Write-Host "✅ $Name started" -ForegroundColor Green
}

Write-Host "📝 Agent Launch Sequence:" -ForegroundColor Cyan
Write-Host "   1️⃣  Customer Experience Agent (starts immediately, initiates inquiry)"
Write-Host "   2️⃣  Operations & Inventory Agent (8s delay, checks inventory)"
Write-Host "   3️⃣  Financial Authorization Agent (12s delay, authorizes payment)"
Write-Host "   4️⃣  Internal Communications Agent (15s delay, generates notifications)"
Write-Host ""
Write-Host "💡 Each agent subscribes to the GLOBAL event stream" -ForegroundColor Yellow
Write-Host "💡 Watch the console output to see the complete workflow" -ForegroundColor Yellow
Write-Host ""

# Start agents with staggered delays to demonstrate flow
Start-Agent -Name "Customer Experience" -Project "AgentBus.Examples.CustomerExperience" -Delay 0
Start-Agent -Name "Operations & Inventory" -Project "AgentBus.Examples.OperationsInventory" -Delay 3
Start-Agent -Name "Financial Authorization" -Project "AgentBus.Examples.FinancialAuth" -Delay 6
Start-Agent -Name "Internal Communications" -Project "AgentBus.Examples.InternalComms" -Delay 9

Write-Host ""
Write-Host "🎉 All agents launched!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Expected Flow:" -ForegroundColor Cyan
Write-Host "   Customer Agent    → customer.inquiry.received"
Write-Host "   Operations Agent  → operations.inventory.checked"
Write-Host "   Financial Agent   → financial.authorization.completed + order.confirmed"
Write-Host "   Customer Agent    → Receives confirmation, responds to customer"
Write-Host "   Internal Comms    → Generates Teams notifications & dashboard summary"
Write-Host ""
Write-Host "🛑 Press Ctrl+C in each terminal to stop agents" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  NOTE: Agents are running in MOCK mode (no real AgentBus or APIs)" -ForegroundColor Yellow
Write-Host "   To run with real services, set environment variables:" -ForegroundColor Yellow
Write-Host "   - AGENTBUS_URL=http://localhost:5000" -ForegroundColor Gray
Write-Host "   - OPENAI_API_KEY=your-key" -ForegroundColor Gray
Write-Host "   - ANTHROPIC_API_KEY=your-key" -ForegroundColor Gray
