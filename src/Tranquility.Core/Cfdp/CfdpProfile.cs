namespace Tranquility.Core.Cfdp;

/// <summary>CFDP service classes in the baseline profile.</summary>
public enum CfdpClass
{
    Unacknowledged = 1,
    Acknowledged = 2,
}

/// <summary>
/// The declared baseline CFDP profile TRQ-CFDP-BP1 (L2-FDP-004, TBD-008).
/// Referenced by every CFDP conformance test.
/// </summary>
public sealed record CfdpProfile(
    string Id = "TRQ-CFDP-BP1",
    int FileSegmentSize = 1024,
    int EntityIdOctets = 2,
    int TransactionSeqOctets = 4,
    int AckLimit = 3,
    int NakLimit = 3)
{
    public static readonly CfdpProfile Baseline = new();
}
