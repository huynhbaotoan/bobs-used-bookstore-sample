using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.AdminUser;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration (was Startup/ConfigurationSetup.cs)
// ---------------------------------------------------------------------------
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddSystemsManager("/BobsBookstore/");
}

// ---------------------------------------------------------------------------
// Services (was Startup/ServicesSetup.cs)
// ---------------------------------------------------------------------------
builder.Services.AddControllersWithViews(x =>
{
    x.Filters.Add(new AuthorizeFilter());
    x.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonRekognition>();

var connString = GetDatabaseConnectionString(builder.Configuration);
builder.Services.AddDbContext<ApplicationDbContext>(option => option.UseSqlServer(connString));
builder.Services.AddSession();

// ---------------------------------------------------------------------------
// Authentication (was Startup/AuthenticationSetup.cs)
// For the 'Development' profile we fake authentication. For 'Test' and 'Production'
// profiles we use Amazon Cognito's Hosted UI
// ---------------------------------------------------------------------------
var cognitoDomain = builder.Configuration["Authentication:Cognito:CognitoDomain"];
var cognitoClientId = GetCognitoClientId(builder.Configuration);
var cognitoAppSignOutUrl = builder.Configuration["Authentication:Cognito:AppSignOutUrl"];

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(x =>
        {
            x.AddScheme<LocalAuthenticationHandler>("localauth", null);
            x.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            x.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = "localauth";
        }).AddCookie();
}
else
{
    JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

    builder.Services
        .AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            x.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(x =>
        {
            x.ResponseType = builder.Configuration["Authentication:Cognito:ResponseType"];
            x.MetadataAddress = builder.Configuration["Authentication:Cognito:MetadataAddress"];
            x.ClientId = GetCognitoClientId(builder.Configuration);
            x.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "cognito:username",
                RoleClaimType = "cognito:groups"
            };

            x.Events.OnRedirectToIdentityProviderForSignOut = OnRedirectToIdentityProviderForSignOut;
            x.Events.OnTokenValidated = SaveCustomerDetailsAsync;
        });
}

// ---------------------------------------------------------------------------
// Dependency injection (was Startup/DependencyInjectionSetup.cs)
// ---------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IBookService, BookService>();
builder.Services.AddTransient<IOrderService, OrderService>();
builder.Services.AddTransient<IReferenceDataService, ReferenceDataService>();
builder.Services.AddTransient<IOfferService, OfferService>();
builder.Services.AddTransient<ICustomerService, CustomerService>();
builder.Services.AddTransient<IAddressService, AddressService>();
builder.Services.AddTransient<IShoppingCartService, ShoppingCartService>();
builder.Services.AddTransient<IImageResizeService, ImageResizeService>();

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IFileService, LocalFileService>(_ => new LocalFileService(builder.Environment.WebRootPath));
    builder.Services.AddTransient<IImageValidationService, LocalImageValidationService>();
}
else
{
    builder.Services.AddTransient<IFileService, S3FileService>();
    builder.Services.AddTransient<IImageValidationService, RekognitionImageValidationService>();
}

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline (was Startup/MiddlewareSetup.cs)
// ---------------------------------------------------------------------------
app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next(context);
});

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
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapAreaControllerRoute(
    name: "Admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Orders}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Create/update the database
using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetService<ApplicationDbContext>()!;
    await context.Database.EnsureCreatedAsync();

    // Check if RowVersion columns exist, if not recreate database
    try
    {
        await context.OrderItem.FirstOrDefaultAsync();
    }
    catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("RowVersion"))
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}

app.Run();

string GetCognitoClientId(IConfiguration configuration)
{
    switch (configuration["AWS:Service"])
    {
        case "EC2":
            return configuration["Authentication:Cognito:EC2ClientId"];

        case "AppRunner":
            return configuration["Authentication:Cognito:AppRunnerClientId"];

        default:
            return configuration["Authentication:Cognito:LocalClientId"];
    }
}

Task OnRedirectToIdentityProviderForSignOut(RedirectContext context)
{
    context.ProtocolMessage.Scope = "openid";
    context.ProtocolMessage.ResponseType = "code";

    var logoutUrl = $"{context.Request.Scheme}://{context.Request.Host}{cognitoAppSignOutUrl}";

    context.ProtocolMessage.IssuerAddress = $"{cognitoDomain}/logout?client_id={cognitoClientId}&logout_uri={logoutUrl}";

    context.Properties.Items.Remove(CookieAuthenticationDefaults.AuthenticationScheme);

    context.Properties.Items.Remove(OpenIdConnectDefaults.AuthenticationScheme);

    return Task.CompletedTask;
}

async Task SaveCustomerDetailsAsync(TokenValidatedContext context)
{
    var customerService = context.HttpContext.RequestServices.GetService<ICustomerService>();

    var dto = new CreateOrUpdateCustomerDto(
        context.Principal.GetSub(),
        context.Principal.Identity.Name,
        context.Principal.FindFirst("given_name").Value,
        context.Principal.FindFirst("family_name").Value);

    await customerService.CreateOrUpdateCustomerAsync(dto);
}

// If we find a non-empty connection string in appsettings, use it, otherwise
// attempt to build it from data in Secrets Manager
static string GetDatabaseConnectionString(ConfigurationManager configuration)
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
        IAmazonSecretsManager secretsManagerClient;
        var options = configuration.GetAWSOptions();
        if (options != null)
        {
            // local "integrated" debug mode using credentials/region in appsettings
            secretsManagerClient = options.CreateServiceClient<IAmazonSecretsManager>();
        }
        else
        {
            // deployed mode using credentials/region inferred on host
            secretsManagerClient = new AmazonSecretsManagerClient();
        }
        var response = secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = dbSecretId
        }).Result;

        var dbSecrets = JsonSerializer.Deserialize<DbSecrets>(response.SecretString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var partialConnString = $"Server={dbSecrets.Host},{dbSecrets.Port}; Initial Catalog=BobsUsedBookStore;MultipleActiveResultSets=true; Integrated Security=false;TrustServerCertificate=true;";

        var builder = new SqlConnectionStringBuilder(partialConnString)
        {
            UserID = dbSecrets.Username,
            Password = dbSecrets.Password
        };

        connString = builder.ConnectionString;
    }
    catch (AmazonSecretsManagerException e)
    {
        Console.WriteLine($"Failed to read secret {configuration[DbSecretsParameterName]}, error {e.Message}, inner {e.InnerException.Message}");
    }
    catch (JsonException e)
    {
        Console.WriteLine($"Failed to parse content for secret {configuration[DbSecretsParameterName]}, error {e.Message}");
    }

    return connString;
}
