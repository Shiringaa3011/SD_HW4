using Microsoft.Extensions.Logging;
using PaymentsService.Application.Dtos;
using PaymentsService.Application.Ports;
using PaymentsService.Domain.Entities;
using PaymentsService.Domain.Enums;
using PaymentsService.Domain.Exceptions;
using PaymentsService.Domain.ValueTypes;
using System.Data;
using System.Text.Json;

namespace PaymentsService.Application.UseCases
{
    public class ProcessPaymentUseCase(
        IAccountRepository accounts,
        IPaymentRepository payments,
        IPaymentInboxRepository inbox,
        IPaymentOutboxRepository outbox,
        IWithdrawalRepository withdrawals,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPaymentUseCase>? logger = null)
    {
        private readonly IAccountRepository _accounts = accounts;
        private readonly IPaymentRepository _payments = payments;
        private readonly IPaymentInboxRepository _inbox = inbox;
        private readonly IPaymentOutboxRepository _outbox = outbox;
        private readonly IWithdrawalRepository _withdrawals = withdrawals; // Для идемпотентности списаний
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly ILogger<ProcessPaymentUseCase>? _logger = logger;

        public async Task HandleAsync(PaymentCommandDto command, CancellationToken ct = default)
        {
            _logger?.LogInformation("Starting payment processing for order {OrderId}, message {MessageId}",
                command.OrderId, command.MessageId);

            try
            {
                ValidateCommand(command);

                Payment? existingPayment = await _payments.GetByOrderIdAsync(command.OrderId, ct);

                if (existingPayment != null && existingPayment.Status == PaymentStatus.Success)
                {
                    _logger?.LogInformation("Order {OrderId} already paid. PaymentId: {PaymentId}",
                        command.OrderId, existingPayment.Id);

                    await SendPaymentResultAsync(
                        messageId: command.MessageId,
                        orderId: command.OrderId,
                        userId: command.UserId,
                        success: true,
                        reason: "Already paid",
                        ct);
                    return;
                }

                await _unitOfWork.BeginTransactionAsync(ct);
                try
                {

                    Payment payment = existingPayment ?? Payment.Create(
                        command.OrderId,
                        command.UserId,
                        Money.Create(command.Amount, command.Currency));
                    Account account = await _accounts.GetByUserIdAsync(command.UserId, ct)
                        ?? throw new AccountNotFoundException(command.UserId);

                    int originalPaymentVersion = payment.Version;
                    int originalAccountVersion = account.Version;

                    (bool withdrawSuccess, Guid? withdrawalId) = await TryWithdrawIdempotentlyAsync(
                        account: account,
                        paymentId: payment.Id,
                        amount: Money.Create(command.Amount, command.Currency),
                        ct);

                    if (withdrawSuccess)
                    {
                        payment.MarkSuccess();
                        _logger?.LogInformation("Payment {PaymentId} succeeded. Withdrawal: {WithdrawalId}",
                            payment.Id, withdrawalId);
                    }
                    else
                    {
                        payment.MarkFailed();
                        _logger?.LogWarning("Payment {PaymentId} failed. Withdrawal: {WithdrawalId}",
                            payment.Id, withdrawalId);
                    }

                    if (existingPayment == null)
                    {
                        await _payments.AddAsync(payment, ct);
                    }
                    else
                    {
                        bool paymentUpdated = await _payments.TryUpdateWithVersionAsync(
                            payment,
                            expectedVersion: originalPaymentVersion,
                            ct);

                        if (!paymentUpdated)
                        {
                            throw new Exception($"Payment {payment.Id} was modified concurrently");
                        }
                    }

                    bool accountUpdated = await _accounts.TryUpdateWithVersionAsync(
                        account,
                        expectedVersion: originalAccountVersion,
                        ct);

                    if (!accountUpdated)
                    {
                        throw new Exception($"Account for user {command.UserId} was modified concurrently");
                    }

                    await _outbox.AddAsync(new OutboxMessage(
                        messageId: Guid.NewGuid(),
                        correlationId: command.MessageId,
                        type: "PaymentProcessed",
                        body: JsonSerializer.Serialize(new PaymentResultDto(
                            command.MessageId,
                            payment.OrderId,
                            payment.UserId,
                            Success: withdrawSuccess,
                            Reason: withdrawSuccess ? null : "Insufficient funds")),
                        topic: "payment-results",
                        createdAt: DateTimeOffset.UtcNow), ct);

                    await _unitOfWork.CommitTransactionAsync(ct);

                    _logger?.LogInformation("Payment processing completed successfully for order {OrderId}",
                        command.OrderId);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Payment processing failed for order {OrderId}. Releasing inbox lock.",
                        command.OrderId);

                    await _inbox.ReleaseAsync(command.MessageId, ct);
                    await _unitOfWork.RollbackTransactionAsync(ct);
                    throw;
                }
            }
            catch (Exception ex) when (ex is not AccountNotFoundException)
            {
                _logger?.LogError(ex, "Unexpected error processing payment for order {OrderId}", command.OrderId);
                throw;
            }
        }

        private void ValidateCommand(PaymentCommandDto command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (string.IsNullOrEmpty(command.MessageId))
            {
                throw new ArgumentException("MessageId is required", nameof(command.MessageId));
            }

            if (command.OrderId == Guid.Empty)
            {
                throw new ArgumentException("OrderId is required", nameof(command.OrderId));
            }

            if (command.UserId == Guid.Empty)
            {
                throw new ArgumentException("UserId is required", nameof(command.UserId));
            }

            if (command.Amount <= 0)
            {
                throw new ArgumentException("Amount must be positive", nameof(command.Amount));
            }
        }

        private async Task<(bool success, Guid? withdrawalId)> TryWithdrawIdempotentlyAsync(
            Account account,
            Guid paymentId,
            Money amount,
            CancellationToken ct)
        {
            Withdrawal? existingWithdrawal = await _withdrawals.GetByPaymentIdAsync(paymentId, ct);

            if (existingWithdrawal != null)
            {
                _logger?.LogInformation("Found existing withdrawal for payment {PaymentId}: Success={Success}",
                    paymentId, existingWithdrawal.Success);
                return (existingWithdrawal.Success, existingWithdrawal.Id);
            }

            try
            {
                account.Withdraw(amount);

                Withdrawal withdrawal = Withdrawal.Record(paymentId, amount, success: true);
                await _withdrawals.AddAsync(withdrawal, ct);

                return (true, withdrawal.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Insufficient funds for payment {PaymentId}", paymentId);

                Withdrawal withdrawal = Withdrawal.Record(paymentId, amount, success: false);
                await _withdrawals.AddAsync(withdrawal, ct);

                return (false, withdrawal.Id);
            }
        }

        private async Task SendPaymentResultAsync(
            string messageId,
            Guid orderId,
            Guid userId,
            bool success,
            string reason,
            CancellationToken ct)
        {
            OutboxMessage? existingResult = await _outbox.FindByMessageIdAsync(messageId, ct);
            if (existingResult != null)
            {
                _logger?.LogDebug("Result for message {MessageId} already sent", messageId);
                return;
            }

            await _outbox.AddAsync(new OutboxMessage(
                messageId: Guid.NewGuid(),
                correlationId: messageId,
                type: "PaymentProcessed",
                body: JsonSerializer.Serialize(new PaymentResultDto(
                    messageId,
                    orderId,
                    userId,
                    Success: success,
                    Reason: success ? null : reason)),
                topic: "payment-results",
                createdAt: DateTimeOffset.UtcNow), ct);

            _logger?.LogInformation("Sent payment result for message {MessageId}. Success: {Success}",
                messageId, success);
        }
    }
}