namespace Raphael;

public readonly record struct SolverInput
{
    public short CP { get; init; }
    public sbyte Durability { get; init; }
    public ushort Progress { get; init; }
    public ushort Quality { get; init; }
    public ushort BaseProgressGain { get; init; }
    public ushort BaseQualityGain { get; init; }
    public byte JobLevel { get; init; }
}
