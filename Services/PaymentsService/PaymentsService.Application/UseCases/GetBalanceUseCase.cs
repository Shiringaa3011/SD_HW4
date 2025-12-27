using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Exceptions;

namespace PaymentsService.Application.UseCases
{
    public class GetBalanceUseCase(IAccountRepository accounts)
    {
        private readonly IAccountRepository _accounts = accounts;

        public async Task<AccountBalanceDto> HandleAsync(Guid userId, CancellationToken ct = default)
        {
            Account account = await _accounts.GetByUserIdAsync(userId, ct)
                          ?? throw new AccountNotFoundException(userId);

            return new AccountBalanceDto(account.UserId, account.Balance.Amount, account.Balance.Currency);
        }
    }

}
