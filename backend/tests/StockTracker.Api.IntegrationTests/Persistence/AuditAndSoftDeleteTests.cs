using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Domain.Common;
using StockTracker.Infrastructure.Persistence;
using StockTracker.Infrastructure.Persistence.Interceptors;

namespace StockTracker.Api.IntegrationTests.Persistence;

/// <summary>
/// No real aggregate derives from BaseEntity yet, so these tests use a test-only entity
/// in a separate database created from the model (not from migrations).
/// </summary>
[Collection(ApiCollection.Name)]
public class AuditAndSoftDeleteTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.CreateVersion7();

    private readonly FakeDateTime _clock = new() { UtcNow = Now };
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        _connectionString = new NpgsqlConnectionStringBuilder(factory.ConnectionString)
        {
            Database = $"audit_{Guid.NewGuid():N}",
        }.ConnectionString;

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task SaveChanges_OnInsert_SetsCreatedFields()
    {
        var entity = new TestEntity { Name = "Drill" };

        await using (var context = CreateContext())
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var saved = await readContext.Set<TestEntity>().SingleAsync(e => e.Id == entity.Id);
        saved.CreatedAt.Should().Be(Now);
        saved.CreatedBy.Should().Be(UserId);
        saved.UpdatedAt.Should().BeNull();
        saved.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_OnUpdate_SetsUpdatedFieldsAndKeepsCreatedFields()
    {
        var entity = new TestEntity { Name = "Drill" };
        await using (var context = CreateContext())
        {
            context.Add(entity);
            await context.SaveChangesAsync();
        }

        var later = Now.AddHours(1);
        _clock.UtcNow = later;
        await using (var context = CreateContext())
        {
            var loaded = await context.Set<TestEntity>().SingleAsync(e => e.Id == entity.Id);
            loaded.Name = "Hammer drill";
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var saved = await readContext.Set<TestEntity>().SingleAsync(e => e.Id == entity.Id);
        saved.CreatedAt.Should().Be(Now);
        saved.UpdatedAt.Should().Be(later);
        saved.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public async Task Query_ExcludesInactiveEntities_UnlessFiltersIgnored()
    {
        var active = new TestEntity { Name = "Active" };
        var inactive = new TestEntity { Name = "Inactive", IsActive = false };
        await using (var context = CreateContext())
        {
            context.AddRange(active, inactive);
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var ids = new[] { active.Id, inactive.Id };

        (await readContext.Set<TestEntity>().Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync())
            .Should().Equal(active.Id);
        (await readContext.Set<TestEntity>().IgnoreQueryFilters().Where(e => ids.Contains(e.Id)).CountAsync())
            .Should().Be(2);
    }

    private TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableEntityInterceptor(new FakeCurrentUser(UserId), _clock))
            .Options;

        return new TestDbContext(options);
    }

    private sealed class TestEntity : BaseEntity, ISoftDeletable
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    private sealed class TestDbContext(DbContextOptions options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<TestEntity>(e =>
            {
                e.ToTable("test_entities");
                e.Property(x => x.Name).HasMaxLength(100);
            });

            base.OnModelCreating(builder);
        }
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;

        public bool IsAuthenticated => true;

        public bool HasPermission(string permission) => false;
    }

    private sealed class FakeDateTime : IDateTime
    {
        public DateTime UtcNow { get; set; }
    }
}
