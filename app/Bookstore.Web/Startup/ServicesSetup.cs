using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SecretsManager.Model;
using Amazon.SecretsManager;
using Bookstore.Data;
using Bookstore.Domain.AdminUser;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace Bookstore.Web.Startup
{
    public static class ServicesSetup
    {
        public static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder)
        {
            // TLS is terminated at the AWS load balancer; honour the proto/host it forwards so
            // Request.Scheme/Host match the original client request. The ALB's IP is not known
            // ahead of time, so the known network/proxy allow-lists must be cleared.
            builder.Services.Configure<ForwardedHeadersOptions>(o =>
            {
                o.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedFor;
                o.KnownNetworks.Clear();
                o.KnownProxies.Clear();
            });

            builder.Services.AddControllersWithViews(x =>
            {
                // Deny anonymous access by default; public endpoints opt out with [AllowAnonymous].
                x.Filters.Add(new AuthorizeFilter());
                x.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            });

            // AWSSDK v4: GetAWSOptions() always returns an AWSOptions instance. Any Profile/Region
            // present in the "AWS" appsettings section is honoured for local development; when those
            // values are absent (deployed on EC2/App Runner) the clients fall back to the default
            // credential resolution chain (instance profile / task role) and the default region
            // resolution chain (AWS_REGION, EC2 instance metadata), so no explicit fallback is needed.
            var awsOptions = builder.Configuration.GetAWSOptions();
            builder.Services.AddDefaultAWSOptions(awsOptions);
            builder.Services.AddAWSService<IAmazonS3>();
            builder.Services.AddAWSService<IAmazonRekognition>();

            var connString = GetDatabaseConnectionString(builder.Configuration);
            builder.Services.AddDbContext<ApplicationDbContext>(option => option.UseSqlServer(connString));
            builder.Services.AddSession();

            return builder;
        }

        // If we find a non-empty connection string in appsettings, use it, otherwise
        // attempt to build it from data in Secrets Manager
        private static string GetDatabaseConnectionString(ConfigurationManager configuration)
        {
            // This is the key of a string value in Parameter Store containing the name of the
            // secret in Secrets Manager that in turn contains the credentials of the database in
            // Amazon RDS. The reason for the indirection is that a secret name is suffixed automatically
            // by the CDK with a random string. Using a fixed Parameter Store value to point to the
            // randomly-named secret insulates the application from variability in the name of
            // the secret.
            const string DbSecretsParameterName = "dbsecretsname";

            var connString = configuration.GetConnectionString("BookstoreDbDefaultConnection");
            if (!string.IsNullOrEmpty(connString))
            {
                Console.WriteLine("Using localdb connection string");
                return connString;
            }

            try
            {
                var dbSecretId = configuration[DbSecretsParameterName];
                Console.WriteLine($"Reading db credentials from secret {dbSecretId}");

                // Read the db secrets posted into Secrets Manager by the CDK. The secret provides the host,
                // port, userid, and password, which we format into the final connection string for SQL Server.
                // For this code to work locally, appsettings.json must contain an AWS object with profile and
                // region info. When deployed to an EC2 instance, credentials and region will be inferred from
                // the instance profile applied to the instance.
                // GetAWSOptions() never returns null under AWSSDK v4, so the former
                // "new AmazonSecretsManagerClient()" fallback branch was unreachable. CreateServiceClient
                // covers both modes: locally it uses the profile/region from the appsettings "AWS"
                // section, and when deployed (where that section carries no profile) it falls through
                // to the default credential/region resolution chain, i.e. the EC2 instance profile.
                var options = configuration.GetAWSOptions();
                IAmazonSecretsManager secretsManagerClient = options.CreateServiceClient<IAmazonSecretsManager>();

                // Startup-only, synchronous context (ConfigureServices cannot be awaited and the
                // connection string is required before the DbContext can be registered), so blocking
                // on the task here is deliberate and not a request-path sync-over-async violation.
                var response = secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest
                {
                    SecretId = dbSecretId
                }).GetAwaiter().GetResult();

                var dbSecrets = JsonSerializer.Deserialize<DbSecrets>(response.SecretString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (dbSecrets == null)
                {
                    throw new JsonException($"Secret '{dbSecretId}' did not contain a JSON object with database credentials.");
                }

                // Microsoft.Data.SqlClient (bundled with EF Core 10) defaults Encrypt=true, so the
                // certificate settings below are required for the RDS/dev server certificates.
                var partialConnString = $"Server={dbSecrets.Host},{dbSecrets.Port}; Initial Catalog=BobsUsedBookStore;MultipleActiveResultSets=true; Integrated Security=false;Encrypt=True;TrustServerCertificate=true;";

                var builder = new SqlConnectionStringBuilder(partialConnString)
                {
                    UserID = dbSecrets.Username,
                    Password = dbSecrets.Password
                };

                connString = builder.ConnectionString;
            }
            catch (AmazonSecretsManagerException e)
            {
                // InnerException is frequently null (for example on ResourceNotFound), so guard it -
                // otherwise the diagnostic itself throws a NullReferenceException.
                var inner = e.InnerException?.Message ?? "(none)";
                Console.WriteLine($"Failed to read secret {configuration[DbSecretsParameterName]}, error {e.Message}, inner {inner}");
            }
            catch (JsonException e)
            {
                Console.WriteLine($"Failed to parse content for secret {configuration[DbSecretsParameterName]}, error {e.Message}");
            }

            if (string.IsNullOrEmpty(connString))
            {
                // Fail fast: previously both catch blocks fell through and returned null, which
                // surfaced much later as an obscure "connection string not initialized" error.
                throw new InvalidOperationException(
                    "Unable to determine the database connection string. Set the configuration key " +
                    "'ConnectionStrings:BookstoreDbDefaultConnection' in appsettings (or an environment-specific " +
                    $"appsettings file), or make the Secrets Manager secret named by the configuration key " +
                    $"'{DbSecretsParameterName}' (current value: '{configuration[DbSecretsParameterName] ?? "<not set>"}') " +
                    "readable by this application's AWS credentials.");
            }

            return connString;
        }
    }
}
