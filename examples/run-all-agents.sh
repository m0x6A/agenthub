#!/bin/bash
# Run all example agents in separate terminals

echo "🚀 Starting AgentBus Example Agents Demo"
echo ""

EXAMPLES_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$EXAMPLES_ROOT")"

# Check if .NET is available
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 9.0 SDK"
    exit 1
fi

# Detect terminal emulator
if command -v gnome-terminal &> /dev/null; then
    TERMINAL_CMD="gnome-terminal --"
elif command -v xterm &> /dev/null; then
    TERMINAL_CMD="xterm -e"
elif command -v konsole &> /dev/null; then
    TERMINAL_CMD="konsole -e"
else
    echo "⚠️  No supported terminal emulator found (gnome-terminal, xterm, konsole)"
    echo "   Running agents in background..."
    TERMINAL_CMD="bash -c"
fi

function start_agent() {
    local name=$1
    local project=$2
    local delay=$3
    
    echo "⏳ Starting $name (waiting ${delay}s)..."
    sleep $delay
    
    local project_path="$EXAMPLES_ROOT/$project"
    
    $TERMINAL_CMD "cd '$project_path'; echo '🤖 $name'; dotnet run; exec bash" &
    
    echo "✅ $name started"
}

echo "📝 Agent Launch Sequence:"
echo "   1️⃣  Customer Experience Agent (starts immediately, initiates inquiry)"
echo "   2️⃣  Operations & Inventory Agent (8s delay, checks inventory)"
echo "   3️⃣  Financial Authorization Agent (12s delay, authorizes payment)"
echo "   4️⃣  Internal Communications Agent (15s delay, generates notifications)"
echo ""
echo "💡 Each agent subscribes to the GLOBAL event stream"
echo "💡 Watch the console output to see the complete workflow"
echo ""

# Start agents with staggered delays
start_agent "Customer Experience" "AgentBus.Examples.CustomerExperience" 0 &
start_agent "Operations & Inventory" "AgentBus.Examples.OperationsInventory" 3 &
start_agent "Financial Authorization" "AgentBus.Examples.FinancialAuth" 6 &
start_agent "Internal Communications" "AgentBus.Examples.InternalComms" 9 &

wait

echo ""
echo "🎉 All agents launched!"
echo ""
echo "📊 Expected Flow:"
echo "   Customer Agent    → customer.inquiry.received"
echo "   Operations Agent  → operations.inventory.checked"
echo "   Financial Agent   → financial.authorization.completed + order.confirmed"
echo "   Customer Agent    → Receives confirmation, responds to customer"
echo "   Internal Comms    → Generates Teams notifications & dashboard summary"
echo ""
echo "🛑 Close each terminal window to stop agents"
echo ""
echo "⚠️  NOTE: Agents are running in MOCK mode (no real AgentBus or APIs)"
echo "   To run with real services, set environment variables:"
echo "   - export AGENTBUS_URL=http://localhost:5000"
echo "   - export OPENAI_API_KEY=your-key"
echo "   - export ANTHROPIC_API_KEY=your-key"
