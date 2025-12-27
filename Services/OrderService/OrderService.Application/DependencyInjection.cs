using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.UseCases;

namespace OrderService.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            // Регистрация Use Cases
            _ = services.AddScoped<CreateOrderUseCase>();
            _ = services.AddScoped<GetOrderStatusUseCase>();
            _ = services.AddScoped<ListOrdersUseCase>();
            _ = services.AddScoped<ApplyPaymentStatusUseCase>();

            return services;
        }
    }
}