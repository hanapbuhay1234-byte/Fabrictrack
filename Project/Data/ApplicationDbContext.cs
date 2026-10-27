using FabriTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using System.Security.Claims;
using System.Text.Json;

namespace FabriTrack.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ApplicationDbContext> _logger;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ApplicationDbContext> logger)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public DbSet<Fabric> Fabrics { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<LoginActivity> LoginActivities { get; set; }

        public override async Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            var pendingEntries = ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(entry => entry.Entity is not AuditLog)
                .Select(CaptureAuditEntry)
                .Where(entry => entry != null)
                .Cast<PendingAuditEntry>()
                .ToList();

            if (pendingEntries.Count == 0)
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            var actor = GetAuditActor();
            if (actor == null)
            {
                _logger.LogWarning(
                    "Skipped {Count} audit log entries because the current request has no authenticated user identifier.",
                    pendingEntries.Count);
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            IDbContextTransaction? transaction = null;
            if (Database.CurrentTransaction == null)
                transaction = await Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var savedCount = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                AuditLogs.AddRange(pendingEntries.Select(entry => CreateAuditLog(entry, actor!)));
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                _logger.LogInformation(
                    "Recorded {Count} audit log entries for user {UserId}.",
                    pendingEntries.Count,
                    actor.UserId);
                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
                return savedCount;
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (transaction != null)
                    await transaction.DisposeAsync();
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Fabric>()
                .HasOne(f => f.Category)
                .WithMany(c => c.Fabrics)
                .HasForeignKey(f => f.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Fabric>()
                .HasOne(f => f.Supplier)
                .WithMany(s => s.Fabrics)
                .HasForeignKey(f => f.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Sale>()
                .Property(sale => sale.IsVoided)
                .IsConcurrencyToken();

            builder.Entity<SaleItem>()
                .HasOne(si => si.Sale)
                .WithMany(s => s.SaleItems)
                .HasForeignKey(si => si.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<SaleItem>()
                .HasOne(si => si.Fabric)
                .WithMany(f => f.SaleItems)
                .HasForeignKey(si => si.FabricId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Fabric>()
                .HasIndex(f => f.Sku)
                .IsUnique();

            builder.Entity<Fabric>()
                .Property(fabric => fabric.ReorderLevel)
                .HasDefaultValue(5m);

            builder.Entity<Fabric>()
                .Property(fabric => fabric.StockQuantity)
                .IsConcurrencyToken();

            builder.Entity<Fabric>()
                .Property(fabric => fabric.IsActive)
                .HasDefaultValue(true);

            builder.Entity<ApplicationUser>()
                .Property(user => user.IsActive)
                .HasDefaultValue(true);

            builder.Entity<ApplicationUser>()
                .Property(user => user.ProfilePhotoFileName)
                .HasMaxLength(255);

            builder.Entity<AuditLog>()
                .HasIndex(log => log.OccurredAt);

            builder.Entity<AuditLog>()
                .HasIndex(log => new { log.EntityType, log.EntityId });

            builder.Entity<AuditLog>()
                .HasIndex(log => log.ActorUserId);

            builder.Entity<LoginActivity>()
                .HasIndex(activity => activity.OccurredAt);

            builder.Entity<LoginActivity>()
                .HasIndex(activity => new { activity.UserId, activity.OccurredAt });

            builder.Entity<InventoryMovement>()
                .HasOne(movement => movement.Fabric)
                .WithMany(fabric => fabric.InventoryMovements)
                .HasForeignKey(movement => movement.FabricId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<InventoryMovement>()
                .HasOne(movement => movement.Sale)
                .WithMany()
                .HasForeignKey(movement => movement.SaleId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<InventoryMovement>()
                .HasIndex(movement => new { movement.FabricId, movement.RecordedAt });

            builder.Entity<InventoryMovement>()
                .Property(movement => movement.RecordedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            // Seed a couple of categories so the app isn't empty on first run
            builder.Entity<Category>().HasData(
                new Category { CategoryId = 1, Name = "Cotton", Description = "Cotton-based fabrics" },
                new Category { CategoryId = 2, Name = "Silk", Description = "Silk-based fabrics" },
                new Category { CategoryId = 3, Name = "Linen", Description = "Linen-based fabrics" }
            );
        }

        private AuditActor? GetAuditActor()
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
                return null;

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var actorName = principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.Identity.Name;
            if (string.IsNullOrWhiteSpace(actorName))
                actorName = principal.FindFirstValue(ClaimTypes.Email);

            return new AuditActor(
                userId,
                actorName ?? "Signed-in user",
                _httpContextAccessor.HttpContext!.Request.RouteValues["controller"]?.ToString() ?? string.Empty,
                _httpContextAccessor.HttpContext.Request.RouteValues["action"]?.ToString() ?? string.Empty);
        }

        private PendingAuditEntry? CaptureAuditEntry(EntityEntry entry)
        {
            var entityType = entry.Entity switch
            {
                Fabric => "Product",
                Category => "Category",
                Supplier => "Supplier",
                Customer => "Customer",
                Sale => "Sale",
                InventoryMovement => "Stock",
                ApplicationUser => "User",
                _ => null
            };
            if (entityType == null)
                return null;

            var state = entry.State;
            var changedProperties = state == EntityState.Modified
                ? entry.Properties
                    .Where(property => property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                    .Select(property => property.Metadata.Name)
                    .ToList()
                : entry.Properties.Select(property => property.Metadata.Name).ToList();

            var meaningfulProperties = changedProperties.Where(property =>
                entityType switch
                {
                    "Product" => new[] { nameof(Fabric.Name), nameof(Fabric.Sku), nameof(Fabric.Color), nameof(Fabric.Pattern),
                        nameof(Fabric.CategoryId), nameof(Fabric.SupplierId), nameof(Fabric.Unit), nameof(Fabric.PricePerUnit),
                        nameof(Fabric.StockQuantity), nameof(Fabric.ReorderLevel), nameof(Fabric.IsActive), nameof(Fabric.ImageUrl) }.Contains(property),
                    "Category" => new[] { nameof(Category.Name), nameof(Category.Description) }.Contains(property),
                    "Supplier" => new[] { nameof(Supplier.Name), nameof(Supplier.ContactNumber), nameof(Supplier.Email), nameof(Supplier.Address) }.Contains(property),
                    "Customer" => new[] { nameof(Customer.FullName), nameof(Customer.ContactNumber), nameof(Customer.Email), nameof(Customer.Address) }.Contains(property),
                    "Sale" => new[] { nameof(Sale.CustomerId), nameof(Sale.TotalAmount), nameof(Sale.PaymentStatus), nameof(Sale.IsVoided),
                        nameof(Sale.VoidedAt), nameof(Sale.VoidedByUserId), nameof(Sale.VoidReason) }.Contains(property),
                    "Stock" => new[] { nameof(InventoryMovement.FabricId), nameof(InventoryMovement.MovementType),
                        nameof(InventoryMovement.QuantityChange), nameof(InventoryMovement.Notes), nameof(InventoryMovement.SaleId) }.Contains(property),
                    "User" => new[] { nameof(ApplicationUser.FullName), nameof(ApplicationUser.Email), nameof(ApplicationUser.PhoneNumber),
                        nameof(ApplicationUser.IsActive), nameof(ApplicationUser.ProfilePhotoFileName), nameof(ApplicationUser.PasswordHash) }.Contains(property),
                    _ => false
                }).ToList();

            if (meaningfulProperties.Count == 0)
                return null;

            return new PendingAuditEntry(entry, entityType, state, meaningfulProperties);
        }

        private AuditLog CreateAuditLog(PendingAuditEntry pending, AuditActor actor)
        {
            var entry = pending.Entry;
            var fields = pending.Properties
                .Where(property => property != nameof(ApplicationUser.PasswordHash))
                .Select(property => GetFieldLabel(pending.EntityType, property))
                .Distinct()
                .ToList();
            var entityId = GetEntityId(entry, pending.EntityType);
            var entityName = GetEntityName(entry, pending.EntityType, entityId);
            var action = GetAction(pending, actor);
            var details = new Dictionary<string, object?>();
            if (fields.Count > 0)
                details["Changed fields"] = fields;

            if (pending.EntityType == "Stock" &&
                entry.Entity is InventoryMovement movement)
            {
                entityId = movement.FabricId.ToString();
                entityName = ChangeTracker.Entries<Fabric>()
                    .FirstOrDefault(fabric => fabric.Entity.FabricId == movement.FabricId)?.Entity.Name
                    ?? $"Product #{movement.FabricId}";
                details["Quantity change"] = movement.QuantityChange;
                if (!string.IsNullOrWhiteSpace(movement.Notes))
                    details["Reason"] = movement.Notes;
            }
            else if (pending.EntityType == "Sale" &&
                entry.Entity is Sale { IsVoided: true } sale &&
                !string.IsNullOrWhiteSpace(sale.VoidReason))
            {
                details["Reason"] = sale.VoidReason;
            }

            return new AuditLog
            {
                ActorUserId = actor.UserId,
                ActorName = actor.Name,
                Action = action,
                EntityType = pending.EntityType == "Stock" ? "Product" : pending.EntityType,
                EntityId = entityId,
                EntityName = entityName,
                Details = details.Count == 0 ? null : JsonSerializer.Serialize(details),
                OccurredAt = DateTime.UtcNow
            };
        }

        private static string GetAction(PendingAuditEntry pending, AuditActor actor)
        {
            var entry = pending.Entry;
            var state = pending.State;
            if (pending.EntityType == "User")
            {
                if (state == EntityState.Added)
                    return "Staff account created";
                if (pending.Properties.Contains(nameof(ApplicationUser.IsActive)))
                    return (bool)entry.Property(nameof(ApplicationUser.IsActive)).CurrentValue!
                        ? "Staff account reactivated"
                        : "Staff account deactivated";
                if (pending.Properties.Contains(nameof(ApplicationUser.PasswordHash)))
                    return actor.Controller == "UserManagement" && actor.Action == "ResetPassword"
                        ? "Staff password reset"
                        : "Password changed";
                if (actor.Controller == "UserManagement" && actor.Action == "Edit")
                    return "Staff account updated";
                if (actor.Controller == "Profile" && actor.Action is "UploadPhoto" or "RemovePhoto")
                    return "Profile photo updated";
                return "Profile updated";
            }

            if (pending.EntityType == "Sale" &&
                pending.Properties.Contains(nameof(Sale.IsVoided)) &&
                (bool)entry.Property(nameof(Sale.IsVoided)).CurrentValue!)
                return "Sale voided";

            if (pending.EntityType == "Product" && pending.Properties.Contains(nameof(Fabric.IsActive)))
                return (bool)entry.Property(nameof(Fabric.IsActive)).CurrentValue!
                    ? "Product restored"
                    : "Product archived";

            if (pending.EntityType == "Stock" && entry.Entity is InventoryMovement movement)
                return movement.MovementType switch
                {
                    "Restock" => "Stock restocked",
                    "Adjustment" => "Stock adjusted",
                    "Opening stock" => "Opening stock recorded",
                    "Sale" => "Sale stock deducted",
                    "Sale Void" => "Sale stock restored",
                    _ => "Stock movement recorded"
                };

            return pending.State switch
            {
                EntityState.Added => $"{pending.EntityType} created",
                EntityState.Deleted => $"{pending.EntityType} deleted",
                _ => $"{pending.EntityType} updated"
            };
        }

        private static string GetFieldLabel(string entityType, string propertyName) =>
            (entityType, propertyName) switch
        {
            ("Product", nameof(Fabric.Name)) => "Name",
            ("Product", nameof(Fabric.Sku)) => "SKU",
            ("Product", nameof(Fabric.Color)) => "Color",
            ("Product", nameof(Fabric.Pattern)) => "Pattern",
            ("Product", nameof(Fabric.CategoryId)) => "Category",
            ("Product", nameof(Fabric.SupplierId)) => "Supplier",
            ("Product", nameof(Fabric.Unit)) => "Unit",
            ("Product", nameof(Fabric.PricePerUnit)) => "Unit price",
            ("Product", nameof(Fabric.StockQuantity)) => "Stock quantity",
            ("Product", nameof(Fabric.ReorderLevel)) => "Reorder level",
            ("Product", nameof(Fabric.IsActive)) => "Active status",
            ("Product", nameof(Fabric.ImageUrl)) => "Photo",
            ("Category", nameof(Category.Name)) => "Name",
            ("Category", nameof(Category.Description)) => "Description",
            ("Supplier", nameof(Supplier.Name)) => "Name",
            ("Supplier", nameof(Supplier.ContactNumber)) => "Phone number",
            ("Supplier", nameof(Supplier.Email)) => "Email",
            ("Supplier", nameof(Supplier.Address)) => "Address",
            ("Customer", nameof(Customer.FullName)) => "Full name",
            ("Customer", nameof(Customer.ContactNumber)) => "Phone number",
            ("Customer", nameof(Customer.Email)) => "Email",
            ("Customer", nameof(Customer.Address)) => "Address",
            ("Sale", nameof(Sale.CustomerId)) => "Customer",
            ("Sale", nameof(Sale.TotalAmount)) => "Total amount",
            ("Sale", nameof(Sale.PaymentStatus)) => "Payment status",
            ("Sale", nameof(Sale.IsVoided)) => "Void status",
            ("Sale", nameof(Sale.VoidedAt)) => "Voided date",
            ("Sale", nameof(Sale.VoidedByUserId)) => "Voided by",
            ("Sale", nameof(Sale.VoidReason)) => "Void reason",
            ("Stock", nameof(InventoryMovement.MovementType)) => "Movement type",
            ("Stock", nameof(InventoryMovement.QuantityChange)) => "Quantity change",
            ("Stock", nameof(InventoryMovement.Notes)) => "Reason",
            ("Stock", nameof(InventoryMovement.SaleId)) => "Sale",
            ("User", nameof(ApplicationUser.FullName)) => "Full name",
            ("User", nameof(ApplicationUser.Email)) => "Email",
            ("User", nameof(ApplicationUser.PhoneNumber)) => "Phone number",
            ("User", nameof(ApplicationUser.IsActive)) => "Account status",
            ("User", nameof(ApplicationUser.ProfilePhotoFileName)) => "Photo",
            ("User", nameof(ApplicationUser.PasswordHash)) => "Password",
            _ => propertyName
        };

        private static string? GetEntityId(EntityEntry entry, string entityType) => entityType switch
        {
            "Product" => entry.Property(nameof(Fabric.FabricId)).CurrentValue?.ToString(),
            "Category" => entry.Property(nameof(Category.CategoryId)).CurrentValue?.ToString(),
            "Supplier" => entry.Property(nameof(Supplier.SupplierId)).CurrentValue?.ToString(),
            "Customer" => entry.Property(nameof(Customer.CustomerId)).CurrentValue?.ToString(),
            "Sale" => entry.Property(nameof(Sale.SaleId)).CurrentValue?.ToString(),
            "Stock" => entry.Property(nameof(InventoryMovement.FabricId)).CurrentValue?.ToString(),
            "User" => entry.Property(nameof(ApplicationUser.Id)).CurrentValue?.ToString(),
            _ => null
        };

        private static string? GetEntityName(EntityEntry entry, string entityType, string? entityId) => entityType switch
        {
            "Product" => $"{entry.Property(nameof(Fabric.Name)).CurrentValue} ({entry.Property(nameof(Fabric.Sku)).CurrentValue})",
            "Category" => entry.Property(nameof(Category.Name)).CurrentValue?.ToString(),
            "Supplier" => entry.Property(nameof(Supplier.Name)).CurrentValue?.ToString(),
            "Customer" => entry.Property(nameof(Customer.FullName)).CurrentValue?.ToString(),
            "Sale" => $"Sale #{entityId}",
            "User" => entry.Property(nameof(ApplicationUser.FullName)).CurrentValue?.ToString(),
            _ => entityId
        };

        private sealed record AuditActor(string UserId, string Name, string Controller, string Action);
        private sealed record PendingAuditEntry(EntityEntry Entry, string EntityType, EntityState State, List<string> Properties);
    }
}
