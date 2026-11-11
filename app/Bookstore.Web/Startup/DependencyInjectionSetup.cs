using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Books;
using Bookstore.Domain;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.Carts;
using Bookstore.Domain.ReferenceData;
using Bookstore.Data.Repositories;
using Bookstore.Data.FileServices;
using Bookstore.Domain.Addresses;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.ImageResizeService;

namespace Bookstore.Web.Startup
{
    using Bookstore.Data;

    public static class DependencyInjectionSetup
    {
        public static WebApplicationBuilder ConfigureDependencyInjection(this WebApplicationBuilder builder)
        {
            builder.Services.AddApplicationServices(builder.Environment);

            return builder;
        }

        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IWebHostEnvironment environment)
        {
            services.AddHttpContextAccessor();
            services.AddTransient<IBookService, BookService>();
            services.AddTransient<IOrderService, OrderService>();
            services.AddTransient<IReferenceDataService, ReferenceDataService>();
            services.AddTransient<IOfferService, OfferService>();
            services.AddTransient<ICustomerService, CustomerService>();
            services.AddTransient<IAddressService, AddressService>();
            services.AddTransient<IShoppingCartService, ShoppingCartService>();
            services.AddTransient<IImageResizeService, ImageResizeService>();

            services.AddScoped<ICustomerRepository, CustomerRepository>();
            services.AddScoped<IAddressRepository, AddressRepository>();
            services.AddScoped<IBookRepository, BookRepository>();
            services.AddScoped<IOfferRepository, OfferRepository>();
            services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

            services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

            if (environment.IsDevelopment())
            {
                services.AddTransient<IFileService, LocalFileService>(_ => new LocalFileService(environment.WebRootPath));
                services.AddTransient<IImageValidationService, LocalImageValidationService>();
            }
            else
            {
                services.AddTransient<IFileService, S3FileService>();
                services.AddTransient<IImageValidationService, RekognitionImageValidationService>();
            }

            return services;
        }
    }
}