using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentsService.Application.Ports;
using PaymentsService.Domain.Entities;
using PaymentsService.Infrastructure.Persistence.Entities;
using Npgsql;

namespace PaymentsService.Infrastructure.Persistence.Repositories
{
    public class AccountRepository(
        PaymentsDbContext dbContext,
        ILogger<AccountRepository> logger) : IAccountRepository
    {
        private readonly PaymentsDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly ILogger<AccountRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            _logger.LogDebug("Getting account for user {UserId}", userId);

            try
            {
                AccountDbModel? dbModel = await _dbContext.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.UserId == userId, ct);

                if (dbModel == null)
                {
                    _logger.LogDebug("Account not found for user {UserId}", userId);
                    return null;
                }

                return dbModel.ToDomainEntity();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account for user {UserId}", userId);
                throw;
            }
        }

        public async Task<(Account?, int)> GetByUserIdWithVersionAsync(Guid userId, CancellationToken ct = default)
        {
            if (userId == Guid.Empty)
            {
                _logger.LogWarning("Empty userId passed to GetByUserIdWithVersionAsync");
                return (null, 0);
            }

            _logger.LogDebug("Getting account with version for user {UserId}", userId);

            try
            {
                AccountDbModel? dbModel = await _dbContext.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == userId, ct);

                if (dbModel == null)
                {
                    _logger.LogDebug("Account not found for user {UserId}", userId);
                    return (null, 0);
                }

                _logger.LogDebug("Found account for user {UserId}: Balance={Balance}, Version={Version}",
                    userId, dbModel.BalanceAmount, dbModel.Version);

                Account account = dbModel.ToDomainEntity();
                return (account, dbModel.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account with version for user {UserId}", userId);
                throw;
            }
        }

        public async Task AddAsync(Account account, CancellationToken ct = default)
        {
            _logger.LogDebug("Adding account for user {UserId}", account.UserId);

            AccountDbModel dbModel = AccountDbModel.FromDomain(account);

            try
            {
                _ = await _dbContext.Accounts.AddAsync(dbModel, ct);
                _logger.LogInformation("Account added for user {UserId}", account.UserId);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogWarning("Account already exists for user {UserId}", account.UserId);
                throw new InvalidOperationException($"Account already exists for user {account.UserId}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding account for user {UserId}", account.UserId);
                throw;
            }
        }

        public async Task<bool> TryUpdateWithVersionAsync(
            Account account,
            int expectedVersion,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Attempting to update account for user {UserId} with expected version {ExpectedVersion}",
                account.UserId, expectedVersion);

            try
            {
                AccountDbModel? dbModel = await _dbContext.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == account.UserId, ct);

                if (dbModel == null)
                {
                    _logger.LogWarning("Account not found for user {UserId}", account.UserId);
                    return false;
                }

                if (dbModel.Version != expectedVersion)
                {
                    _logger.LogWarning(
                        "Version mismatch when updating account for user {UserId}. " +
                        "Expected: {ExpectedVersion}, Actual: {ActualVersion}",
                        account.UserId, expectedVersion, dbModel.Version);
                    return false;
                }

                dbModel.BalanceAmount = account.Balance.Amount;
                dbModel.BalanceCurrency = account.Balance.Currency;
                dbModel.Version = expectedVersion + 1;
                dbModel.UpdatedAt = DateTimeOffset.UtcNow;

                int affectedRows = await _dbContext.SaveChangesAsync(ct);

                bool success = affectedRows > 0;

                if (success)
                {
                    _logger.LogDebug("Account updated successfully for user {UserId}. New version: {NewVersion}",
                        account.UserId, dbModel.Version);
                }

                return success;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict when updating account for user {UserId}", account.UserId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating account for user {UserId}", account.UserId);
                throw;
            }
        }

        private bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException is PostgresException pgEx &&
                   pgEx.SqlState == PostgresErrorCodes.UniqueViolation;
        }
    }
}