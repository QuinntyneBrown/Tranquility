namespace Tranquility.Application.Abstractions;

/// <summary>
/// Application-level failures carrying the documented wire exception type
/// (L2-API-004). The server envelope middleware maps these one-to-one onto
/// <c>{"exception":{"type","msg"}}</c> with the corresponding HTTP status.
/// </summary>
public abstract class ServiceException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }

    public abstract string WireType { get; }
}

public sealed class NotFoundServiceException(string message) : ServiceException(message)
{
    public override int StatusCode => 404;

    public override string WireType => "NotFoundException";
}

public sealed class BadRequestServiceException(string message) : ServiceException(message)
{
    public override int StatusCode => 400;

    public override string WireType => "BadRequestException";
}

public sealed class ConflictServiceException(string message) : ServiceException(message)
{
    public override int StatusCode => 409;

    public override string WireType => "ConflictException";
}

public sealed class UnauthorizedServiceException(string message) : ServiceException(message)
{
    public override int StatusCode => 401;

    public override string WireType => "UnauthorizedException";
}

public sealed class ForbiddenServiceException(string message) : ServiceException(message)
{
    public override int StatusCode => 403;

    public override string WireType => "ForbiddenException";
}
