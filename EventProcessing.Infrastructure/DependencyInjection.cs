using EventProcessing.Core.Interfaces;
using EventProcessing.Infrastructure.Data;
using EventProcessing.Infrastructure.Messaging;
using EventProcessing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventProcessing.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<ISummaryRepository, SummaryRepository>();

            services.AddSingleton<IEventPublisher, EventPublisher>();

            return services;
        }
    }
}