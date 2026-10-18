using Microsoft.EntityFrameworkCore;
using ResortManagementSystem.Infrastructure.Data;
using ResortManagementSystem.Domain.Entities;
using ResortManagementSystem.Infrastructure.Services;

// ============================================================
// APPLICATION SETUP AND DATABASE CONFIGURATION
// ============================================================
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MasterResortDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterResort"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
builder.Services.AddDbContext<TenantResortDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TenantResort"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));

// Tenant factory and its dependency must be registered so endpoints can receive them from DI
builder.Services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
builder.Services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

// Promo emails: settings, sender and the background dispatcher
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IPromoMailer, SmtpPromoMailer>();
builder.Services.AddSingleton<EmailDispatcher>();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// SuperAdmin can never be created from the app
string[] allowedStaffRoles = { "Staff", "Manager", "Admin" };

// ============================================================
// CONNECTION HEALTH CHECK
// ============================================================
app.MapGet("/health/status", async (
    MasterResortDbContext masterDb,
    ITenantDbContextFactory tenantFactory,
    int? companyId) =>
{
    bool masterOk;
    try
    {
        masterOk = await masterDb.Database.CanConnectAsync();
    }
    catch
    {
        masterOk = false;
    }

    bool? tenantOk = null;
    if (companyId is not null)
    {
        try
        {
            await using var tenantDb = await tenantFactory.CreateAsync(companyId.Value);
            tenantOk = await tenantDb.Database.CanConnectAsync();
        }
        catch
        {
            tenantOk = false;
        }
    }

    return Results.Ok(new { master = masterOk, tenant = tenantOk });
});

// ============================================================
// LOGIN (MASTER DATABASE)
// The account decides the role and company, not the client.
// ============================================================
app.MapPost("/auth/login", async (LoginRequest req, MasterResortDbContext db) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

    // Same response for "no such user" and "wrong password" so emails can't be probed
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password ?? "", user.PasswordHash))
        return Results.Unauthorized();

    if (user.CompanyId is not null)
    {
        var company = await db.Companies.FindAsync(user.CompanyId);
        if (company is null || !company.IsSubscriptionActive)
            return Results.Json(new { message = "This company's subscription is inactive." }, statusCode: 403);
    }

    return Results.Ok(new LoginResponse(user.Role, user.DisplayName, user.CompanyId ?? 0));
});
app.MapPost("/auth/change-password", async (ChangePasswordRequest req, MasterResortDbContext db) =>
{
    var email = (req.Email ?? "").Trim().ToLowerInvariant();
    var user = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

    // Same answer for unknown email and wrong current password
    if (user is null || !BCrypt.Net.BCrypt.Verify(req.CurrentPassword ?? "", user.PasswordHash))
        return Results.Unauthorized();

    if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 8)
        return Results.BadRequest("New password must be at least 8 characters.");

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
    await db.SaveChangesAsync();
    return Results.Ok();
});
// ============================================================
// COMPANY MANAGEMENT (MASTER DATABASE)
// Provisioning a company also creates its first Admin login
// ============================================================
app.MapPost("/companies", async (ProvisionCompanyRequest req, MasterResortDbContext db) =>
{
    var code = (req.CompanyCode ?? "").Trim();
    var name = (req.CompanyName ?? "").Trim();
    var adminEmail = (req.AdminEmail ?? "").Trim().ToLowerInvariant();

    if (code.Length == 0 || name.Length == 0 || adminEmail.Length == 0)
        return Results.BadRequest("Company code, company name, and admin email are required.");
    if (string.IsNullOrEmpty(req.AdminPassword) || req.AdminPassword.Length < 8)
        return Results.BadRequest("Admin password must be at least 8 characters.");
    if (req.SubscriptionTier is not ("Micro" or "Small" or "Medium"))
        return Results.BadRequest("Invalid subscription tier.");

    if (await db.Companies.AnyAsync(c => c.CompanyCode == code))
        return Results.Conflict("That company code is already used.");
    if (await db.UserAccounts.AnyAsync(u => u.Email == adminEmail))
        return Results.Conflict("That admin email is already used by another account.");

    var company = new Company
    {
        CompanyCode = code,
        CompanyName = name,
        SubscriptionTier = req.SubscriptionTier,
        IsSubscriptionActive = true
    };
    db.Companies.Add(company);
    await db.SaveChangesAsync();

    try
    {
        var firstPrice = SubscriptionHelper.PlanPrices[req.SubscriptionTier];
        var firstStart = DateTime.UtcNow.Date;

        db.UserAccounts.Add(new UserAccount
        {
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.AdminPassword),
            DisplayName = $"{name} Admin",
            Role = "Admin",
            CompanyId = company.CompanyId,
            IsActive = true
        });
        db.Subscriptions.Add(new Subscription
        {
            CompanyId = company.CompanyId,
            Tier = company.SubscriptionTier,
            PricePerMonth = firstPrice,
            Months = 1,
            TotalPrice = firstPrice,
            StartDate = firstStart,
            EndDate = firstStart.AddMonths(1),
            Status = "Active"
        });
        await db.SaveChangesAsync();
    }
    catch
    {
        // Undo the company so we never leave a resort with no way to log in
        db.ChangeTracker.Clear();
        db.Companies.Remove(company);
        await db.SaveChangesAsync();
        return Results.Problem("Could not create the admin login. The company was not created.");
    }

    return Results.Created($"/companies/{company.CompanyId}", company);
});

app.MapGet("/companies", async (MasterResortDbContext db) =>
{
    return await db.Companies.ToListAsync();
});

// NEW: one company by id. The desktop app reads its subscription tier and name from here
// (without this endpoint the app silently fell back to the Micro tier).
app.MapGet("/companies/{id:int}", async (int id, MasterResortDbContext db) =>
{
    var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == id);
    return company is null ? Results.NotFound() : Results.Ok(company);
});

// ============================================================
// COMPANY DATABASE REGISTRATION (MASTER DATABASE)
// ============================================================
app.MapPost("/company-databases", async (
    CompanyDatabase companyDatabase,
    MasterResortDbContext db) =>
{
    db.CompanyDatabases.Add(companyDatabase);
    await db.SaveChangesAsync();
    return Results.Created(
        $"/company-databases/{companyDatabase.CompanyDatabaseId}",
        companyDatabase);
});

// ============================================================
// TENANT DATABASE CONNECTION TEST
// ============================================================
app.MapGet("/test-tenant/{companyId:int}", async (
    int companyId,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var productCount = await tenantDb.Products.CountAsync();
    return Results.Ok(new { companyId, productCount });
});

// ============================================================
// SUBSCRIPTIONS, TERMS AND CONDITIONS (MASTER DATABASE)
// ============================================================

// One row per company for the Super Admin company list
app.MapGet("/admin/companies/overview", async (MasterResortDbContext db) =>
{
    var companies = await db.Companies.OrderBy(c => c.CompanyId).ToListAsync();

    // Companies created before subscriptions existed get a starter record here
    foreach (var c in companies)
        await SubscriptionHelper.GetOrCreateCurrentAsync(db, c);

    var current = await db.Subscriptions.AsNoTracking()
        .Where(s => s.Status == "Active")
        .ToListAsync();

    var rows = companies.Select(c =>
    {
        var s = current.Where(x => x.CompanyId == c.CompanyId)
                       .OrderByDescending(x => x.CreatedAt)
                       .FirstOrDefault();
        return new CompanyOverviewRow(
            c.CompanyId, c.CompanyCode, c.CompanyName, c.SubscriptionTier, c.IsSubscriptionActive,
            s?.StartDate, s?.EndDate, s?.TotalPrice ?? 0, s?.Months ?? 0,
            s is null ? "None" : s.AcceptedAt is null ? "Pending" : "Accepted",
            s?.AcceptedBy, s?.AcceptedAt);
    }).ToList();

    return Results.Ok(rows);
});

app.MapGet("/companies/{id:int}/subscriptions", async (int id, MasterResortDbContext db) =>
{
    var company = await db.Companies.FindAsync(id);
    if (company is null) return Results.NotFound();

    await SubscriptionHelper.GetOrCreateCurrentAsync(db, company);
    var list = await db.Subscriptions.AsNoTracking()
        .Where(s => s.CompanyId == id)
        .OrderByDescending(s => s.CreatedAt)
        .ToListAsync();
    return Results.Ok(list);
});

// New subscription: renews or changes the plan. The previous one is marked Superseded.
app.MapPost("/companies/{id:int}/subscriptions", async (
    int id, NewSubscriptionRequest req, MasterResortDbContext db) =>
{
    var company = await db.Companies.FindAsync(id);
    if (company is null) return Results.NotFound();

    if (!SubscriptionHelper.PlanPrices.TryGetValue(req.Tier ?? "", out var price))
        return Results.BadRequest("Invalid subscription tier.");
    if (req.Months is < 1 or > 36)
        return Results.BadRequest("Duration must be between 1 and 36 months.");

    var start = (req.StartDate ?? DateTime.UtcNow).Date;

    var active = await db.Subscriptions
        .Where(s => s.CompanyId == id && s.Status == "Active")
        .ToListAsync();
    foreach (var old in active) old.Status = "Superseded";

    var sub = new Subscription
    {
        CompanyId = id,
        Tier = req.Tier!,
        PricePerMonth = price,
        Months = req.Months,
        TotalPrice = price * req.Months,
        StartDate = start,
        EndDate = start.AddMonths(req.Months),
        Status = "Active"
    };
    db.Subscriptions.Add(sub);

    company.SubscriptionTier = req.Tier!;
    company.IsSubscriptionActive = true;
    await db.SaveChangesAsync();

    return Results.Created($"/companies/{id}/subscriptions/{sub.SubscriptionId}", sub);
});

// Suspend / reactivate (a suspended company cannot sign in)
app.MapPut("/companies/{id:int}/active", async (
    int id, SetActiveRequest req, MasterResortDbContext db) =>
{
    var company = await db.Companies.FindAsync(id);
    if (company is null) return Results.NotFound();
    company.IsSubscriptionActive = req.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(company);
});

app.MapGet("/terms/current", async (MasterResortDbContext db) =>
    Results.Ok(await SubscriptionHelper.EnsureTermsAsync(db)));

app.MapGet("/terms", async (MasterResortDbContext db) =>
{
    await SubscriptionHelper.EnsureTermsAsync(db);
    var all = await db.TermsVersions.AsNoTracking()
        .OrderByDescending(t => t.Version)
        .ToListAsync();
    return Results.Ok(all);
});

// Publishing creates a new version; old versions are kept for the audit trail
app.MapPost("/terms", async (NewTermsRequest req, MasterResortDbContext db) =>
{
    var title = (req.Title ?? "").Trim();
    var body = (req.Body ?? "").Trim();
    if (title.Length == 0 || body.Length < 20)
        return Results.BadRequest("A title and the full terms text (at least 20 characters) are required.");

    await SubscriptionHelper.EnsureTermsAsync(db);
    var nextVersion = (await db.TermsVersions.MaxAsync(t => t.Version)) + 1;

    foreach (var t in await db.TermsVersions.Where(t => t.IsCurrent).ToListAsync())
        t.IsCurrent = false;

    var terms = new TermsAndConditions { Version = nextVersion, Title = title, Body = body, IsCurrent = true };
    db.TermsVersions.Add(terms);
    await db.SaveChangesAsync();
    return Results.Created($"/terms/{terms.TermsId}", terms);
});

// Does this company's Admin still need to accept the terms for the current subscription?
app.MapGet("/companies/{id:int}/agreement-status", async (int id, MasterResortDbContext db) =>
{
    var company = await db.Companies.FindAsync(id);
    if (company is null) return Results.NotFound();

    var sub = await SubscriptionHelper.GetOrCreateCurrentAsync(db, company);
    var terms = await SubscriptionHelper.EnsureTermsAsync(db);
    return Results.Ok(new AgreementStatusResponse(sub.AcceptedAt is null, sub, terms));
});

app.MapPost("/companies/{id:int}/agreements", async (
    int id, AcceptTermsRequest req, MasterResortDbContext db) =>
{
    var sub = await db.Subscriptions
        .FirstOrDefaultAsync(s => s.SubscriptionId == req.SubscriptionId && s.CompanyId == id);
    if (sub is null) return Results.NotFound("Subscription not found for this company.");
    if (sub.Status != "Active") return Results.Conflict("This subscription is no longer current.");
    if (sub.AcceptedAt is not null) return Results.Ok(sub);          // already accepted

    var terms = await db.TermsVersions.FindAsync(req.TermsId);
    if (terms is null) return Results.BadRequest("Unknown terms version.");

    var by = (req.AcceptedBy ?? "").Trim();
    if (by.Length == 0) return Results.BadRequest("The name of the person accepting is required.");

    sub.TermsId = terms.TermsId;
    sub.TermsVersion = terms.Version;
    sub.AcceptedBy = by;
    sub.AcceptedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(sub);
});

// ============================================================
// BRANCH MANAGEMENT (Medium-tier tenants)
// ============================================================
app.MapGet("/tenant/{companyId:int}/branches", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Branches.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(b => !b.IsArchived);
    var branches = await query.OrderBy(x => x.BranchId).ToListAsync();
    return Results.Ok(branches);
});

app.MapPost("/tenant/{companyId:int}/branches", async (
    int companyId, Branch branch, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Branches.Add(branch);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/branches/{branch.BranchId}", branch);
});

app.MapPut("/tenant/{companyId:int}/branches/{id:int}", async (
    int companyId, int id, Branch updated, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var branch = await tenantDb.Branches.FindAsync(id);
    if (branch is null) return Results.NotFound();
    branch.BranchName = updated.BranchName;
    branch.Location = updated.Location;
    branch.IsActive = updated.IsActive;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(branch);
});

app.MapPut("/tenant/{companyId:int}/branches/{id:int}/archive", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var branch = await tenantDb.Branches.FindAsync(id);
    if (branch is null) return Results.NotFound();

    // A branch with people still assigned cannot be archived: unassign them first
    var assigned = await tenantDb.Staff.CountAsync(s => s.BranchId == id && !s.IsArchived);
    if (assigned > 0)
        return Results.Conflict($"This branch still has {assigned} team member(s) assigned. Open Manage Team and unassign them first.");

    branch.IsArchived = true;
    branch.ManagerStaffId = null;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(branch);
});

// NEW: the Managers and Staff assigned to one branch
app.MapGet("/tenant/{companyId:int}/branches/{id:int}/staff", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    if (!await tenantDb.Branches.AnyAsync(b => b.BranchId == id)) return Results.NotFound();

    var team = await tenantDb.Staff.AsNoTracking()
        .Where(s => s.BranchId == id && !s.IsArchived)
        .OrderBy(s => s.Role).ThenBy(s => s.FullName)
        .ToListAsync();
    return Results.Ok(team);
});

// NEW: assign a staff member to a branch (BranchId = null unassigns them)
app.MapPut("/tenant/{companyId:int}/staff/{id:int}/branch", async (
    int companyId, int id, AssignBranchRequest req, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var staff = await tenantDb.Staff.FindAsync(id);
    if (staff is null || staff.IsArchived) return Results.NotFound("Staff member not found.");

    if (staff.Role is not ("Manager" or "Staff"))
        return Results.BadRequest("Only Manager and Staff accounts can be assigned to a branch. Admins oversee every branch.");

    if (req.BranchId is int newBranchId)
    {
        var target = await tenantDb.Branches.FindAsync(newBranchId);
        if (target is null || target.IsArchived) return Results.BadRequest("That branch does not exist or is archived.");
    }

    // Leaving a branch also drops the branch-manager title there
    if (staff.BranchId is int oldBranchId && oldBranchId != req.BranchId)
    {
        var old = await tenantDb.Branches.FindAsync(oldBranchId);
        if (old is not null && old.ManagerStaffId == id) old.ManagerStaffId = null;
    }

    staff.BranchId = req.BranchId;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(staff);
});

// NEW: choose (or clear, StaffId = null) the manager of a branch
app.MapPut("/tenant/{companyId:int}/branches/{id:int}/manager", async (
    int companyId, int id, SetBranchManagerRequest req, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var branch = await tenantDb.Branches.FindAsync(id);
    if (branch is null || branch.IsArchived) return Results.NotFound("Branch not found.");

    if (req.StaffId is null)
    {
        branch.ManagerStaffId = null;
        await tenantDb.SaveChangesAsync();
        return Results.Ok(branch);
    }

    var staff = await tenantDb.Staff.FindAsync(req.StaffId.Value);
    if (staff is null || staff.IsArchived) return Results.BadRequest("Staff member not found.");
    if (staff.Role != "Manager") return Results.BadRequest("Only someone with the Manager role can be the branch manager.");
    if (!staff.IsActive) return Results.BadRequest("An inactive account cannot be the branch manager.");
    if (staff.BranchId != id) return Results.BadRequest("Assign this person to the branch first.");

    branch.ManagerStaffId = staff.StaffId;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(branch);
});

// ============================================================
// SUPER ADMIN AGGREGATE BI (across all tenants)
// ============================================================
app.MapGet("/admin/reports/aggregate", async (
    MasterResortDbContext masterDb, ITenantDbContextFactory tenantFactory) =>
{
    var companies = await masterDb.Companies.ToListAsync();
    var companyDatabases = await masterDb.CompanyDatabases.Where(cd => cd.IsActive).ToListAsync();

    int totalCustomers = 0;
    int totalReservations = 0;
    decimal totalRevenue = 0;

    foreach (var cd in companyDatabases)
    {
        try
        {
            await using var tenantDb = await tenantFactory.CreateAsync(cd.CompanyId);
            totalCustomers += await tenantDb.Customers.CountAsync(c => !c.IsArchived);
            totalReservations += await tenantDb.Reservations.CountAsync();
            totalRevenue += await tenantDb.Reservations
                .Where(r => r.Status == "Confirmed" || r.Status == "CheckedIn" || r.Status == "CheckedOut")
                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
        }
        catch
        {
            // Skip a tenant whose database is unreachable so one bad connection doesn't break the whole report
        }
    }

    var tierBreakdown = companies
        .GroupBy(c => c.SubscriptionTier)
        .Select(g => new { Tier = g.Key, Count = g.Count() })
        .ToList();

    return Results.Ok(new
    {
        totalCompanies = companies.Count,
        totalCustomers,
        totalReservations,
        totalRevenue,
        tierBreakdown
    });
});

// ============================================================
// PRODUCT MANAGEMENT
// ============================================================
app.MapGet("/tenant/{companyId:int}/products", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Products.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(p => !p.IsArchived);
    var products = await query.OrderBy(x => x.ProductId).ToListAsync();
    return Results.Ok(products);
});
app.MapPut("/tenant/{companyId:int}/products/{id:int}", async (
    int companyId,
    int id,
    Product updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var product = await tenantDb.Products.FindAsync(id);
    if (product is null) return Results.NotFound();

    product.ProductCode = updated.ProductCode;
    product.ProductName = updated.ProductName;
    product.UnitPrice = updated.UnitPrice;
    product.IsActive = updated.IsActive;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(product);
});
app.MapPut("/tenant/{companyId:int}/products/{id:int}/archive", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var product = await tenantDb.Products.FindAsync(id);
    if (product is null) return Results.NotFound();
    product.IsArchived = true;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(product);
});

app.MapPut("/tenant/{companyId:int}/products/{id:int}/unarchive", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var product = await tenantDb.Products.FindAsync(id);
    if (product is null) return Results.NotFound();
    product.IsArchived = false;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(product);
});

// ============================================================
// CUSTOMER MANAGEMENT
// ============================================================
app.MapGet("/tenant/{companyId:int}/customers", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Customers.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(c => !c.IsArchived);
    var customers = await query.OrderBy(x => x.CustomerId).ToListAsync();
    return Results.Ok(customers);
});

// NEW: edit a customer (copies only the editable fields so CreatedAt / IsArchived are untouched)
app.MapPut("/tenant/{companyId:int}/customers/{id:int}", async (
    int companyId,
    int id,
    Customer updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var customer = await tenantDb.Customers.FindAsync(id);
    if (customer is null) return Results.NotFound();

    customer.FullName = updated.FullName;
    customer.Email = updated.Email;
    customer.PhoneNumber = updated.PhoneNumber;
    customer.Address = updated.Address;
    customer.CustomerCategory = updated.CustomerCategory;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(customer);
});

app.MapPut("/tenant/{companyId:int}/customers/{id:int}/archive", async (
    int companyId,
    int id,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var customer = await tenantDb.Customers.FindAsync(id);
    if (customer is null) return Results.NotFound();

    customer.IsArchived = true;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(customer);
});

app.MapPut("/tenant/{companyId:int}/customers/{id:int}/unarchive", async (
    int companyId,
    int id,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var customer = await tenantDb.Customers.FindAsync(id);
    if (customer is null) return Results.NotFound();

    customer.IsArchived = false;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(customer);
});

// ============================================================
// RESERVATION MANAGEMENT
// ============================================================
app.MapGet("/tenant/{companyId:int}/reservations", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Reservations.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(r => !r.IsArchived);
    var reservations = await query.OrderBy(x => x.ReservationId).ToListAsync();
    return Results.Ok(reservations);
});

app.MapPut("/tenant/{companyId:int}/reservations/{id:int}", async (
    int companyId,
    int id,
    Reservation updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var reservation = await tenantDb.Reservations.FindAsync(id);
    if (reservation is null) return Results.NotFound();

    if (updated.CheckOutDate <= updated.CheckInDate)
        return Results.BadRequest("CheckOutDate must be after CheckInDate.");

    var hasConflict = await tenantDb.Reservations.AnyAsync(r =>
        r.ReservationId != id &&
        r.ProductId == updated.ProductId &&
        !r.IsArchived &&
        r.Status != "Cancelled" && r.Status != "Refunded" &&
        r.CheckInDate < updated.CheckOutDate &&
        r.CheckOutDate > updated.CheckInDate);

    if (hasConflict)
        return Results.Conflict($"This room/service is already booked for an overlapping date range.");

    reservation.CustomerId = updated.CustomerId;
    reservation.ProductId = updated.ProductId;
    reservation.CheckInDate = updated.CheckInDate;
    reservation.CheckOutDate = updated.CheckOutDate;
    reservation.Status = updated.Status;
    reservation.TotalAmount = updated.TotalAmount;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(reservation);
});

app.MapPut("/tenant/{companyId:int}/reservations/{id:int}/archive", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var reservation = await tenantDb.Reservations.FindAsync(id);
    if (reservation is null) return Results.NotFound();
    reservation.IsArchived = true;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(reservation);
});

// ============================================================
// STAFF MANAGEMENT
// Staff rows live in the tenant database; their login lives in
// the master database (UserAccounts). Both are kept in sync.
// ============================================================
app.MapGet("/tenant/{companyId:int}/staff", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Staff.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(s => !s.IsArchived);
    var staff = await query.OrderBy(x => x.StaffId).ToListAsync();
    return Results.Ok(staff);
});

app.MapPost("/tenant/{companyId:int}/staff", async (
    int companyId, StaffRequest req,
    ITenantDbContextFactory tenantFactory, MasterResortDbContext masterDb) =>
{
    var email = (req.Email ?? "").Trim();
    var emailKey = email.ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(req.FullName) || email.Length == 0)
        return Results.BadRequest("Full name and email are required.");
    if (!allowedStaffRoles.Contains(req.Role))
        return Results.BadRequest("Invalid role.");
    if (string.IsNullOrEmpty(req.Password) || req.Password.Length < 8)
        return Results.BadRequest("Password must be at least 8 characters.");

    if (await masterDb.Companies.FindAsync(companyId) is null)
        return Results.NotFound("Company not found.");
    if (await masterDb.UserAccounts.AnyAsync(u => u.Email == emailKey))
        return Results.Conflict("That email is already used by another account.");

    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var staff = new Staff
    {
        FullName = req.FullName.Trim(),
        Email = email,
        PhoneNumber = req.PhoneNumber ?? "",
        Role = req.Role,
        IsActive = req.IsActive
    };
    tenantDb.Staff.Add(staff);
    await tenantDb.SaveChangesAsync();

    try
    {
        masterDb.UserAccounts.Add(new UserAccount
        {
            Email = emailKey,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            DisplayName = staff.FullName,
            Role = req.Role,
            CompanyId = companyId,
            IsActive = req.IsActive
        });
        await masterDb.SaveChangesAsync();
    }
    catch
    {
        // Two separate databases, so no shared transaction: undo the staff row manually
        tenantDb.Staff.Remove(staff);
        await tenantDb.SaveChangesAsync();
        return Results.Problem("Could not create the login account. Nothing was saved.");
    }

    return Results.Created($"/tenant/{companyId}/staff/{staff.StaffId}", staff);
});

app.MapPut("/tenant/{companyId:int}/staff/{id:int}", async (
    int companyId, int id, StaffRequest req,
    ITenantDbContextFactory tenantFactory, MasterResortDbContext masterDb) =>
{
    var email = (req.Email ?? "").Trim();
    var newKey = email.ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(req.FullName) || email.Length == 0)
        return Results.BadRequest("Full name and email are required.");
    if (!allowedStaffRoles.Contains(req.Role))
        return Results.BadRequest("Invalid role.");
    var newPassword = string.IsNullOrEmpty(req.Password) ? null : req.Password;
    if (newPassword is not null && newPassword.Length < 8)
        return Results.BadRequest("Password must be at least 8 characters.");

    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var staff = await tenantDb.Staff.FindAsync(id);
    if (staff is null) return Results.NotFound();

    var oldKey = staff.Email.Trim().ToLowerInvariant();
    var account = await masterDb.UserAccounts
        .FirstOrDefaultAsync(u => u.Email == oldKey && u.CompanyId == companyId);

    if (newKey != oldKey && await masterDb.UserAccounts.AnyAsync(u => u.Email == newKey))
        return Results.Conflict("That email is already used by another account.");

    staff.FullName = req.FullName.Trim();
    staff.Email = email;
    staff.PhoneNumber = req.PhoneNumber ?? "";
    staff.Role = req.Role;
    staff.IsActive = req.IsActive;

    // Only Managers can be a branch manager, and Admins belong to no single branch
    if (req.Role != "Manager")
    {
        foreach (var managed in await tenantDb.Branches.Where(b => b.ManagerStaffId == id).ToListAsync())
            managed.ManagerStaffId = null;
    }
    if (req.Role == "Admin") staff.BranchId = null;
    await tenantDb.SaveChangesAsync();

    var loginActive = req.IsActive && !staff.IsArchived;
    if (account is not null)
    {
        account.Email = newKey;
        account.DisplayName = staff.FullName;
        account.Role = req.Role;
        account.IsActive = loginActive;
        if (newPassword is not null)
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await masterDb.SaveChangesAsync();
    }
    else if (newPassword is not null)
    {
        // Older staff rows that never had a login: typing a password creates one
        masterDb.UserAccounts.Add(new UserAccount
        {
            Email = newKey,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword),
            DisplayName = staff.FullName,
            Role = req.Role,
            CompanyId = companyId,
            IsActive = loginActive
        });
        await masterDb.SaveChangesAsync();
    }

    return Results.Ok(staff);
});

app.MapPut("/tenant/{companyId:int}/staff/{id:int}/archive", async (
    int companyId, int id,
    ITenantDbContextFactory tenantFactory, MasterResortDbContext masterDb) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var staff = await tenantDb.Staff.FindAsync(id);
    if (staff is null) return Results.NotFound();

    staff.IsArchived = true;

    // An archived person leaves their branch (and the branch-manager seat, if they held it)
    staff.BranchId = null;
    foreach (var managed in await tenantDb.Branches.Where(b => b.ManagerStaffId == id).ToListAsync())
        managed.ManagerStaffId = null;
    await tenantDb.SaveChangesAsync();

    // Archived staff must not be able to sign in
    var key = staff.Email.Trim().ToLowerInvariant();
    var account = await masterDb.UserAccounts
        .FirstOrDefaultAsync(u => u.Email == key && u.CompanyId == companyId);
    if (account is not null)
    {
        account.IsActive = false;
        await masterDb.SaveChangesAsync();
    }

    return Results.Ok(staff);
});

// ============================================================
// PROMOTION MANAGEMENT
// ============================================================
app.MapGet("/tenant/{companyId:int}/promotions", async (
    int companyId,
    ITenantDbContextFactory tenantFactory,
    bool includeArchived = false) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var query = tenantDb.Promotions.AsNoTracking().AsQueryable();
    if (!includeArchived) query = query.Where(p => !p.IsArchived);
    var promotions = await query.OrderBy(x => x.PromotionId).ToListAsync();
    return Results.Ok(promotions);
});

app.MapPut("/tenant/{companyId:int}/promotions/{id:int}", async (
    int companyId,
    int id,
    Promotion updated,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var promo = await tenantDb.Promotions.FindAsync(id);
    if (promo is null) return Results.NotFound();

    promo.Code = updated.Code;
    promo.Description = updated.Description;
    promo.DiscountPercent = updated.DiscountPercent;
    promo.EligibleCategory = updated.EligibleCategory;   // FIXED: was not being saved on edit
    promo.IsActive = updated.IsActive;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(promo);
});

app.MapPut("/tenant/{companyId:int}/promotions/{id:int}/archive", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var promo = await tenantDb.Promotions.FindAsync(id);
    if (promo is null) return Results.NotFound();
    promo.IsArchived = true;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(promo);
});

// ============================================================
// CUSTOMER FEEDBACK AND COMPLAINTS
// ============================================================
app.MapPost("/tenant/{companyId:int}/feedback", async (
    int companyId, Feedback feedback, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customerExists = await tenantDb.Customers.AnyAsync(c => c.CustomerId == feedback.CustomerId);
    if (!customerExists) return Results.BadRequest($"CustomerId {feedback.CustomerId} does not exist.");

    tenantDb.Feedbacks.Add(feedback);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/feedback/{feedback.FeedbackId}", feedback);
});

app.MapGet("/tenant/{companyId:int}/feedback", async (
    int companyId, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var feedback = await tenantDb.Feedbacks.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync();
    return Results.Ok(feedback);
});

app.MapPut("/tenant/{companyId:int}/feedback/{id:int}/resolve", async (
    int companyId, int id, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var feedback = await tenantDb.Feedbacks.FindAsync(id);
    if (feedback is null) return Results.NotFound();

    feedback.Status = "Resolved";
    feedback.ResolvedAt = DateTime.UtcNow;
    await tenantDb.SaveChangesAsync();
    return Results.Ok(feedback);
});

// ============================================================
// PROMO EMAILS TO CUSTOMERS
// Rows are saved in the outbox first, then sent in the background.
// If the mail server is unreachable they are marked Failed and can be retried.
// ============================================================
app.MapPost("/tenant/{companyId:int}/promotions/{id:int}/notify", async (
    int companyId, int id, NotifyRequest req,
    ITenantDbContextFactory tenantFactory, MasterResortDbContext masterDb, EmailDispatcher dispatcher) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var promo = await tenantDb.Promotions.FirstOrDefaultAsync(p => p.PromotionId == id && !p.IsArchived);
    if (promo is null) return Results.NotFound("Promotion not found.");
    if (!promo.IsActive) return Results.BadRequest("Switch the promotion to Active before emailing customers.");

    var subject = (req.Subject ?? "").Trim();
    var message = (req.Message ?? "").Trim();
    if (subject.Length > 150) return Results.BadRequest("Subject is too long (maximum 150 characters).");
    if (message.Length > 1000) return Results.BadRequest("Message is too long (maximum 1000 characters).");

    var company = await masterDb.Companies.FindAsync(companyId);
    var companyName = company?.CompanyName ?? "Our Resort";
    if (subject.Length == 0) subject = $"{companyName}: {promo.DiscountPercent:0.##}% off with code {promo.Code}";

    // Who qualifies: active guests with an email whose category matches (blank category = Regular)
    var all = await tenantDb.Customers.AsNoTracking().Where(c => !c.IsArchived && c.Email != "").ToListAsync();
    var eligible = all.Where(c =>
        promo.EligibleCategory is "All" or "" ||
        (string.IsNullOrWhiteSpace(c.CustomerCategory) ? "Regular" : c.CustomerCategory) == promo.EligibleCategory).ToList();

    var existing = await tenantDb.EmailOutbox.Where(m => m.PromotionId == id).ToListAsync();
    var received = existing.Where(m => m.Status is "Sent" or "Pending").Select(m => m.ToEmail.ToLower()).ToHashSet();
    var toSend = eligible.Where(c => !received.Contains(c.Email.ToLower())).ToList();

    if (req.PreviewOnly)
        return Results.Ok(new NotifyResult(eligible.Count, eligible.Count - toSend.Count, toSend.Count));

    foreach (var c in toSend)
    {
        var body = PromoEmailTemplate.Build(companyName, c.FullName, promo, message);

        // A previously failed email to the same guest is re-queued instead of duplicated
        var failed = existing.FirstOrDefault(m => m.Status == "Failed" && m.ToEmail.Equals(c.Email, StringComparison.OrdinalIgnoreCase));
        if (failed is not null)
        {
            failed.Subject = subject;
            failed.Body = body;
            failed.Status = "Pending";
            failed.LastError = null;
        }
        else
        {
            tenantDb.EmailOutbox.Add(new EmailMessage
            {
                PromotionId = id,
                CustomerId = c.CustomerId,
                ToEmail = c.Email,
                ToName = c.FullName,
                Subject = subject,
                Body = body
            });
        }
    }
    await tenantDb.SaveChangesAsync();

    if (toSend.Count > 0) dispatcher.Kick(companyId, companyName);
    return Results.Ok(new NotifyResult(eligible.Count, eligible.Count - toSend.Count, toSend.Count));
});

// Try the failed ones again (for example after the internet is back)
app.MapPost("/tenant/{companyId:int}/email/retry", async (
    int companyId, ITenantDbContextFactory tenantFactory, MasterResortDbContext masterDb, EmailDispatcher dispatcher) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var failed = await tenantDb.EmailOutbox.Where(m => m.Status == "Failed").ToListAsync();
    foreach (var m in failed) m.Status = "Pending";
    await tenantDb.SaveChangesAsync();

    var company = await masterDb.Companies.FindAsync(companyId);
    if (failed.Count > 0) dispatcher.Kick(companyId, company?.CompanyName ?? "Our Resort");
    return Results.Ok(new { requeued = failed.Count });
});

app.MapGet("/tenant/{companyId:int}/email-log", async (
    int companyId, ITenantDbContextFactory tenantFactory, int? promotionId) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    var q = tenantDb.EmailOutbox.AsNoTracking().AsQueryable();
    if (promotionId is not null) q = q.Where(m => m.PromotionId == promotionId);
    var rows = await q.OrderByDescending(m => m.EmailMessageId).Take(1000)
        .Select(m => new EmailLogRow(m.EmailMessageId, m.PromotionId, m.ToEmail, m.ToName, m.Subject,
            m.Status, m.Attempts, m.LastError, m.CreatedAt, m.SentAt))
        .ToListAsync();
    return Results.Ok(rows);
});

// ============================================================
// SUMMARY REPORTS / DASHBOARD STATISTICS
// ============================================================
app.MapGet("/tenant/{companyId:int}/reports/summary", async (
    int companyId, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var totalCustomers = await tenantDb.Customers.CountAsync(c => !c.IsArchived);
    var totalReservations = await tenantDb.Reservations.CountAsync();
    var confirmedRevenue = await tenantDb.Reservations
        .Where(r => r.Status == "Confirmed" || r.Status == "CheckedIn" || r.Status == "CheckedOut")
        .SumAsync(r => (decimal?)r.TotalAmount) ?? 0;
    var pendingCount = await tenantDb.Reservations.CountAsync(r => r.Status == "Pending");
    var openComplaints = await tenantDb.Feedbacks.CountAsync(f => f.IsComplaint && f.Status == "Open");
    var activeStaff = await tenantDb.Staff.CountAsync(s => s.IsActive);

    return Results.Ok(new
    {
        totalCustomers,
        totalReservations,
        confirmedRevenue,
        pendingCount,
        openComplaints,
        activeStaff
    });
});

// ============================================================
// CREATE RECORDS — TENANT DATABASE
// (staff creation is handled above, together with its login)
// ============================================================
app.MapPost("/tenant/{companyId:int}/promotions", async (
    int companyId, Promotion promotion, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Promotions.Add(promotion);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/promotions/{promotion.PromotionId}", promotion);
});

app.MapPost("/tenant/{companyId:int}/customers", async (
    int companyId, Customer customer, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Customers.Add(customer);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/customers/{customer.CustomerId}", customer);
});

app.MapPost("/tenant/{companyId:int}/reservations", async (
    int companyId,
    Reservation reservation,
    ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);

    var customerExists = await tenantDb.Customers.AnyAsync(c => c.CustomerId == reservation.CustomerId);
    if (!customerExists) return Results.BadRequest($"CustomerId {reservation.CustomerId} does not exist.");

    var productExists = await tenantDb.Products.AnyAsync(p => p.ProductId == reservation.ProductId);
    if (!productExists) return Results.BadRequest($"ProductId {reservation.ProductId} does not exist.");

    if (reservation.CheckOutDate <= reservation.CheckInDate)
        return Results.BadRequest("CheckOutDate must be after CheckInDate.");

    var hasConflict = await tenantDb.Reservations.AnyAsync(r =>
        r.ProductId == reservation.ProductId &&
        !r.IsArchived &&
        r.Status != "Cancelled" && r.Status != "Refunded" &&
        r.CheckInDate < reservation.CheckOutDate &&
        r.CheckOutDate > reservation.CheckInDate);

    if (hasConflict)
        return Results.Conflict($"This room/service is already booked for an overlapping date range.");

    tenantDb.Reservations.Add(reservation);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/reservations/{reservation.ReservationId}", reservation);
});

app.MapPost("/tenant/{companyId:int}/products", async (
    int companyId, Product product, ITenantDbContextFactory tenantFactory) =>
{
    await using var tenantDb = await tenantFactory.CreateAsync(companyId);
    tenantDb.Products.Add(product);
    await tenantDb.SaveChangesAsync();
    return Results.Created($"/tenant/{companyId}/products/{product.ProductId}", product);
});

app.Run();

// ============================================================
// REQUEST / RESPONSE RECORDS (must stay after all statements)
// ============================================================
public record LoginRequest(string? Email, string? Password);
public record LoginResponse(string Role, string DisplayName, int CompanyId);
public record StaffRequest(string FullName, string Email, string? PhoneNumber, string Role, bool IsActive, string? Password);
public record ProvisionCompanyRequest(string CompanyCode, string CompanyName, string SubscriptionTier, string AdminEmail, string AdminPassword);
public record ChangePasswordRequest(string? Email, string? CurrentPassword, string? NewPassword);
public record AssignBranchRequest(int? BranchId);
public record SetBranchManagerRequest(int? StaffId);