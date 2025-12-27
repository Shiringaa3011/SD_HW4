using System;

namespace PaymentsService.Domain.Exceptions
{
    public class PaymentDomainException(string message) : Exception(message)
    {
    }

    public class AccountNotFoundException(Guid userId) : PaymentDomainException($"Account for user '{userId}' was not found.")
    {
    }

}
