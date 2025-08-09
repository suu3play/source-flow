using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SourceFlow.Data.Context;

namespace SourceFlow.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataServices(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SourceFlowDbContext>(options =>
            options.UseSqlite(connectionString));
            
        return services;
    }
}