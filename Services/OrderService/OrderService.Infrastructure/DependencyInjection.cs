using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Ports;
using OrderService.Application.Workers;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Persistence.Repositories;
using OrderService.Infrastructure.Idempotency;
using OrderService.Infrastructure.Workers;
using Common.Messaging.Abstractions.Interfaces;
using Common.Messaging.RabbitMQ;

namespace OrderService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            _ = services.AddDbContext<OrderDbContext>(options => _ = options.UseNpgsql(
                    configuration.GetConnectionString("OrderDb"),
                    npgsqlOptions =>
                        _ = npgsqlOptions.MigrationsAssembly(typeof(OrderDbContext).Assembly.FullName)));

            // Repositories
            _ = services.AddScoped<IOrderRepository, OrderRepository>();
            _ = services.AddScoped<IOutboxRepository, OutboxRepository>();

            // Idempotency
            _ = services.AddScoped<IIdempotencyService, IdempotencyService>();

            // Unit of Work
            _ = services.AddScoped<IUnitOfWork, UnitOfWork>();

            // RabbitMQ
            _ = services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
            _ = services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

            // Outbox Worker
            _ = services.Configure<OutboxWorkerOptions>(configuration.GetSection("OutboxWorker"));
            _ = services.AddHostedService<OutboxWorker>();

            _ = services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
            _ = services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();

            // Payment Result Consumer
            _ = services.AddHostedService<PaymentResultConsumer>();

            return services;
        }
    }
}