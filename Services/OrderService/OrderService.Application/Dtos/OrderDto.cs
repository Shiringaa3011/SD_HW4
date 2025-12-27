namespace OrderService.Application.Dtos
{
    public record OrderDto(
        Guid Id,
        Guid UserId,
        decimal Amount,
        string Currency,
        string Description,
        string Status,
        DateTimeOffset CreatedAt);

}
