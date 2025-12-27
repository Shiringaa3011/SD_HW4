using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using PaymentsService.Domain.Exceptions;
using PaymentsService.Domain.ValueTypes;

namespace PaymentsService.Application.UseCases
{
    public class TopUpAccountUseCase(IAccountRepository accounts)
    {
        private readonly IAccountRepository _accounts = accounts;

        public async Task HandleAsync(TopUpAccountDto request, CancellationToken ct = default)
        {
            (Domain.Entities.Account? account, int loadedVersion) = await _accounts.GetByUserIdWithVersionAsync(
            request.UserId, ct);
            if (account == null)
            {
                throw new AccountNotFoundException(request.UserId);
            }

            account.TopUp(Money.Create(request.Amount));

            bool updated = await _accounts.TryUpdateWithVersionAsync(
                account,
                expectedVersion: loadedVersion,
                ct);

            if (!updated)
            {
                throw new Exception("Account was modified concurrently");
            }
        }
    }

}
