using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Interfaces.Services;
namespace PlanningPoker.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBllServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IGuestSessionService, GuestSessionService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IGameAccessService, GameAccessService>();
        
        services.AddScoped<IGuestAccountLinkService, GuestAccountLinkService>();

        services.AddHttpClient<IPlaneService,PlaneService>(client =>
        {
           client.BaseAddress = new Uri("https://api.plane.so");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
