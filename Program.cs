using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

// Add services to the container.
// 1. Deep-Search for the Connection String
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"[VAHINI DIAGNOSTIC] Connection String Found: {(!string.IsNullOrEmpty(connectionString))}");

// 2. Updated Cloud URI Parser (Handles both postgres:// and postgresql://)
if (!string.IsNullOrEmpty(connectionString) && 
   (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
{
    // Ensure the URI parser understands the scheme by standardizing it to 'postgres' for the Uri object
    var uriString = connectionString.StartsWith("postgresql://") 
        ? connectionString.Replace("postgresql://", "postgres://") 
        : connectionString;

    var databaseUri = new Uri(uriString);
    var userInfo = databaseUri.UserInfo.Split(':');
    var port = databaseUri.Port == -1 ? 5432 : databaseUri.Port;
    
    connectionString = $"Host={databaseUri.Host};Port={port};Database={databaseUri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
}

if (string.IsNullOrEmpty(connectionString)) {
    throw new Exception("FATAL: VAHINI could not find a valid database connection string in the environment.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// VAHINI services
builder.Services.AddSingleton<WorkflowService>();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()                       // Enable role-based authorization (Admin / Member)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<VahiniClaimsPrincipalFactory>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── Seed roles and default admin on every startup (idempotent) ────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        // --- ADD THIS LINE ---
        await context.Database.MigrateAsync(); 
        // ---------------------

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await DbInitializer.SeedAsync(roleManager, userManager, context);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[VAHINI ERROR] An error occurred during migration or seeding: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection(); // COMMENT THIS OUT FOR RAILWAY
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
