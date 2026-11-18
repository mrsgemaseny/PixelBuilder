using System;

namespace MiniLife.WorldStudio.Domain
{
    // Нарушение инварианта домена. Сообщение на русском, показывается пользователю как есть.
    public sealed class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }
}
