using Bookstore.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Bookstore.Web.Startup
{
    public static class MiddlewareSetup
    {
        public static async Task<WebApplication> ConfigureMiddlewareAsync(this WebApplication app)
        {
            // Behind the AWS load balancer TLS is terminated at the ALB, so the request arrives
            // over plain HTTP. UseForwardedHeaders must run FIRST so that Request.Scheme (and Host)
            // reflect the original client request before any cookie is written or redirect generated
            // (the OIDC correlation/state cookie is SameSite=None+Secure, so the scheme must genuinely
            // be https at the time it is written). Options are configured in ServicesSetup.
            app.UseForwardedHeaders();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
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
            // Session must be available to authentication handlers and to endpoints, so it is
            // registered after routing and before authentication/authorization.
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapAreaControllerRoute(
                name: "Admin",
                areaName: "Admin",
                pattern: "Admin/{controller=Orders}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Create/update the database. This runs exactly once per process, here at the end of
            // host configuration and before app.Run() starts accepting requests.
            await EnsureDatabaseAsync(app);

            return app;
        }

        // Creates the database if it does not exist, and repairs a pre-existing database that was
        // created before the RowVersion concurrency tokens were added to the entities.
        private static async Task EnsureDatabaseAsync(WebApplication app)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MiddlewareSetup).FullName!);

            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await context.Database.EnsureCreatedAsync();

                // Probe a table that carries the [Timestamp] RowVersion column. If the database
                // predates that column, SQL Server reports "Invalid column name 'RowVersion'"
                // (error 207) and the schema is recreated.
                try
                {
                    await context.OrderItem.FirstOrDefaultAsync();
                }
                catch (SqlException ex) when (IsMissingRowVersionColumn(ex))
                {
                    logger.LogWarning(ex, "Database schema is missing the RowVersion concurrency columns; recreating the database.");
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();
                }
            }
            catch (Exception ex)
            {
                // A connectivity/permission failure must NOT be mistaken for a schema problem and
                // must not take the whole host down at startup - log it loudly and let the app start
                // so that the error surfaces with full diagnostics on the first request instead.
                logger.LogError(ex, "Startup database create/repair failed. The application will start, but database access will fail until this is resolved.");
            }
        }

        private static bool IsMissingRowVersionColumn(SqlException ex)
        {
            // Match on the SQL Server error number rather than only the message text (the message is
            // localized and was reworded in the Microsoft.Data.SqlClient version bundled with
            // EF Core 10): 207 = Invalid column name, 1911 = column does not exist in the table.
            foreach (SqlError error in ex.Errors)
            {
                if ((error.Number == 207 || error.Number == 1911) &&
                    error.Message.Contains("RowVersion", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return ex.Message.Contains("RowVersion", StringComparison.OrdinalIgnoreCase);
        }
    }
}
