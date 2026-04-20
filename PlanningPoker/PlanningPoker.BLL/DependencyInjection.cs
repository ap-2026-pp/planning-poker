using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.BLL.Services;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBllServices(this IServiceCollection services)
    {
        services.AddScoped<ISampleService, SampleService>();
        return services;
    }
}