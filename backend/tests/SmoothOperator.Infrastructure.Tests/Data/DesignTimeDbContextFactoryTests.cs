using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmoothOperator.Infrastructure.Data;

namespace SmoothOperator.Infrastructure.Tests.Data
{
    public class DesignTimeDbContextFactoryTests
    {
        [Fact]
        public void CreateDbContext_ShouldUseExpectedNpgsqlConnectionString()
        {
            var sut = new DesignTimeDbContextFactory();

            using var context = sut.CreateDbContext([]);
            var connectionString = context.Database.GetDbConnection().ConnectionString;

            context.Should().NotBeNull();
            context.Database.ProviderName.Should().Contain("Npgsql");
            connectionString.Should().Contain("Host=localhost");
            connectionString.Should().Contain("Database=smoothoperator");
            connectionString.Should().Contain("Username=postgres");
            connectionString.Should().Contain("Password=postgres");
        }
    }
}
