using Microsoft.EntityFrameworkCore;
using StockTracker.Domain.Assignments;
using StockTracker.Domain.Auditing;
using StockTracker.Domain.Catalog;
using StockTracker.Domain.Counts;
using StockTracker.Domain.Inventory;
using StockTracker.Domain.Locations;
using StockTracker.Domain.Notifications;

namespace StockTracker.Application.Common.Interfaces;

/// <summary>
/// Read-side access for query handlers: always AsNoTracking + projection to DTOs.
/// Commands use aggregate repositories and IUnitOfWork instead.
/// </summary>
public interface IAppDbContext
{
    DbSet<Category> Categories { get; }

    DbSet<Product> Products { get; }

    DbSet<ProductBarcode> ProductBarcodes { get; }

    DbSet<Location> Locations { get; }

    DbSet<Item> Items { get; }

    DbSet<StockBalance> StockBalances { get; }

    DbSet<StockMovement> StockMovements { get; }

    DbSet<Assignment> Assignments { get; }

    DbSet<CountSession> CountSessions { get; }

    DbSet<ProductPhoto> ProductPhotos { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Notification> Notifications { get; }
}
