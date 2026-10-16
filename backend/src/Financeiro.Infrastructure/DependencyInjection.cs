using Financeiro.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Financeiro.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinanceInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IFinanceRepository, SqliteFinanceRepository>();
        return services;
    }
}
