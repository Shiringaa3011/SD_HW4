using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrderService.Application;
using OrderService.Application.Dtos;
using OrderService.Application.UseCases;
using OrderService.Domain.Exceptions;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Common.Messaging.RabbitMQ;
using Common.Messaging.Abstractions.Dtos;
using Common.Messaging.Abstractions.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderService.WebApi
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            _ = builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            ConfigureServices(builder.Services, builder.Configuration);

            WebApplication app = builder.Build();
            _ = await WaitForDatabaseAsync(app.Services);
            ConfigureMiddleware(app);

            Console.WriteLine($"Order Service запущен");
            Console.WriteLine($"Swagger: http://localhost:8080/swagger");

            await app.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            _ = services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            // Swagger
            _ = services.AddEndpointsApiExplorer();
            _ = services.AddSwaggerGen(c => c.SwaggerDoc("v1",
                new OpenApiInfo
                {
                    Title = "Order Service API",
                    Version = "v1",
                    Description = "Сервис управления заказами"
                }));

            // Регистрация зависимостей
            _ = services.AddApplication();
            _ = services.AddInfrastructure(configuration);

            // Логирование
            _ = services.AddLogging(logging =>
            {
                _ = logging.AddConsole();
                _ = logging.AddDebug();
            });

            _ = services.AddCors(options => options.AddPolicy("AllowAll", policy =>
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader()));
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            _ = app.UseRouting();

            // Обработчик ошибок
            _ = app.UseExceptionHandler(appError => appError.Run(async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    Message = "Internal server error",
                    RequestId = context.TraceIdentifier,
                    Timestamp = DateTime.UtcNow
                };

                string json = JsonSerializer.Serialize(errorResponse);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                await context.Response.Body.FlushAsync();
            }));

            _ = app.UseSwagger();
            _ = app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1"));

            _ = app.UseCors("AllowAll");

            // Health check endpoint
            _ = app.MapGet("/health", () => new
            {
                status = "healthy",
                service = "order-service",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });

            // Эндпоинты
            ConfigureEndpoints(app);

            _ = app.MapControllers();
        }

        private static void ConfigureEndpoints(WebApplication app)
        {
            // 1. Создание заказа
            _ = app.MapPost("/api/orders", async (
                [FromQuery] Guid userId,
                [FromBody] CreateOrderRequest request,
                [FromServices] CreateOrderUseCase createOrderUseCase) =>
            {
                if (userId == Guid.Empty)
                {
                    return Results.BadRequest(new { Error = "User ID is required" });
                }

                if (request.Amount <= 0)
                {
                    return Results.BadRequest(new { Error = "Amount must be greater than zero" });
                }

                if (string.IsNullOrWhiteSpace(request.Description))
                {
                    return Results.BadRequest(new { Error = "Description is required" });
                }

                try
                {
                    OrderDto orderDto = await createOrderUseCase.HandleAsync(
                        new CreateOrderDto(userId, request.Amount, request.Description));

                    return Results.Ok(new
                    {
                        OrderId = orderDto.Id,
                        orderDto.UserId,
                        orderDto.Amount,
                        orderDto.Currency,
                        orderDto.Description,
                        orderDto.Status,
                        orderDto.CreatedAt
                    });
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
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            // 2. Просмотр списка заказов
            _ = app.MapGet("/api/orders", async (
                [FromQuery] Guid userId,
                [FromServices] ListOrdersUseCase listOrdersUseCase,
                [FromQuery] int page = 0,
                [FromQuery] int pageSize = 20) =>
            {
                if (userId == Guid.Empty)
                {
                    return Results.BadRequest(new { Error = "User ID is required" });
                }

                try
                {
                    IReadOnlyCollection<OrderDto> orders = await listOrdersUseCase.HandleAsync(userId);

                    var pagedOrders = orders
                        .Skip(page * pageSize)
                        .Take(pageSize)
                        .Select(o => new
                        {
                            OrderId = o.Id,
                            o.UserId,
                            o.Amount,
                            o.Currency,
                            o.Description,
                            o.Status,
                            o.CreatedAt
                        });

                    return Results.Ok(new
                    {
                        UserId = userId,
                        TotalCount = orders.Count,
                        Page = page,
                        PageSize = pageSize,
                        Orders = pagedOrders
                    });
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
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status500InternalServerError);

            // 3. Просмотр статуса заказа
            _ = app.MapGet("/api/orders/{orderId:guid}", async (
                Guid orderId,
                [FromQuery] Guid userId,
                [FromServices] GetOrderStatusUseCase getOrderStatusUseCase) =>
            {
                if (userId == Guid.Empty)
                {
                    return Results.BadRequest(new { Error = "User ID is required" });
                }

                try
                {
                    OrderDto orderDto = await getOrderStatusUseCase.HandleAsync(orderId);

                    // Проверяем, что заказ принадлежит пользователю
                    return orderDto.UserId != userId
                        ? Results.Unauthorized()
                        : Results.Ok(new
                    {
                        OrderId = orderDto.Id,
                        orderDto.UserId,
                        orderDto.Amount,
                        orderDto.Currency,
                        orderDto.Description,
                        orderDto.Status,
                        orderDto.CreatedAt
                    });
                }
                catch (OrderNotFoundException ex)
                {
                    return Results.NotFound(new { Error = ex.Message });
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
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status500InternalServerError);

            // Проверка базы данных
            _ = app.MapGet("/check-db", async ([FromServices] OrderDbContext dbContext) =>
            {
                try
                {
                    bool canConnect = await dbContext.Database.CanConnectAsync();
                    return Results.Ok(new
                    {
                        DatabaseConnected = canConnect,
                        Time = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    return Results.Problem(
                        detail: ex.Message,
                        statusCode: 500,
                        title: "Database connection failed");
                }
            });
        }

        private static async Task<bool> WaitForDatabaseAsync(IServiceProvider services, int maxRetries = 30)
        {
            using IServiceScope scope = services.CreateScope();
            OrderDbContext dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            ILogger<Program> logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    logger.LogInformation("Database connection attempt {Attempt}/{Max}", i + 1, maxRetries);
                    bool canConnect = await dbContext.Database.CanConnectAsync();
                    if (canConnect)
                    {
                        logger.LogInformation("Database connection successful");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Database connection failed: {Message}", ex.Message);
                }

                await Task.Delay(2000);
            }

            logger.LogError("Failed to connect to database after {MaxRetries} attempts", maxRetries);
            return false;
        }
    }

    public record CreateOrderRequest(
        decimal Amount,
        string Description,
        string? Currency = null);
}