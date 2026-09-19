using SocietyManagement.Application.Features.Budgets;
using SocietyManagement.Application.Features.Inventory;
using SocietyManagement.Application.Features.Purchasing;
using SocietyManagement.Domain.Entities;
using SocietyManagement.Domain.Enums;
using SocietyManagement.Shared.Exceptions;
using SocietyManagement.Tests.Fakes;
using Xunit;

namespace SocietyManagement.Tests.Operations;

public class ProcurementHandlerTests
{
    private static (SocietyManagement.Infrastructure.Persistence.ApplicationDbContext Db, Society Society, User User) Seed()
    {
        var db = TestDbContextFactory.Create();
        var role = new Role { Name = "Admin", IsSystemRole = true, CreatedBy = "t" };
        db.Roles.Add(role);
        var society = new Society { Name = "S1", Address = "a", City = "c", State = "s", Pincode = "1", CreatedBy = "t" };
        db.Societies.Add(society);
        db.SaveChanges();
        var user = new User
        {
            FirstName = "A", LastName = "B", Email = "a@b.com", MobileNumber = "9999999999", PasswordHash = "x",
            RoleId = role.Id, SocietyId = society.Id, CreatedBy = "t"
        };
        db.Users.Add(user);
        db.SaveChanges();
        return (db, society, user);
    }

    [Fact]
    public async Task Stock_IssuingMoreThanOnHand_IsRejectedAndStockIsSumOfMovements()
    {
        var (db, society, user) = Seed();
        using var _ = db;
        var handlers = new InventoryHandlers(db, new FakeAuditService(), new FakeCurrentUserService { UserId = user.Id, SocietyId = society.Id });

        var itemId = await handlers.Handle(new CreateInventoryItemCommand(society.Id, "Bulbs", null, "Units", 5, 10), CancellationToken.None);
        await handlers.Handle(new RecordStockTransactionCommand(itemId, StockTransactionType.Out, 4, DateTime.UtcNow, "Ravi", "Gate", null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictAppException>(() => handlers.Handle(
            new RecordStockTransactionCommand(itemId, StockTransactionType.Out, 7, DateTime.UtcNow, null, null, null), CancellationToken.None));

        var page = await handlers.Handle(new GetInventoryItemsQuery(society.Id, null, false, false), CancellationToken.None);
        var item = Assert.Single(page.Items);
        Assert.Equal(6m, item.CurrentStock);
        Assert.False(item.IsLow);

        await handlers.Handle(new RecordStockTransactionCommand(itemId, StockTransactionType.Out, 2, DateTime.UtcNow, null, null, null), CancellationToken.None);
        var low = await handlers.Handle(new GetInventoryItemsQuery(society.Id, null, true, false), CancellationToken.None);
        Assert.Equal(4m, Assert.Single(low.Items).CurrentStock);
    }

    [Fact]
    public async Task Purchase_FullWorkflow_ReceivingAddsStockAndEndsReceived()
    {
        var (db, society, user) = Seed();
        using var _ = db;
        var current = new FakeCurrentUserService { UserId = user.Id, SocietyId = society.Id };
        var inventory = new InventoryHandlers(db, new FakeAuditService(), current);
        var purchases = new PurchaseHandlers(db, new FakeAuditService(), current);

        var itemId = await inventory.Handle(new CreateInventoryItemCommand(society.Id, "Phenyl", null, "Litre", 0, 0), CancellationToken.None);
        var vendor = new Vendor { SocietyId = society.Id, Name = "Cleanco", Phone = "9999999999", CreatedBy = "t" };
        db.Vendors.Add(vendor);
        db.SaveChanges();

        var id = await purchases.Handle(new CreatePurchaseRequestCommand(
            society.Id, "Cleaning supplies", null, PurchasePriority.Normal, null, null,
            new List<PurchaseItemInput> { new("Phenyl", 10, "Litre", 50, itemId) }, Submit: true), CancellationToken.None);

        // Can't order before approval.
        await Assert.ThrowsAsync<ConflictAppException>(() => purchases.Handle(new MarkPurchaseOrderedCommand(id, vendor.Id), CancellationToken.None));

        await purchases.Handle(new ApprovePurchaseRequestCommand(id), CancellationToken.None);
        await purchases.Handle(new MarkPurchaseOrderedCommand(id, vendor.Id), CancellationToken.None);

        var line = (await purchases.Handle(new GetPurchaseRequestByIdQuery(id), CancellationToken.None)).Items.Single();
        await purchases.Handle(new ReceivePurchaseCommand(id, new List<ReceiveLine> { new(line.Id, 6) }), CancellationToken.None);
        Assert.Equal(PurchaseStatus.PartiallyReceived, (await purchases.Handle(new GetPurchaseRequestByIdQuery(id), CancellationToken.None)).Status);

        // Over-receiving is refused.
        await Assert.ThrowsAsync<ConflictAppException>(() => purchases.Handle(
            new ReceivePurchaseCommand(id, new List<ReceiveLine> { new(line.Id, 5) }), CancellationToken.None));

        await purchases.Handle(new ReceivePurchaseCommand(id, new List<ReceiveLine> { new(line.Id, 4) }), CancellationToken.None);
        Assert.Equal(PurchaseStatus.Received, (await purchases.Handle(new GetPurchaseRequestByIdQuery(id), CancellationToken.None)).Status);

        var stock = await inventory.Handle(new GetInventoryItemsQuery(society.Id, null, false, false), CancellationToken.None);
        Assert.Equal(10m, stock.Items.Single().CurrentStock);
    }

    [Fact]
    public async Task Purchase_OrderRequiresAVendor_AndForeignSocietyIsHidden()
    {
        var (db, society, user) = Seed();
        using var _ = db;
        var current = new FakeCurrentUserService { UserId = user.Id, SocietyId = society.Id };
        var purchases = new PurchaseHandlers(db, new FakeAuditService(), current);
        var id = await purchases.Handle(new CreatePurchaseRequestCommand(
            society.Id, "Chairs", null, PurchasePriority.Low, null, null,
            new List<PurchaseItemInput> { new("Chair", 2, "Units", 100, null) }, Submit: true), CancellationToken.None);
        await purchases.Handle(new ApprovePurchaseRequestCommand(id), CancellationToken.None);

        await Assert.ThrowsAsync<BadRequestAppException>(() => purchases.Handle(new MarkPurchaseOrderedCommand(id, null), CancellationToken.None));

        var stranger = new PurchaseHandlers(db, new FakeAuditService(), new FakeCurrentUserService { UserId = user.Id, SocietyId = society.Id + 99 });
        await Assert.ThrowsAsync<NotFoundException>(() => stranger.Handle(new GetPurchaseRequestByIdQuery(id), CancellationToken.None));
    }

    [Fact]
    public async Task Budget_SetIsUpsert_AndActualsComeFromExpensesInTheFinancialYear()
    {
        var (db, society, _) = Seed();
        using var _ = db;
        db.Expenses.AddRange(
            new Expense { SocietyId = society.Id, Category = ExpenseCategory.Repairs, Title = "in", Amount = 300, ExpenseDate = new DateTime(2026, 6, 1), CreatedBy = "t" },
            new Expense { SocietyId = society.Id, Category = ExpenseCategory.Repairs, Title = "before", Amount = 999, ExpenseDate = new DateTime(2026, 3, 31), CreatedBy = "t" });
        db.SaveChanges();

        var handlers = new BudgetHandlers(db, new FakeAuditService());
        await handlers.Handle(new SetBudgetCommand(society.Id, 2026, ExpenseCategory.Repairs, 1000), CancellationToken.None);
        await handlers.Handle(new SetBudgetCommand(society.Id, 2026, ExpenseCategory.Repairs, 1500), CancellationToken.None);

        var overview = await handlers.Handle(new GetBudgetOverviewQuery(society.Id, 2026), CancellationToken.None);
        var repairs = overview.Lines.Single(l => l.Category == ExpenseCategory.Repairs);
        Assert.Equal(1500m, repairs.Budgeted);
        Assert.Equal(300m, repairs.Actual);
        Assert.Single(db.Budgets);
    }
}
