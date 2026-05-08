using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vahini.Data;
using Vahini.Models;
using Vahini.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

// Add services to the container.
// 1. Prioritize Railway's native variable, then Fallback to appsettings
string rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
                ?? builder.Configuration.GetConnectionString("DefaultConnection");

string connectionString = rawUrl;

// 2. The Cloud Translator
if (!string.IsNullOrEmpty(rawUrl) && rawUrl.StartsWith("postgres://"))
{
    var databaseUri = new Uri(rawUrl);
    var userInfo = databaseUri.UserInfo.Split(':');
    var port = databaseUri.Port == -1 ? 5432 : databaseUri.Port;
    connectionString = $"Host={databaseUri.Host};Port={port};Database={databaseUri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true;";
}

// 3. Register the Context
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

// app.UseHttpsRedirection(); // COMMENT THIS OUT FOR RAILWAY
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
