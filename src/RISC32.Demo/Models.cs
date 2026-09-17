using System.Collections.ObjectModel;

namespace Risc32.Demo;

public sealed record SourceLocation(string SourceName, int LineNumber, string Text);

/// <summary>Результат ассемблирования с данными для листинга и исходной трассы.</summary>
public sealed class ProgramImage
{
    public IReadOnlyList<uint> Words { get; }
    public uint EntryPoint { get; }
    public IReadOnlyDictionary<string, uint> Symbols { get; }
    public IReadOnlyDictionary<uint, SourceLocation> SourceMap { get; }

    public ProgramImage(IEnumerable<uint> words, uint entryPoint,
        IDictionary<string, uint>? symbols = null, IDictionary<uint, SourceLocation>? sourceMap = null)
    {
        Words = Array.AsReadOnly(words.ToArray());
        EntryPoint = entryPoint;
        Symbols = new ReadOnlyDictionary<string, uint>(
            new Dictionary<string, uint>(symbols ?? new Dictionary<string, uint>(), StringComparer.OrdinalIgnoreCase));
        SourceMap = new ReadOnlyDictionary<uint, SourceLocation>(
            new Dictionary<uint, SourceLocation>(sourceMap ?? new Dictionary<uint, SourceLocation>()));
    }
}

public sealed record CpuState(
    IReadOnlyList<uint> Registers, uint Pc, uint Sp, CpuFlags Flags,
    bool Halted, int Steps, int CallDepth);

public sealed record StepTrace(
    int StepNumber, uint RawInstruction, Instruction32 Instruction, SourceLocation? Source,
    CpuState Before, CpuState After, string Fetch, string Decode, string Execute,
    string Memory, string WriteBack);

public sealed record RunResult(CpuState State, int ExecutedSteps);
