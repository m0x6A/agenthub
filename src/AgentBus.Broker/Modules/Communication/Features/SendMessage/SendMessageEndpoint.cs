using AgentBus.Broker.SharedKernel.Services;
using Microsoft.AspNetCore.Mvc;
using A2A;

namespace AgentBus.Broker.Modules.Communication.Features.SendMessage;

public sealed record SendMessageRequest(
    string TargetAgentId,
    AgentMessage Message,
    string? PreferredTransport = null);

public sealed record SendMessageResponse(
    AgentMessage Response,
    string TransportUsed);

public static class SendMessageEndpoint
{
    public static IEndpointRouteBuilder MapSendMessageEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/communication/send", SendMessage)
            .WithName("SendMessage")
            .WithTags("Communication")
            .AllowAnonymous(); // Allow anonymous for development

        return app;
    }

    private static async Task<IResult> SendMessage(
        [FromBody] SendMessageRequest request,
        AgentCommunicationService communicationService,
        CancellationToken cancellationToken)
    {
        try
        {
            var transportPreference = request.PreferredTransport?.ToLower() switch
            {
                "servicebus" => SharedKernel.Models.TransportPreference.ServiceBus,
                "http" => SharedKernel.Models.TransportPreference.Http,
                _ => SharedKernel.Models.TransportPreference.Auto
            };

            var messageSendParams = new MessageSendParams
            {
                Message = request.Message
            };

            var response = await communicationService.SendMessageAsync(
                request.TargetAgentId,
                messageSendParams,
                transportPreference,
                cancellationToken);

            return Results.Ok(new SendMessageResponse(
                Response: response,
                TransportUsed: transportPreference.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (TimeoutException ex)
        {
            return Results.Problem(
                statusCode: 504,
                title: "Gateway Timeout",
                detail: ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
