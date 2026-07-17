using Tranquility.AcceptanceTests.Traceability;
using Tranquility.Application.Abstractions;
using Xunit;

namespace Tranquility.AcceptanceTests.Quality;

/// <summary>
/// L2-QLT-006: GIVEN architecture conformance review WHEN service interfaces
/// are inspected THEN mutation paths and retrieval paths are separated by
/// interface contract. Mechanical reflection over the Application assembly —
/// no exemptions.
/// </summary>
[Requirement("L2-QLT-006")]
public sealed class CqrsSeparationTests
{
    private static readonly Type[] ApplicationTypes = typeof(ICommandDispatcher).Assembly.GetTypes();

    [Fact]
    public void Mutation_and_retrieval_paths_exist_as_separate_interface_contracts()
    {
        Assert.True(HandlerImplementations(typeof(ICommandHandler<,>)).Count > 0,
            "At least one ICommandHandler implementation must exist (mutation path).");
        Assert.True(HandlerImplementations(typeof(IQueryHandler<,>)).Count > 0,
            "At least one IQueryHandler implementation must exist (retrieval path).");
    }

    [Fact]
    public void No_type_implements_both_command_and_query_handling()
    {
        var offenders = ApplicationTypes
            .Where(t => Implements(t, typeof(ICommandHandler<,>)) && Implements(t, typeof(IQueryHandler<,>)))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(offenders.Count == 0,
            "Types implementing both ICommandHandler and IQueryHandler violate the CQRS split: " +
            string.Join(", ", offenders));
    }

    [Fact]
    public void Every_handler_lives_in_the_application_layer()
    {
        var stray = HandlerImplementations(typeof(ICommandHandler<,>))
            .Concat(HandlerImplementations(typeof(IQueryHandler<,>)))
            .Where(t => !t.Namespace!.StartsWith("Tranquility.Application", StringComparison.Ordinal))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(stray.Count == 0,
            "Handlers outside Tranquility.Application: " + string.Join(", ", stray));
    }

    private static List<Type> HandlerImplementations(Type openInterface) =>
        ApplicationTypes.Where(t => t is { IsAbstract: false, IsInterface: false } && Implements(t, openInterface)).ToList();

    private static bool Implements(Type type, Type openInterface) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);
}
