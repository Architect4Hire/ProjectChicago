namespace ProjectChicago.Shared.Messaging;

// The envelope JSON was absent/malformed, or its payload did not bind to the requested contract
// CLR type. Per messaging.md failure semantics this is a poison-message condition - callers should
// not retry indefinitely, and should route to dead-letter/poison handling instead.
public sealed class EnvelopeDeserializationException : Exception
{
    public EnvelopeDeserializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
