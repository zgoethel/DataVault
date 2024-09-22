namespace DataVault.Core.Algorithm;

public enum RestoreResult
{
    // Data and/or parity stripes were successfully restored
    Success,
    // Not enough stripes survive to reconstruct the data
    NotEnoughData,
    // Validation was enabled and found disagreement in the stripes
    //Invalid
}
