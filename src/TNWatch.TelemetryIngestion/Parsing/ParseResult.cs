namespace TNWatch.TelemetryIngestion.Parsing;

/// <summary>Result of parsing/validating a message: either a value or a rejection reason.</summary>
public readonly record struct ParseResult<T>(T? Value, string? Error)
    where T : class
{
    public bool IsSuccess => Value is not null;

    public static ParseResult<T> Ok(T value) => new(value, null);

    public static ParseResult<T> Fail(string error) => new(null, error);
}
