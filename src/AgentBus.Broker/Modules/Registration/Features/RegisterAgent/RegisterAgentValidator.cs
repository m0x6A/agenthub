using FluentValidation;
using System.Text.RegularExpressions;

namespace AgentBus.Broker.Modules.Registration.Features.RegisterAgent;

public sealed partial class RegisterAgentValidator : AbstractValidator<RegisterAgentRequest>
{
    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[a-zA-Z0-9\-.]+)?(\+[a-zA-Z0-9\-.]+)?$")]
    private static partial Regex SemVerRegex();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CapabilityRegex();

    public RegisterAgentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Agent ID is required")
            .Length(3, 64).WithMessage("Agent ID must be between 3 and 64 characters")
            .Matches(@"^[a-zA-Z0-9-]+$").WithMessage("Agent ID must contain only alphanumeric characters and hyphens");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Agent name is required")
            .Length(3, 100).WithMessage("Agent name must be between 3 and 100 characters");

        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required")
            .Must(BeValidSemVer).WithMessage("Version must be valid SemVer 2.0 format");

        RuleFor(x => x.Capabilities)
            .NotNull().WithMessage("Capabilities are required")
            .NotEmpty().WithMessage("At least one capability is required")
            .Must(x => x.Length <= 50).WithMessage("Maximum 50 capabilities allowed")
            .Must(x => x.Distinct().Count() == x.Length).WithMessage("Capabilities must not contain duplicates")
            .ForEach(capability =>
            {
                capability.Must(c => CapabilityRegex().IsMatch(c))
                    .WithMessage("Capability must be lowercase with hyphens only");
            });

        RuleFor(x => x.MessageTypes)
            .NotNull().WithMessage("Message types are required");

        RuleFor(x => x.MessageTypes.Accepts)
            .Must(x => x.Length <= 50).WithMessage("Maximum 50 accept patterns allowed");

        RuleFor(x => x.MessageTypes.Emits)
            .Must(x => x.Length <= 50).WithMessage("Maximum 50 emit patterns allowed");

        RuleFor(x => x.Identity)
            .NotNull().WithMessage("Identity is required");

        RuleFor(x => x.Identity.PrincipalId)
            .Must(BeValidGuid).WithMessage("Principal ID must be a valid GUID");

        RuleFor(x => x.Identity.TenantId)
            .Must(BeValidGuid).WithMessage("Tenant ID must be a valid GUID");

        RuleFor(x => x.Identity.ManagedIdentityId)
            .NotEmpty().WithMessage("Managed Identity ID is required");

        When(x => x.HealthCheckUrl != null, () =>
        {
            RuleFor(x => x.HealthCheckUrl)
                .Must(BeValidHttpsUrl!).WithMessage("Health check URL must be a valid HTTPS URL");
        });

        When(x => x.Metadata?.Tags != null, () =>
        {
            RuleFor(x => x.Metadata!.Tags)
                .Must(tags => tags!.Length <= 20).WithMessage("Maximum 20 tags allowed");
        });
    }

    private static bool BeValidSemVer(string version)
    {
        return SemVerRegex().IsMatch(version);
    }

    private static bool BeValidGuid(string guid)
    {
        return Guid.TryParse(guid, out _);
    }

    private static bool BeValidHttpsUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https";
    }
}
