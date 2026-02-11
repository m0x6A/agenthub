using AgentBus.Broker.Modules.Registration.Features.RegisterAgent;
using AgentBus.Broker.SharedKernel.Models;
using FluentValidation.TestHelper;
using Xunit;

namespace AgentBus.Broker.Tests.Unit.Registration;

[Trait("Category", "Registration")]
public sealed class RegisterAgentValidatorTests
{
    private readonly RegisterAgentValidator _validator;

    public RegisterAgentValidatorTests()
    {
        _validator = new RegisterAgentValidator();
    }

    [Fact]
    public void Validate_ValidRequest_PassesValidation()
    {
        // Arrange
        var request = new RegisterAgentRequest(
            Id: "test-agent-123",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "12345678-1234-1234-1234-123456789abc",
                TenantId: "87654321-4321-4321-4321-cba987654321"
            ),
            Metadata: null
        );

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    public void Validate_IdTooShort_FailsValidation(string id)
    {
        // Arrange
        var request = CreateValidRequest() with { Id = id };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Id)
            .WithErrorMessage("*3*64*");
    }

    [Fact]
    public void Validate_IdTooLong_FailsValidation()
    {
        // Arrange
        var longId = new string('a', 65);
        var request = CreateValidRequest() with { Id = longId };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Id)
            .WithErrorMessage("*3*64*");
    }

    [Theory]
    [InlineData("agent with spaces")]
    [InlineData("agent@special")]
    [InlineData("agent_underscore")]
    public void Validate_IdInvalidCharacters_FailsValidation(string id)
    {
        // Arrange
        var request = CreateValidRequest() with { Id = id };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Id)
            .WithErrorMessage("*alphanumeric*hyphen*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    public void Validate_NameTooShort_FailsValidation(string name)
    {
        // Arrange
        var request = CreateValidRequest() with { Name = name };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name)
            .WithErrorMessage("*3*100*");
    }

    [Fact]
    public void Validate_NameTooLong_FailsValidation()
    {
        // Arrange
        var longName = new string('a', 101);
        var request = CreateValidRequest() with { Name = longName };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Name)
            .WithErrorMessage("*3*100*");
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.0.1")]
    [InlineData("2.1.3-beta")]
    [InlineData("1.0.0-alpha.1")]
    [InlineData("1.2.3+build.123")]
    public void Validate_ValidSemVer_PassesValidation(string version)
    {
        // Arrange
        var request = CreateValidRequest() with { Version = version };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Version);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    [InlineData("1")]
    [InlineData("1.0.0.0")]
    [InlineData("invalid")]
    public void Validate_InvalidSemVer_FailsValidation(string version)
    {
        // Arrange
        var request = CreateValidRequest() with { Version = version };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Version)
            .WithErrorMessage("*SemVer*");
    }

    [Fact]
    public void Validate_EmptyCapabilities_FailsValidation()
    {
        // Arrange
        var request = CreateValidRequest() with { Capabilities = Array.Empty<string>() };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Capabilities)
            .WithErrorMessage("*at least one*");
    }

    [Fact]
    public void Validate_TooManyCapabilities_FailsValidation()
    {
        // Arrange
        var capabilities = Enumerable.Range(1, 51).Select(i => $"capability-{i}").ToArray();
        var request = CreateValidRequest() with { Capabilities = capabilities };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Capabilities)
            .WithErrorMessage("*maximum*50*");
    }

    [Theory]
    [InlineData("valid-capability")]
    [InlineData("schedule-drone")]
    [InlineData("capability123")]
    public void Validate_ValidCapabilityFormat_PassesValidation(string capability)
    {
        // Arrange
        var request = CreateValidRequest() with { Capabilities = new[] { capability } };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.Capabilities);
    }

    [Theory]
    [InlineData("Invalid Capability")]
    [InlineData("capability_underscore")]
    [InlineData("UPPERCASE")]
    [InlineData("-starts-with-hyphen")]
    public void Validate_InvalidCapabilityFormat_FailsValidation(string capability)
    {
        // Arrange
        var request = CreateValidRequest() with { Capabilities = new[] { capability } };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Capabilities)
            .WithErrorMessage("*lowercase*hyphen*");
    }

    [Fact]
    public void Validate_DuplicateCapabilities_FailsValidation()
    {
        // Arrange
        var request = CreateValidRequest() with 
        { 
            Capabilities = new[] { "capability-1", "capability-2", "capability-1" } 
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Capabilities)
            .WithErrorMessage("*duplicate*");
    }

    [Fact]
    public void Validate_EmptyAccepts_PassesValidation()
    {
        // Arrange
        var request = CreateValidRequest() with 
        { 
            MessageTypes = new MessageTypes(
                Accepts: Array.Empty<string>(),
                Emits: new[] { "test.response" }
            )
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.MessageTypes);
    }

    [Fact]
    public void Validate_EmptyEmits_PassesValidation()
    {
        // Arrange
        var request = CreateValidRequest() with 
        { 
            MessageTypes = new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: Array.Empty<string>()
            )
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.MessageTypes);
    }

    [Fact]
    public void Validate_InvalidPrincipalId_FailsValidation()
    {
        // Arrange
        var request = CreateValidRequest() with 
        { 
            Identity = CreateValidRequest().Identity with { PrincipalId = "not-a-guid" }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Identity.PrincipalId)
            .WithErrorMessage("*GUID*");
    }

    [Fact]
    public void Validate_InvalidTenantId_FailsValidation()
    {
        // Arrange
        var request = CreateValidRequest() with 
        { 
            Identity = CreateValidRequest().Identity with { TenantId = "not-a-guid" }
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Identity.TenantId)
            .WithErrorMessage("*GUID*");
    }

    [Fact]
    public void Validate_InvalidHealthCheckUrl_FailsValidation()
    {
        // Arrange
        var request = CreateValidRequest() with { HealthCheckUrl = "not-a-url" };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.HealthCheckUrl)
            .WithErrorMessage("*HTTPS*");
    }

    [Theory]
    [InlineData("https://example.com/health")]
    [InlineData("https://agent.example.com:8443/health")]
    public void Validate_ValidHealthCheckUrl_PassesValidation(string url)
    {
        // Arrange
        var request = CreateValidRequest() with { HealthCheckUrl = url };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(r => r.HealthCheckUrl);
    }

    [Fact]
    public void Validate_MetadataWithTooManyTags_FailsValidation()
    {
        // Arrange
        var tags = Enumerable.Range(1, 21).Select(i => $"tag-{i}").ToArray();
        var request = CreateValidRequest() with 
        { 
            Metadata = new AgentMetadata(
                Owner: "[email protected]",
                Environment: "dev",
                Tags: tags
            )
        };

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(r => r.Metadata!.Tags)
            .WithErrorMessage("*maximum*20*");
    }

    private static RegisterAgentRequest CreateValidRequest()
    {
        return new RegisterAgentRequest(
            Id: "test-agent-123",
            Name: "Test Agent",
            Version: "1.0.0",
            Capabilities: new[] { "test-capability" },
            MessageTypes: new MessageTypes(
                Accepts: new[] { "test.*" },
                Emits: new[] { "test.response" }
            ),
            Identity: new AgentIdentity(
                ManagedIdentityId: "/subscriptions/test/resourceGroups/test/providers/Microsoft.ManagedIdentity/userAssignedIdentities/test",
                PrincipalId: "12345678-1234-1234-1234-123456789abc",
                TenantId: "87654321-4321-4321-4321-cba987654321"
            ),
            Metadata: null
        );
    }
}
