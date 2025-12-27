using System;
using System.Collections.Generic;
using System.Text;

namespace OrderService.Application.Ports
{
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync(CancellationToken ct);

        Task CommitTransactionAsync(CancellationToken ct);

        Task RollbackTransactionAsync(CancellationToken ct);
    }
}
