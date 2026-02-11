using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;

namespace AgentBus.Examples.Shared;

/// <summary>
/// Base class for autonomous agents that use LLMs to make decisions.
/// Unlike traditional microservices, these agents:
/// - Analyze events and decide if/how to respond
/// - Choose their own tools/actions via function calling
/// - Maintain conversational memory across interactions
/// - Work toward goals autonomously
/// </summary>
public abstract class AutonomousAgent
{
    protected readonly Kernel Kernel;
    protected readonly IChatCompletionService ChatService;
    protected readonly IAgentBusClient AgentBus;
    protected readonly string AgentId;
    protected readonly string SystemPrompt;
    protected readonly ChatHistory ChatHistory;
    private readonly ILogger _logger;

    protected AutonomousAgent(
        string agentId,
        string systemPrompt,
        IAgentBusClient agentBus,
        ILogger logger)
    {
        AgentId = agentId;
        SystemPrompt = systemPrompt;
        AgentBus = agentBus;
        _logger = logger;

        // Build Semantic Kernel with plugins
        var kernelBuilder = Kernel.CreateBuilder();
        
        // Try Azure OpenAI first (provisioned by Aspire), then fallback to OpenAI API key, then mock
        var azureOpenAiEndpoint = Environment.GetEnvironmentVariable("ConnectionStrings__openai__Endpoint")
            ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        
        if (!string.IsNullOrEmpty(azureOpenAiEndpoint))
        {
            // Use Azure OpenAI (provisioned by Aspire with Managed Identity)
            kernelBuilder.AddAzureOpenAIChatCompletion(
                deploymentName: GetModelId(),
                endpoint: azureOpenAiEndpoint,
                credentials: new Azure.Identity.DefaultAzureCredential());
            
            _logger.Information("Using Azure OpenAI at {Endpoint}", azureOpenAiEndpoint);
        }
        else if (!string.IsNullOrEmpty(openAiKey))
        {
            // Use OpenAI API key
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: GetModelId(),
                apiKey: openAiKey);
            
            _logger.Information("Using OpenAI API with key");
        }
        else
        {
            // Mock service for demonstration
            kernelBuilder.Services.AddSingleton<IChatCompletionService>(
                new MockChatCompletionService(_logger));
            
            _logger.Warning("Using mock LLM - set OPENAI_API_KEY or run with Aspire for real LLM");
        }

        Kernel = kernelBuilder.Build();
        
        // Add plugins to kernel - agent will choose which tools to use
        ConfigurePlugins(Kernel);

        ChatService = Kernel.GetRequiredService<IChatCompletionService>();
        
        // Initialize chat history with system prompt
        ChatHistory = new ChatHistory(systemPrompt);
        
        _logger.Information("🤖 Autonomous agent {AgentId} initialized with {PluginCount} plugins",
            agentId, Kernel.Plugins.Count);
    }

    protected abstract string GetModelId();
    protected abstract void ConfigurePlugins(Kernel kernel);

    /// <summary>
    /// Autonomous event processing - agent decides what to do
    /// </summary>
    public async Task ProcessEventAsync(EventEnvelope eventEnvelope)
    {
        _logger.Information("📩 Agent {AgentId} analyzing event: {EventType}", 
            AgentId, eventEnvelope.EventType);

        // Present event to agent for analysis and decision-making
        var eventContext = $@"
EVENT RECEIVED:
Type: {eventEnvelope.EventType}
Source: {eventEnvelope.Source}
Timestamp: {eventEnvelope.Timestamp}
Payload: {JsonSerializer.Serialize(eventEnvelope.Data)}

Analyze this event and decide:
1. Is this event relevant to your role and objectives?
2. What actions (if any) should you take?
3. Which tools do you need to use?
4. Should you publish any events to inform other agents?

Think step-by-step and use your available tools as needed.
";

        ChatHistory.AddUserMessage(eventContext);

        try
        {
            // Let the agent reason and choose tools autonomously
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.7,
                MaxTokens = 2000
            };

            _logger.Information("🧠 Agent {AgentId} reasoning about event...", AgentId);

            var response = await ChatService.GetChatMessageContentAsync(
                ChatHistory,
                executionSettings,
                Kernel);

            ChatHistory.AddAssistantMessage(response.Content ?? "");

            _logger.Information("💭 Agent {AgentId} decision: {Response}", 
                AgentId, response.Content);

            // Log any function calls the agent made
            LogFunctionCalls(response);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "❌ Agent {AgentId} failed to process event", AgentId);
        }
    }

    private void LogFunctionCalls(ChatMessageContent response)
    {
        var metadata = response.Metadata;
        if (metadata != null && metadata.ContainsKey("ToolCalls"))
        {
            _logger.Information("🔧 Agent {AgentId} used tools: {Tools}", 
                AgentId, metadata["ToolCalls"]);
        }
    }

    /// <summary>
    /// Allows agent to process proactive goals or scheduled tasks
    /// </summary>
    public async Task ProcessProactiveGoalAsync(string goal)
    {
        _logger.Information("🎯 Agent {AgentId} processing goal: {Goal}", AgentId, goal);

        ChatHistory.AddUserMessage($"GOAL: {goal}\n\nWork toward this goal using your available tools.");

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.7,
            MaxTokens = 2000
        };

        var response = await ChatService.GetChatMessageContentAsync(
            ChatHistory,
            executionSettings,
            Kernel);

        ChatHistory.AddAssistantMessage(response.Content ?? "");

        _logger.Information("✅ Agent {AgentId} completed goal work: {Response}", 
            AgentId, response.Content);
    }
}

/// <summary>
/// Mock chat completion service for demonstration when no API key is available
/// </summary>
internal class MockChatCompletionService : IChatCompletionService
{
    private readonly ILogger _logger;
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public MockChatCompletionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // In demo mode, simulate agent reasoning
        await Task.Delay(500, cancellationToken); // Simulate thinking

        var lastMessage = chatHistory.LastOrDefault()?.Content ?? "";
        
        // Simple rule-based response for demo
        var response = "Analyzing event... This appears relevant to my domain. " +
                      "I should investigate further using my available tools and " +
                      "communicate findings to other agents if significant.";

        _logger.Warning("⚠️ Using mock LLM - set OPENAI_API_KEY for real autonomous behavior");

        return new List<ChatMessageContent>
        {
            new(AuthorRole.Assistant, response)
        };
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not supported in mock service");
    }
}
