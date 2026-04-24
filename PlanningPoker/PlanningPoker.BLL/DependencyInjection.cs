using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Interfaces.Services;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace PlanningPoker.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBllServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        return services;
    }
}