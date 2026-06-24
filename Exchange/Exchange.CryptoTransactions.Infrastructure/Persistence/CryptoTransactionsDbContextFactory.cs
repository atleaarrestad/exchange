using Exchange.CryptoTransactions.Infrastructure.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Exchange.CryptoTransactions.Infrastructure.Persistence;

public sealed class CryptoTransactionsDbContextFactory : IDesignTimeDbContextFactory<CryptoTransactionsDbContext>
{
    public CryptoTransactionsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CryptoTransactionsDbContext>();
        optionsBuilder.UseNpgsql(InfrastructureConfigurationKeys.DefaultIdempotencyConnectionString);

        return new CryptoTransactionsDbContext(optionsBuilder.Options);
    }
}
