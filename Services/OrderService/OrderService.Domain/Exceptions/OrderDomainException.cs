using System;

namespace OrderService.Domain.Exceptions
{
    public class OrderDomainException(string message) : Exception(message)
    {
    }

    public class OrderNotFoundException(Guid orderId) : OrderDomainException($"Заказ с id '{orderId}' не был найден.")
    {
    }

}
