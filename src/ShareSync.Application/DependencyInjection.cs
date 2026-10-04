using Microsoft.Extensions.DependencyInjection;
using ShareSync.Application.Interfaces;
using ShareSync.Application.Services;

namespace ShareSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IDividendService, DividendService>();
        services.AddScoped<IPortfolioSnapshotService, PortfolioSnapshotService>();

        return services;
    }
}
