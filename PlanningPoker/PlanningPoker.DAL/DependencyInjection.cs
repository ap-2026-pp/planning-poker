using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Models;
using Microsoft.AspNetCore.Identity;
using PlanningPoker.DAL.Repositories;
using PlanningPoker.Domain.Interfaces.Repositories;

namespace PlanningPoker.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDalServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(config.GetConnectionString("Default")));
        
        services.AddScoped<IAuthRepository, AuthRepository>();

        return services;
    }
}