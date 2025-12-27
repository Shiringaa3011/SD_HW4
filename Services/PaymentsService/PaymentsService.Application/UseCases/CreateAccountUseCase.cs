using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using PaymentsService.Domain.Entities;

namespace PaymentsService.Application.UseCases
{
    public class CreateAccountUseCase(IAccountRepository accounts)
    {
        private readonly IAccountRepository _accounts = accounts;

        public async Task HandleAsync(CreateAccountDto request, CancellationToken ct = default)
        {
            Account? existing = await _accounts.GetByUserIdAsync(request.UserId, ct);
            if (existing != null)
            {
                return;
            }

            Account account = Account.Create(request.UserId);
            await _accounts.AddAsync(account, ct);
        }
    }

}
