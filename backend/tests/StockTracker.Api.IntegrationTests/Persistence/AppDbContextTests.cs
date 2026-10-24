using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockTracker.Api.IntegrationTests.Infrastructure;
using StockTracker.Application.Common.Exceptions;
using StockTracker.Application.Common.Interfaces;
using StockTracker.Infrastructure.Identity;
using StockTracker.Infrastructure.Persistence;

namespace StockTracker.Api.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public class AppDbContextTests(ApiFactory factory)
{
    [Fact]
    public async Task Users_InactiveUser_IsHiddenByQueryFilter()
    {
        var user = NewUser();
        user.IsActive = false;
        await AddUserAsync(user);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        (await db.Users.AnyAsync(u => u.Id == user.Id)).Should().BeFalse();
        (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task UnitOfWork_OnConcurrentUpdate_ThrowsConcurrencyException()
    {
        var user = NewUser();
        await AddUserAsync(user);

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = await LoadAsync(firstScope, user.Id);
        var second = await LoadAsync(secondScope, user.Id);

        first.FullName = "First writer";
        first.ConcurrencyStamp = Guid.NewGuid().ToString();
        await firstScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        second.FullName = "Second writer";
        var act = () => secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    private static AppUser NewUser()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new AppUser
        {
            FullName = "Test User",
            UserName = $"user_{suffix}",
            NormalizedUserName = $"USER_{suffix}".ToUpperInvariant(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
    }

    private async Task AddUserAsync(AppUser user)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private static Task<AppUser> LoadAsync(IServiceScope scope, Guid id) =>
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == id);
}
