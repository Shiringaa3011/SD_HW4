using Microsoft.Extensions.DependencyInjection;
using PaymentsService.Application.UseCases;

namespace PaymentsService.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Регистрация Use Cases
            _ = services.AddScoped<CreateAccountUseCase>();
            _ = services.AddScoped<TopUpAccountUseCase>();
            _ = services.AddScoped<GetBalanceUseCase>();
            _ = services.AddScoped<ProcessPaymentUseCase>();

            return services;
        }
    }
}