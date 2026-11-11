using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

using Bookstore.Web.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureConfiguration();

builder.ConfigureServices();

builder.ConfigureAuthentication();

builder.Services.AddApplicationServices(builder.Environment);

var app = builder.Build();

await app.ConfigureMiddlewareAsync();

app.Run();