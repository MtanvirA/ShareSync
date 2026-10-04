using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShareSync.Application.Common.Interfaces;
using ShareSync.Infrastructure.Data;
using ShareSync.Infrastructure.Security;

namespace ShareSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OracleConnection")
            ?? throw new InvalidOperationException("Oracle connection string 'OracleConnection' was not found.");

        services.AddDbContext<ShareSyncDbContext>(options =>
        {
            options.UseOracle(connectionString, b =>
            {
                b.MigrationsAssembly(typeof(ShareSyncDbContext).Assembly.FullName);
                b.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion23);
            });
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ShareSyncDbContext>());
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
