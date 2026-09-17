using System.Text;

namespace Risc32.Demo;

/// <summary>Чистое форматирование листинга, пяти стадий шага и снимка процессора.</summary>
public static class TraceFormatter
{
    public static string FormatListing(ProgramImage image)
    {
        var text = new StringBuilder();
        text.AppendLine("АДРЕС      СЛОВО       ИСХОДНЫЙ ТЕКСТ");
        for (int i = 0; i < image.Words.Count; i++)
        {
            uint address = (uint)i;
            string marker = address == image.EntryPoint ? ">" : " ";
            string source = image.SourceMap.TryGetValue(address, out SourceLocation? location)
                ? location.Text.Trim() : "";
            text.AppendLine($"{marker}0x{address:X8}  0x{image.Words[i]:X8}  {source}");
        }
        return text.ToString().TrimEnd();
    }

    public static string FormatStep(StepTrace trace)
    {
        string indent = new(' ', trace.Before.CallDepth * 2);
        string source = trace.Source is null ? "" : $" | {trace.Source.Text.Trim()}";
        var text = new StringBuilder();
        text.AppendLine($"{indent}┌─ Шаг {trace.StepNumber}, глубина вызова {trace.Before.CallDepth}{source}");
        text.AppendLine($"{indent}│ FETCH     {trace.Fetch}");
        text.AppendLine($"{indent}│ DECODE    {trace.Decode}");
        text.AppendLine($"{indent}│ EXECUTE   {trace.Execute}");
        text.AppendLine($"{indent}│ MEMORY    {trace.Memory}");
        text.AppendLine($"{indent}│ WRITEBACK {trace.WriteBack}");
        text.Append($"{indent}└─ ZNCV={FormatFlags(trace.After.Flags)}, SP=0x{trace.After.Sp:X8}, PC=0x{trace.After.Pc:X8}");
        return text.ToString();
    }

    public static string FormatState(CpuState state)
    {
        var text = new StringBuilder();
        text.AppendLine($"PC=0x{state.Pc:X8}  SP=0x{state.Sp:X8}  ZNCV={FormatFlags(state.Flags)}  " +
                        $"шагов={state.Steps}  глубина={state.CallDepth}  HALT={state.Halted}");
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                int register = row * 4 + column;
                uint value = state.Registers[register];
                text.Append($"R{register,-2}=0x{value:X8} ({unchecked((int)value),11})");
                if (column < 3) text.Append("  ");
            }
            if (row < 3) text.AppendLine();
        }
        return text.ToString();
    }

    public static string FormatDump(Memory32 memory, uint start, int count)
    {
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        ulong end = (ulong)start + (uint)count;
        if (end > (ulong)memory.WordCount)
            throw new CpuException(CpuError.MemoryAccess, "Диапазон дампа выходит за пределы памяти.");
        var text = new StringBuilder();
        for (uint address = start; address < end; address++)
            text.AppendLine($"0x{address:X8}: 0x{memory.Read(address):X8} ({unchecked((int)memory.Read(address))})");
        return text.ToString().TrimEnd();
    }

    public static string FormatFlags(CpuFlags flags) => string.Concat(
        flags.HasFlag(CpuFlags.Zero) ? '1' : '0',
        flags.HasFlag(CpuFlags.Negative) ? '1' : '0',
        flags.HasFlag(CpuFlags.Carry) ? '1' : '0',
        flags.HasFlag(CpuFlags.Overflow) ? '1' : '0');
}
