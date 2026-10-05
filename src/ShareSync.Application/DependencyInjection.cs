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
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<ICsvTransactionImportService, CsvTransactionImportService>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISimulatorService, SimulatorService>();
        services.AddScoped<IPortfolioGoalService, PortfolioGoalService>();
        services.AddSingleton<IBenchmarkDataProvider, BenchmarkDataProvider>();
        services.AddScoped<IBenchmarkService, BenchmarkService>();
        services.AddScoped<IActivityTimelineService, ActivityTimelineService>();
        services.AddScoped<ISearchService, SearchService>();

        return services;
    }
}
