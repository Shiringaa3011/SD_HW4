namespace PaymentsService.Application.Dtos
{
    public record AccountBalanceDto(Guid UserId, decimal Balance, string Currency);

}
