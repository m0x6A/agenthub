using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;

namespace AgentBus.Broker.SharedKernel.Security;

public static class JwtValidation
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(configuration.GetSection("AzureAd"));

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AgentPolicy", policy =>
                policy.RequireRole("AgentBus.Agent"));
        });

        return services;
    }
}
