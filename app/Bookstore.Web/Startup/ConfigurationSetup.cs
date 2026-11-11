using System;
using AWS.Logger;
using AWS.Logger.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookstore.Web.Startup
{
    public static class ConfigurationSetup
    {
        public static WebApplicationBuilder ConfigureConfiguration(this WebApplicationBuilder builder)
        {
            if (!builder.Environment.IsDevelopment())
            {
                // Amazon.Extensions.Configuration.SystemsManager 7.x: pass optional:true so that a
                // missing/unreachable Parameter Store path (for example when running the container
                // outside AWS, or before the CDK stack has published the parameters) does not throw
                // during host construction. Values are loaded once at startup - no reload timer is
                // configured, which keeps startup deterministic and avoids background SSM polling.
                builder.Configuration.AddSystemsManager("/BobsBookstore/", optional: true);

                // AWS.Logger.AspNetCore 4.x. The CloudWatch log group name and the log level
                // filters both live in the existing "Logging" section of appsettings.json
                // (Logging:LogGroup + Logging:LogLevel), so bind the provider to that section
                // rather than the library default of "AWS.Logging". Region/credentials are
                // resolved from the host (EC2 instance profile / task role).
                AWSLoggerConfigSection? loggerConfig = builder.Configuration.GetAWSLoggingConfigSection("Logging");
                if (loggerConfig != null && !string.IsNullOrEmpty(loggerConfig.Config?.LogGroup))
                {
                    builder.Logging.AddAWSProvider(loggerConfig);
                }
                else
                {
                    Console.WriteLine("CloudWatch logging not configured (Logging:LogGroup is missing); using default logging providers.");
                }
            }

            return builder;
        }
    }
}
