using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlanningPoker.API.Hubs;
using PlanningPoker.API.Middlewares;
using PlanningPoker.API.Services;
using PlanningPoker.BLL;
using PlanningPoker.BLL.Services;
using PlanningPoker.DAL;
using PlanningPoker.DAL.Data;
using PlanningPoker.Domain.Constants;
using PlanningPoker.Domain.Interfaces.Services;
using PlanningPoker.Domain.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IGameRoomNotifier, GameRoomNotifier>();
builder.Services.AddScoped<IUserNotifier, UserNotifier>();
builder.Services.AddScoped<InviteLinkService>();

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDalServices(builder.Configuration);
builder.Services.AddBllServices();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddIdentity<User, Role>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments(GameRoomHub.HubRoute))
                {
                    var accessToken = context.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrWhiteSpace(accessToken))
                    {
                        context.Token = accessToken;
                    }
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var tokenType = context.Principal?.FindFirstValue(GuestSessionDefaults.TokenTypeClaimType);
                if (!string.Equals(tokenType, GuestSessionDefaults.GuestAccessTokenType, StringComparison.Ordinal))
                {
                    return;
                }

                var accessToken = context.Request.Path.StartsWithSegments(GameRoomHub.HubRoute)
                    ? context.Request.Query["access_token"].ToString()
                    : null;

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    const string bearerPrefix = "Bearer ";
                    var authorizationHeader = context.Request.Headers.Authorization.ToString();
                    if (authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        accessToken = authorizationHeader[bearerPrefix.Length..].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    context.Fail("Guest access token is missing.");
                    return;
                }

                var guestSessionService = context.HttpContext.RequestServices.GetRequiredService<IGuestSessionService>();
                var isActive = await guestSessionService.IsGuestAccessTokenActiveAsync(accessToken);
                if (!isActive)
                {
                    context.Fail("Guest access token is invalid or expired.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PlanningPoker API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

await SeedDatabaseAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PlanningPoker API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.UseCors("ClientPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<GameRoomHub>(GameRoomHub.HubRoute);
app.MapHub<UserHub>(UserHub.HubRoute);
app.MapControllers();

app.Run();

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    await DbInitializer.SeedDataAsync(context, userManager);
}
