namespace Raphael;

public readonly record struct SolverConfig
{
    public bool Adversarial { get; init; }
    public bool BackloadProgress { get; init; }
    public bool UnsoundBranchPruning { get; init; }
    public LevelFilter LogLevel { get; init; }
}
