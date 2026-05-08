using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Railway DATABASE_URL Parser
if (connectionString?.StartsWith("postgres://") == true)
{
    var databaseUri = new Uri(connectionString);
    var userInfo = databaseUri.UserInfo.Split(':');
    connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString ?? throw new InvalidOperationException("Connection string not found.")));

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
await using (var scope = app.Services.CreateAsyncScope())
{
    await DbInitializer.SeedAsync(
        scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
        scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
