namespace OrderService.Application.Dtos
{
    public record PaymentStatusDto
    {
        public Guid PaymentId { get; init; }
        public Guid OrderId { get; init; }
        public bool Success { get; init; }
        public string? Reason { get; init; } //а надо??? не надо
        public string? MessageId { get; init; }
        public DateTimeOffset ProcessedAt { get; init; } //а надо??
    }

}
