using Xunit.v3;

namespace Tranquility.AcceptanceTests.Traceability;

/// <summary>
/// Binds an acceptance test to one requirement ID from <c>docs/specs</c>
/// (e.g. <c>L2-CMD-001</c>). Emitted as an xunit trait named
/// <c>Requirement</c> so runs are filterable per requirement and the trace
/// matrix can be regenerated from trx output.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirementAttribute(string id) : Attribute, ITraitAttribute
{
    public string Id { get; } = id;

    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new("Requirement", Id)];
}
