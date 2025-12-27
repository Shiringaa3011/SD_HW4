using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentsService.Application.Ports;
using PaymentsService.Application.Workers;
using Common.Messaging.Abstractions.Interfaces;
using Common.Messaging.RabbitMQ;
using PaymentsService.Infrastructure.Persistence;
using PaymentsService.Infrastructure.Persistence.Repositories;
using PaymentsService.Infrastructure.Workers;

namespace PaymentsService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database
            string? connectionString = configuration.GetConnectionString("PaymentDb");

            _ = services.AddDbContext<PaymentsDbContext>(options => _ = options.UseNpgsql(
                    connectionString,
                    npgsqlOptions => _ = npgsqlOptions.MigrationsAssembly(typeof(PaymentsDbContext).Assembly.FullName)));

            // Repositories
            _ = services.AddScoped<IAccountRepository, AccountRepository>();
            _ = services.AddScoped<IPaymentRepository, PaymentRepository>();
            _ = services.AddScoped<IWithdrawalRepository, WithdrawalRepository>();
            _ = services.AddScoped<IPaymentInboxRepository, PaymentInboxRepository>();
            _ = services.AddScoped<IPaymentOutboxRepository, PaymentOutboxRepository>();

            // Unit of Work
            _ = services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Messaging
            _ = services.Configure<RabbitMqOptions>(
                configuration.GetSection("RabbitMQ"));
            _ = services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
            _ = services.AddHostedService<PaymentCommandConsumer>();

            _ = services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMQ"));
            _ = services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

            _ = services.AddHostedService<InboxProcessor>();
            _ = services.AddHostedService<PaymentOutboxWorker>();

            return services;
        }
    }
}