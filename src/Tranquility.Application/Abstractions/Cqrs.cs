namespace Tranquility.Application.Abstractions;

/// <summary>
/// Marker and handler contracts for the CQRS query/command split (L2-QLT-006).
/// Mutations flow exclusively through <see cref="ICommandHandler{TCommand, TResult}"/>,
/// retrievals through <see cref="IQueryHandler{TQuery, TResult}"/>; the
/// architecture-conformance acceptance test enforces the separation.
/// </summary>
public interface IQuery<TResult>;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}

public interface ICommand<TResult>;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Dispatches commands to their registered handler (mutation path).</summary>
public interface ICommandDispatcher
{
    Task<TResult> Dispatch<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);
}

/// <summary>Dispatches queries to their registered handler (retrieval path).</summary>
public interface IQueryDispatcher
{
    Task<TResult> Dispatch<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}

/// <summary>
/// Service-provider-backed dispatchers. Handlers are resolved by their closed
/// generic handler interface, so registration stays conventional DI.
/// </summary>
public sealed class Dispatcher(IServiceProvider services) : ICommandDispatcher, IQueryDispatcher
{
    Task<TResult> ICommandDispatcher.Dispatch<TResult>(ICommand<TResult> command, CancellationToken cancellationToken) =>
        Invoke<TResult>(typeof(ICommandHandler<,>), command, cancellationToken);

    Task<TResult> IQueryDispatcher.Dispatch<TResult>(IQuery<TResult> query, CancellationToken cancellationToken) =>
        Invoke<TResult>(typeof(IQueryHandler<,>), query, cancellationToken);

    private Task<TResult> Invoke<TResult>(Type openHandlerType, object message, CancellationToken cancellationToken)
    {
        var handlerType = openHandlerType.MakeGenericType(message.GetType(), typeof(TResult));
        var handler = services.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for {message.GetType().Name}.");
        var method = handlerType.GetMethod("Handle")!;
        return (Task<TResult>)method.Invoke(handler, [message, cancellationToken])!;
    }
}
