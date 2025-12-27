using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PaymentsService.Application.Ports;
using PaymentsService.Infrastructure.Persistence.Entities;
using Npgsql;
using System.Data.Common;

namespace PaymentsService.Infrastructure.Persistence.Repositories
{
    public class PaymentInboxRepository(
        PaymentsDbContext dbContext,
        ILogger<PaymentInboxRepository> logger) : IPaymentInboxRepository
    {
        private readonly PaymentsDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly ILogger<PaymentInboxRepository> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task AddAsync(InboxMessage message, CancellationToken ct = default)
        {
            _logger.LogDebug("Adding message to inbox: {MessageId}", message.Id);

            InboxMessageDbModel dbModel = InboxMessageDbModel.FromDomain(message);

            try
            {
                _ = await _dbContext.InboxMessages.AddAsync(dbModel, ct);
                _logger.LogInformation("Message added to inbox: {MessageId}", message.Id);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogDebug("Message already exists in inbox: {MessageId}", message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding message to inbox: {MessageId}", message.Id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string messageId, CancellationToken ct = default)
        {
            _logger.LogDebug("Checking if message exists in inbox: {MessageId}", messageId);

            try
            {
                return await _dbContext.InboxMessages
                    .AsNoTracking()
                    .AnyAsync(m => m.Id == messageId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if message exists: {MessageId}", messageId);
                throw;
            }
        }

        public async Task<bool> TryAcquireAsync(
            string messageId,
            Guid orderId,
            Guid userId,
            string processorId,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Attempting to acquire message: {MessageId} for processor: {ProcessorId}",
                messageId, processorId);

            DbConnection connection = _dbContext.Database.GetDbConnection();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                }

                await using DbTransaction transaction = await connection.BeginTransactionAsync(ct);

                try
                {
                    string lockSql = @"
                        SELECT id, status, version 
                        FROM inbox_messages 
                        WHERE id = @messageId 
                          AND status = 'Pending'
                        FOR UPDATE SKIP LOCKED";

                    var current = await _dbContext.InboxMessages
                        .FromSqlRaw(lockSql, new NpgsqlParameter("messageId", messageId))
                        .Select(m => new { m.Id, m.Status, m.Version })
                        .FirstOrDefaultAsync(ct);

                    if (current == null)
                    {
                        await transaction.RollbackAsync(ct);
                        _logger.LogDebug("Message not available for acquisition: {MessageId}", messageId);
                        return false;
                    }

                    string updateSql = @"
                        UPDATE inbox_messages 
                        SET status = 'Processing',
                            processor_id = @processorId,
                            locked_at = @lockedAt,
                            version = version + 1
                        WHERE id = @messageId 
                          AND status = 'Pending'
                          AND version = @currentVersion";

                    NpgsqlParameter[] parameters =
                    [
                        new NpgsqlParameter("messageId", messageId),
                        new NpgsqlParameter("processorId", processorId),
                        new NpgsqlParameter("lockedAt", DateTimeOffset.UtcNow),
                        new NpgsqlParameter("currentVersion", current.Version)
                    ];

                    int rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(
                        updateSql, parameters);

                    if (rowsAffected > 0)
                    {
                        await transaction.CommitAsync(ct);
                        _logger.LogInformation("Message acquired: {MessageId} by processor: {ProcessorId}",
                            messageId, processorId);
                        return true;
                    }
                    else
                    {
                        await transaction.RollbackAsync(ct);
                        _logger.LogDebug("Failed to acquire message (concurrent update): {MessageId}", messageId);
                        return false;
                    }
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error acquiring message: {MessageId}", messageId);
                throw;
            }
        }

        public async Task<bool> TryAcquireAsync(
            string messageId,
            Guid orderId,
            Guid userId,
            CancellationToken ct)
        {
            return await TryAcquireAsync(messageId, orderId, userId, "default-processor", ct);
        }

        public async Task ReleaseAsync(string messageId, CancellationToken ct)
        {
            _logger.LogDebug("Releasing message: {MessageId}", messageId);

            try
            {
                int rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(@"
                    UPDATE inbox_messages 
                    SET status = 'Pending',
                        processor_id = NULL,
                        locked_at = NULL,
                        retry_count = retry_count + 1,
                        version = version + 1
                    WHERE id = {0} 
                      AND status = 'Processing'",
                    messageId);

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Message released: {MessageId}", messageId);
                }
                else
                {
                    _logger.LogDebug("Message not found or not in Processing state: {MessageId}", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing message: {MessageId}", messageId);
                throw;
            }
        }

        public async Task MarkAsProcessedAsync(string messageId, CancellationToken ct)
        {
            _logger.LogDebug("Marking message as processed: {MessageId}", messageId);

            try
            {
                int rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(@"
                    UPDATE inbox_messages 
                    SET status = 'Processed',
                        processor_id = NULL,
                        locked_at = NULL,
                        processed_at = {1},
                        version = version + 1
                    WHERE id = {0} 
                      AND status = 'Processing'",
                    messageId, DateTimeOffset.UtcNow);

                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Message marked as processed: {MessageId}", messageId);
                }
                else
                {
                    _logger.LogWarning("Message not found or not in Processing state: {MessageId}", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as processed: {MessageId}", messageId);
                throw;
            }
        }

        public async Task<List<InboxMessage>> GetPendingMessagesAsync(
            int batchSize = 50,
            CancellationToken ct = default)
        {
            _logger.LogDebug("Getting pending messages, batch size: {BatchSize}", batchSize);

            try
            {
                List<InboxMessage> messages = await _dbContext.InboxMessages
                    .Where(m => m.Status == "Pending")
                    .OrderBy(m => m.CreatedAt)
                    .ThenBy(m => m.RetryCount)
                    .Take(batchSize)
                    .AsNoTracking()
                    .Select(m => m.ToDomainEntity())
                    .ToListAsync(ct);

                _logger.LogDebug("Found {Count} pending messages", messages.Count);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending messages");
                throw;
            }
        }

        public async Task<IEnumerable<InboxMessage>> GetStuckMessagesAsync(
            TimeSpan olderThan,
            CancellationToken ct)
        {
            _logger.LogDebug("Getting stuck messages older than {OlderThan}", olderThan);

            try
            {
                DateTimeOffset cutoff = DateTimeOffset.UtcNow - olderThan;

                List<InboxMessage> messages = await _dbContext.InboxMessages
                    .Where(m => m.Status == "Processing" && m.LockedAt < cutoff)
                    .OrderBy(m => m.LockedAt)
                    .AsNoTracking()
                    .Select(m => m.ToDomainEntity())
                    .ToListAsync(ct);

                _logger.LogDebug("Found {Count} stuck messages", messages.Count);
                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stuck messages");
                throw;
            }
        }

        public async Task IncrementRetryCountAsync(string messageId, CancellationToken ct)
        {
            _logger.LogDebug("Incrementing retry count for message: {MessageId}", messageId);

            try
            {
                _ = await _dbContext.Database.ExecuteSqlRawAsync(@"
                    UPDATE inbox_messages 
                    SET retry_count = retry_count + 1,
                        version = version + 1
                    WHERE id = {0}",
                    messageId);

                _logger.LogDebug("Retry count incremented for message: {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing retry count for message: {MessageId}", messageId);
                throw;
            }
        }

        public async Task MarkAsFailedAsync(string messageId, string error, CancellationToken ct)
        {
            _logger.LogDebug("Marking message as failed: {MessageId}, error: {Error}",
                messageId, error);

            try
            {
                _ = await _dbContext.Database.ExecuteSqlRawAsync(@"
                    UPDATE inbox_messages 
                    SET status = 
                        CASE 
                            WHEN retry_count >= 3 THEN 'DeadLetter'
                            ELSE 'Failed'
                        END,
                        processor_id = NULL,
                        locked_at = NULL,
                        error_message = {1},
                        version = version + 1
                    WHERE id = {0} 
                      AND status IN ('Pending', 'Processing')",
                    messageId, error);

                _logger.LogInformation("Message marked as failed: {MessageId}", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking message as failed: {MessageId}", messageId);
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