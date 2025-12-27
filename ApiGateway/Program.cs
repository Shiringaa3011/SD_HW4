using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using System.Text;

namespace ApiGateway
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Swagger с детальной конфигурацией
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "E-Commerce Gateway API",
                    Version = "v1",
                    Description = "Единая точка входа для всех микросервисов"
                });

                // Группируем по тегам
                c.TagActionsBy(api =>
                {
                    var relativePath = api.RelativePath ?? "";
                    if (relativePath.Contains("/orders"))
                        return new[] { "Orders" };
                    if (relativePath.Contains("/accounts"))
                        return new[] { "Accounts" };
                    return new[] { "Gateway" };
                });
            });

            // HttpClient с настройками
            builder.Services.AddHttpClient("GatewayClient", client =>
            {
                client.DefaultRequestHeaders.Add("User-Agent", "APIGateway/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            var app = builder.Build();

            // Swagger UI
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "E-Commerce Gateway v1");
                c.RoutePrefix = "swagger";
            });

            app.UseRouting();

            // Health check самого гейтвея
            app.MapGet("/health", () =>
            {
                return Results.Ok(new
                {
                    status = "healthy",
                    service = "api-gateway",
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0",
                    port = 5002
                });
            })
            .WithName("GetGatewayHealth")
            .WithTags("Gateway")
            .Produces(StatusCodes.Status200OK);

            // 1. Создание заказа
            app.MapPost("/api/orders", async (
                [FromQuery] Guid userId,
                [FromBody] CreateOrderRequest request,
                IHttpClientFactory httpClientFactory) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                if (request.Amount <= 0)
                    return Results.BadRequest(new { Error = "Amount must be greater than zero" });

                if (string.IsNullOrWhiteSpace(request.Description))
                    return Results.BadRequest(new { Error = "Description is required" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.PostAsJsonAsync(
                        $"http://order-service:8080/api/orders?userId={userId}",
                        request);

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Order creation failed");
                }
            })
            .WithName("CreateOrder")
            .WithTags("Orders")
            .Accepts<CreateOrderRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            // 2. Получение списка заказов
            app.MapGet("/api/orders", async (
                IHttpClientFactory httpClientFactory,
                [FromQuery] Guid userId,
                [FromQuery] int page = 0,
                [FromQuery] int pageSize = 20
                ) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.GetAsync(
                        $"http://order-service:8080/api/orders?userId={userId}&page={page}&pageSize={pageSize}");

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Failed to get orders");
                }
            })
            .WithName("GetOrders")
            .WithTags("Orders")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            // 3. Получение статуса заказа
            app.MapGet("/api/orders/{orderId:guid}", async (
                Guid orderId,
                [FromQuery] Guid userId,
                IHttpClientFactory httpClientFactory) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.GetAsync(
                        $"http://order-service:8080/api/orders/{orderId}?userId={userId}");

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Failed to get order status");
                }
            })
            .WithName("GetOrderStatus")
            .WithTags("Orders")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

            // 1. Создание счёта
            app.MapPost("/api/accounts", async (
                [FromQuery] Guid userId,
                [FromBody] CreateAccountRequest request,
                IHttpClientFactory httpClientFactory) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.PostAsJsonAsync(
                        $"http://payment-service:8080/api/accounts?userId={userId}",
                        request);

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Failed to create account");
                }
            })
            .WithName("CreateAccount")
            .WithTags("Accounts")
            .Accepts<CreateAccountRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            // 2. Пополнение счёта
            app.MapPost("/api/accounts/deposit", async (
                [FromQuery] Guid userId,
                [FromBody] TopUpAccountRequest request,
                IHttpClientFactory httpClientFactory) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                if (request.Amount <= 0)
                    return Results.BadRequest(new { Error = "Amount must be greater than zero" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.PostAsJsonAsync(
                        $"http://payment-service:8080/api/accounts/deposit?userId={userId}",
                        request);

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Failed to top up account");
                }
            })
            .WithName("TopUpAccount")
            .WithTags("Accounts")
            .Accepts<TopUpAccountRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

            // 3. Получение баланса
            app.MapGet("/api/accounts/balance", async (
                [FromQuery] Guid userId,
                IHttpClientFactory httpClientFactory) =>
            {
                if (userId == Guid.Empty)
                    return Results.BadRequest(new { Error = "User ID is required" });

                try
                {
                    var client = httpClientFactory.CreateClient("GatewayClient");
                    var response = await client.GetAsync(
                        $"http://payment-service:8080/api/accounts/balance?userId={userId}");

                    var content = await response.Content.ReadAsStringAsync();
                    return Results.Text(content, "application/json", Encoding.UTF8, (int)response.StatusCode);
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Failed to get balance");
                }
            })
            .WithName("GetBalance")
            .WithTags("Accounts")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

            Console.WriteLine("API Gateway запущен на порту 5002");
            Console.WriteLine("Swagger UI: http://localhost:5002/swagger");
            Console.WriteLine("Health check: http://localhost:5002/health");

            app.Run();
        }

        // Модели запросов
        public record CreateOrderRequest(
            decimal Amount,
            string Description,
            string? Currency = null);

        public record CreateAccountRequest(string Currency = "RUB");

        public record TopUpAccountRequest(
            decimal Amount,
            string? Description = null);
    }
}