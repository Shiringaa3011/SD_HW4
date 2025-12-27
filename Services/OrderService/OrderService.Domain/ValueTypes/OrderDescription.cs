using System;

namespace OrderService.Domain.ValueTypes
{
    public record OrderDescription
    {
        public string Value { get; }

        private OrderDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Описание не может быть пустым", nameof(value));
            }

            if (value.Length > 500)
            {
                throw new ArgumentException("Описание не может превышать 500 символов", nameof(value));
            }

            Value = value.Trim();
        }

        public static OrderDescription Create(string description)
        {
            return new OrderDescription(description);
        }
    }
}
