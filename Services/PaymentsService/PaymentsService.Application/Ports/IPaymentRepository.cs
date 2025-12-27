using PaymentsService.Domain.Entities;

namespace PaymentsService.Application.Ports
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
        Task AddAsync(Payment payment, CancellationToken ct = default);
        Task<bool> TryUpdateWithVersionAsync(Payment payment, int expectedVersion, CancellationToken ct = default);
    }

}
