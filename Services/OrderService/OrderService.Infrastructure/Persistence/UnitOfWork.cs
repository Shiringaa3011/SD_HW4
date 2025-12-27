using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using OrderService.Application.Ports;

namespace OrderService.Infrastructure.Persistence
{
    public class UnitOfWork(OrderDbContext context, ILogger<UnitOfWork> logger) : IUnitOfWork
    {
        private readonly OrderDbContext _context = context;
        private readonly ILogger<UnitOfWork> _logger = logger;
        private IDbContextTransaction? _transaction;

        public async Task BeginTransactionAsync(CancellationToken ct)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("Transaction already started");
            }

            _transaction = await _context.Database.BeginTransactionAsync(ct);
            _logger.LogDebug("Transaction started");
        }

        public async Task CommitTransactionAsync(CancellationToken ct)
        {
            if (_transaction == null)
            {
                throw new InvalidOperationException("No active transaction");
            }

            try
            {
                _ = await _context.SaveChangesAsync(ct);
                await _transaction.CommitAsync(ct);
                _logger.LogDebug("Transaction committed");
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken ct)
        {
            if (_transaction == null)
            {
                return;
            }

            try
            {
                await _transaction.RollbackAsync(ct);
                _logger.LogDebug("Transaction rolled back");
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }
}