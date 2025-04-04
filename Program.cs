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

            // Add services to the container.
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

            // Configuration for Kestrel to deploy the project on Railway.app
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
                serverOptions.ListenAnyIP(Int32.Parse(port));
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".py"] = "text/plain"; // Allow Python files
            provider.Mappings[".cs"] = "text/plain"; // Allow CSharp files

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider
            });

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}