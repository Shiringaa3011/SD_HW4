using PaymentsService.Domain.Entities;

namespace PaymentsService.Application.Ports
{
    public interface IAccountRepository
    {
        Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

        Task AddAsync(Account account, CancellationToken ct = default);

        Task<bool> TryUpdateWithVersionAsync(
            Account account,
            int expectedVersion,
            CancellationToken ct = default);

        Task<(Account?, int)> GetByUserIdWithVersionAsync(Guid userId, CancellationToken ct = default);
    }

}
