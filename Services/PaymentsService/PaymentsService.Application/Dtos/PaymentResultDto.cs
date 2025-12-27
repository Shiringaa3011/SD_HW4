namespace PaymentsService.Application.Dtos
{
    public record PaymentResultDto(string MessageId, Guid OrderId, Guid UserId, bool Success, string? Reason = null);

}
