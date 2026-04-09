using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using PCG.Data;
using PCG.Models;
using PCG.Services;

namespace PCG
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var contentRoot = builder.Environment.ContentRootPath;
            var appDataRoot = Path.Combine(contentRoot, "App_Data");
            var uploadsRoot = Path.Combine(appDataRoot, "uploads");
            var tempUploadsRoot = Path.Combine(appDataRoot, "upload_temp");
            Directory.CreateDirectory(appDataRoot);
            Directory.CreateDirectory(uploadsRoot);
            Directory.CreateDirectory(tempUploadsRoot);

            var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            var shouldUseSqlite = string.IsNullOrWhiteSpace(configuredConnectionString)
                || configuredConnectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase)
                || string.Equals(builder.Configuration["DatabaseProvider"], "sqlite", StringComparison.OrdinalIgnoreCase)
                || string.Equals(builder.Environment.EnvironmentName, "Render", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"));

            if (shouldUseSqlite)
            {
                var sqliteConnectionString = $"Data Source={Path.Combine(appDataRoot, "pcg.db")}";
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(sqliteConnectionString));
            }
            else
            {
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(configuredConnectionString));
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();
            }

            builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 8;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            var requireAuthPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            builder.Services.AddAuthorization(options => { options.FallbackPolicy = requireAuthPolicy; });

            builder.Services.AddControllersWithViews(options =>
            {
                options.Filters.Add(new AuthorizeFilter(requireAuthPolicy));
            });

            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
                options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Logout");
                options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/AccessDenied");
            });

            builder.Services.AddHttpClient();
            builder.Services.Configure<InvoiceExtractionOptions>(builder.Configuration.GetSection("InvoiceExtraction"));
            builder.Services.Configure<InsightsOptions>(builder.Configuration.GetSection("Insights"));
            builder.Services.AddScoped<IReportExportService, ReportExportService>();
            builder.Services.AddScoped<IInvoiceExtractionService, InvoiceExtractionService>();
            builder.Services.AddScoped<IDuplicateDetectionService, DuplicateDetectionService>();
            builder.Services.AddScoped<IReportQueryService, ReportQueryService>();
            builder.Services.AddScoped<IInsightsService, InsightsService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (db.Database.IsSqlite())
                {
                    await db.Database.EnsureCreatedAsync();
                }
                else
                {
                    await db.Database.MigrateAsync();
                }

                await DbInitializer.SeedAsync(app.Services);
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.MapControllers();
            app.MapRazorPages()
                .WithStaticAssets();

            await app.RunAsync();
        }
    }
}
