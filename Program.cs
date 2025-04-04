using EduSubmit.Data;
using EduSubmit.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace EduSubmit
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Reset and configure configuration sources
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddEnvironmentVariables();

            var connectionString = Environment.GetEnvironmentVariable("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new Exception("Database connection string is not set. Ensure you have added it to Railway environment variables.");
            }

            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(connectionString ?? throw new InvalidOperationException("Database connection string not found!")));

            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login"; // Redirects to login if not authenticated
                    options.AccessDeniedPath = "/Account/Login"; // Redirects on access denied
                });

            builder.Services.AddAuthorization();
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<CodeExecutionService>();
            builder.Services.AddControllersWithViews();

            // Configuration for Kestrel to deploy on Railway.app
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                var port = Environment.GetEnvironmentVariable("PORT") ?? "8080"; // Default to 8080 if PORT is not set
                serverOptions.ListenAnyIP(int.Parse(port));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".py"] = "text/plain"; // Allow Python files
            provider.Mappings[".cs"] = "text/plain"; // Allow C# files

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider
            });

            app.UseRouting();
            app.UseAuthentication(); // Added this to ensure authentication middleware is used
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}