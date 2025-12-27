using PaymentsService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace PaymentsService.Application.Ports
{
    public interface IWithdrawalRepository
    {
        Task AddAsync(Withdrawal withdrawal, CancellationToken ct = default);
        Task<Withdrawal?> GetByPaymentIdAsync(Guid paymentId, CancellationToken ct = default);
    }
}
