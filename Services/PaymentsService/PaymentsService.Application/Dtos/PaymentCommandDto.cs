namespace PaymentsService.Application.Dtos
{
    public record PaymentCommandDto(string MessageId, Guid OrderId, Guid UserId, decimal Amount, string Currency);

}
