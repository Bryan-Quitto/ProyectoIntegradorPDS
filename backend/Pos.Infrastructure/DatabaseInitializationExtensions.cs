using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure;

public static class DatabaseInitializationExtensions
{
    public static void InitializePosDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        dbContext.Database.EnsureCreated();
    }
}
