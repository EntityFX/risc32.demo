using System.Globalization;
using System.Text.RegularExpressions;

namespace Risc32.Demo;

/// <summary>Небольшой двухпроходный ассемблер: первый проход собирает метки, второй кодирует слова.</summary>
public sealed class Assembler32
{
    private static readonly Regex LabelPattern = new(
        "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly Regex MemoryPattern = new(
        @"^\[\s*(R(?:1[0-5]|[0-9]))(?:\s*([+-])\s*(.+?))?\s*\]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public ProgramImage Assemble(string source, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        string name = string.IsNullOrWhiteSpace(sourceName) ? "<memory>" : sourceName;
        string[] sourceLines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var symbols = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var parsed = new List<ParsedLine>();
        ParsedLine? entryDirective = null;
        uint address = 0;

        for (int i = 0; i < sourceLines.Length; i++)
        {
            string original = sourceLines[i];
            string code = StripComment(original).Trim();
            if (code.Length == 0) continue;

            int colon = code.IndexOf(':');
            if (colon >= 0)
            {
                string label = code[..colon].Trim();
                RequireLabel(label, name, i + 1, original);
                if (!symbols.TryAdd(label, address))
                    Fail($"Метка '{label}' определена повторно.", name, i + 1, original);
                code = code[(colon + 1)..].Trim();
                if (code.Length == 0) continue;
            }

            var line = new ParsedLine(address, i + 1, original, code);
            if (FirstToken(code).Equals(".entry", StringComparison.OrdinalIgnoreCase))
            {
                if (entryDirective is not null)
                    Fail("Директива .entry указана повторно.", name, i + 1, original);
                entryDirective = line;
                continue;
            }

            parsed.Add(line);
            if (address == uint.MaxValue) Fail("Программа содержит слишком много слов.", name, i + 1, original);
            address++;
        }

        if (parsed.Count == 0) Fail("Исходный текст не содержит команд или данных.", name, 1, "");

        var words = new uint[parsed.Count];
        var sourceMap = new Dictionary<uint, SourceLocation>();
        foreach (ParsedLine line in parsed)
        {
            try
            {
                words[(int)line.Address] = EncodeLine(line, symbols);
                sourceMap[line.Address] = new SourceLocation(name, line.Number, line.Original);
            }
            catch (CpuException ex) when (ex.Kind != CpuError.Assembly)
            {
                Fail(ex.Message, name, line.Number, line.Original);
            }
            catch (FormatException ex)
            {
                Fail(ex.Message, name, line.Number, line.Original);
            }
        }

        uint entry = entryDirective is null ? 0 : ResolveEntry(entryDirective, symbols, name);
        if (entry >= words.Length)
            Fail($"Точка входа 0x{entry:X8} не указывает на слово программы.", name,
                entryDirective?.Number ?? 1, entryDirective?.Original ?? "");
        return new ProgramImage(words, entry, symbols, sourceMap);
    }

    private static uint EncodeLine(ParsedLine line, IReadOnlyDictionary<string, uint> symbols)
    {
        (string mnemonic, string rest) = SplitFirst(line.Statement);
        string op = mnemonic.ToUpperInvariant();
        string[] args = SplitOperands(rest);

        if (op == ".WORD")
        {
            Count(args, 1, op);
            long value = Resolve(args[0], symbols, true);
            if (value < int.MinValue || value > uint.MaxValue)
                throw new FormatException($"Значение .word {value} вне 32-битного диапазона.");
            return unchecked((uint)value);
        }

        return op switch
        {
            "NOP" => Simple(Opcode.Nop, args),
            "HALT" => Simple(Opcode.Halt, args),
            "RET" => Simple(Opcode.Ret, args),
            "LDI" => U(Opcode.Ldi, args, symbols),
            "MOV" => UnaryR(Opcode.Mov, args),
            "LOAD" => Memory(Opcode.Load, args),
            "STORE" => Memory(Opcode.Store, args),
            "PUSH" => S(Opcode.Push, args),
            "POP" => S(Opcode.Pop, args),
            "ADD" => R(Opcode.Add, args),
            "SUB" => R(Opcode.Sub, args),
            "MUL" => R(Opcode.Mul, args),
            "DIV" => R(Opcode.Div, args),
            "MOD" => R(Opcode.Mod, args),
            "AND" => R(Opcode.And, args),
            "OR" => R(Opcode.Or, args),
            "XOR" => R(Opcode.Xor, args),
            "NOT" => UnaryR(Opcode.Not, args),
            "SHL" => R(Opcode.Shl, args),
            "SHR" => R(Opcode.Shr, args),
            "SAR" => R(Opcode.Sar, args),
            "CMP" => Cmp(args),
            "JMP" => J(Opcode.Jmp, args, line.Address, symbols),
            "JZ" => J(Opcode.Jz, args, line.Address, symbols),
            "JNZ" => J(Opcode.Jnz, args, line.Address, symbols),
            "JL" => J(Opcode.Jl, args, line.Address, symbols),
            "JLE" => J(Opcode.Jle, args, line.Address, symbols),
            "JG" => J(Opcode.Jg, args, line.Address, symbols),
            "JGE" => J(Opcode.Jge, args, line.Address, symbols),
            "CALL" => J(Opcode.Call, args, line.Address, symbols),
            _ => throw new FormatException($"Неизвестная команда или директива '{mnemonic}'."),
        };
    }

    private static uint Simple(Opcode op, string[] args)
    {
        Count(args, 0, op.ToString());
        return Instruction32.EncodeSimple(op);
    }

    private static uint R(Opcode op, string[] args)
    {
        Count(args, 3, op.ToString());
        return Instruction32.EncodeR(op, Register(args[0]), Register(args[1]), Register(args[2]));
    }

    private static uint UnaryR(Opcode op, string[] args)
    {
        Count(args, 2, op.ToString());
        return Instruction32.EncodeR(op, Register(args[0]), Register(args[1]));
    }

    private static uint Cmp(string[] args)
    {
        Count(args, 2, "CMP");
        return Instruction32.EncodeR(Opcode.Cmp, Register(args[0]), Register(args[1]));
    }

    private static uint U(Opcode op, string[] args, IReadOnlyDictionary<string, uint> symbols)
    {
        Count(args, 2, op.ToString());
        long value = Resolve(args[1], symbols, true);
        if (value < -524_288 || value > 524_287)
            throw new FormatException($"Значение {value} не помещается в знаковое поле 20 бит.");
        return Instruction32.EncodeU(op, Register(args[0]), (int)value);
    }

    private static uint Memory(Opcode op, string[] args)
    {
        Count(args, 2, op.ToString());
        (int baseRegister, int offset) = MemoryOperand(args[1]);
        return Instruction32.EncodeI(op, Register(args[0]), baseRegister, offset);
    }

    private static uint S(Opcode op, string[] args)
    {
        Count(args, 1, op.ToString());
        return Instruction32.EncodeS(op, Register(args[0]));
    }

    private static uint J(Opcode op, string[] args, uint address, IReadOnlyDictionary<string, uint> symbols)
    {
        Count(args, 1, op.ToString());
        long offset = symbols.TryGetValue(args[0], out uint target)
            ? (long)target - (address + 1L)
            : Resolve(args[0], symbols, false);
        if (offset < -8_388_608 || offset > 8_388_607)
            throw new FormatException($"Смещение перехода {offset} не помещается в 24 бита.");
        return Instruction32.EncodeJ(op, (int)offset);
    }

    private static (int Register, int Offset) MemoryOperand(string text)
    {
        Match match = MemoryPattern.Match(text);
        if (!match.Success)
            throw new FormatException($"Ожидался адрес вида [Rbase + offset], получено '{text}'.");
        int register = Register(match.Groups[1].Value);
        if (!match.Groups[3].Success) return (register, 0);
        long magnitude = ParseNumber(match.Groups[3].Value.Trim());
        long offset = match.Groups[2].Value == "-" ? -magnitude : magnitude;
        if (offset < short.MinValue || offset > short.MaxValue)
            throw new FormatException($"Смещение памяти {offset} не помещается в 16 бит.");
        return (register, (int)offset);
    }

    private static int Register(string text)
    {
        string value = text.Trim();
        if (value.Length < 2 || char.ToUpperInvariant(value[0]) != 'R' ||
            !int.TryParse(value[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int register) ||
            register is < 0 or > 15)
            throw new FormatException($"Неизвестный регистр '{text}', ожидается R0..R15.");
        return register;
    }

    private static uint ResolveEntry(ParsedLine line, IReadOnlyDictionary<string, uint> symbols, string sourceName)
    {
        (_, string rest) = SplitFirst(line.Statement);
        string[] args = SplitOperands(rest);
        if (args.Length != 1) Fail("Директива .entry требует ровно одну метку или адрес.", sourceName, line.Number, line.Original);
        long value = Resolve(args[0], symbols, true);
        if (value < 0 || value > uint.MaxValue)
            Fail($"Адрес точки входа {value} вне 32-битного диапазона.", sourceName, line.Number, line.Original);
        return (uint)value;
    }

    private static long Resolve(string token, IReadOnlyDictionary<string, uint> symbols, bool labelAllowed)
    {
        string value = token.Trim();
        if (labelAllowed && symbols.TryGetValue(value, out uint address)) return address;
        if (LabelPattern.IsMatch(value)) throw new FormatException($"Неизвестная метка '{value}'.");
        return ParseNumber(value);
    }

    private static long ParseNumber(string text)
    {
        string value = text.Trim();
        bool negative = value.StartsWith("-", StringComparison.Ordinal);
        if (negative || value.StartsWith("+", StringComparison.Ordinal)) value = value[1..];
        int numberBase = 10;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { numberBase = 16; value = value[2..]; }
        else if (value.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) { numberBase = 2; value = value[2..]; }
        if (value.Length == 0) throw new FormatException($"Некорректное число '{text}'.");
        try
        {
            long magnitude = Convert.ToInt64(value, numberBase);
            return negative ? -magnitude : magnitude;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            throw new FormatException($"Некорректное число '{text}'.");
        }
    }

    private static string[] SplitOperands(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var result = new List<string>();
        int start = 0, brackets = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '[') brackets++;
            else if (text[i] == ']') brackets--;
            else if (text[i] == ',' && brackets == 0)
            {
                result.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(text[start..].Trim());
        if (brackets != 0 || result.Any(string.IsNullOrWhiteSpace))
            throw new FormatException("Некорректный список операндов.");
        return result.ToArray();
    }

    private static void Count(string[] args, int expected, string instruction)
    {
        if (args.Length != expected)
            throw new FormatException($"{instruction.ToUpperInvariant()}: ожидалось операндов: {expected}, получено: {args.Length}.");
    }

    private static string StripComment(string line) => line.Split(';', 2)[0];
    private static string FirstToken(string line) => SplitFirst(line).First;
    private static (string First, string Remainder) SplitFirst(string line)
    {
        int end = 0;
        while (end < line.Length && !char.IsWhiteSpace(line[end])) end++;
        return (line[..end], line[end..].Trim());
    }

    private static void RequireLabel(string label, string source, int number, string text)
    {
        if (!LabelPattern.IsMatch(label)) Fail($"Некорректное имя метки '{label}'.", source, number, text);
    }

    private static void Fail(string message, string source, int number, string text) =>
        throw new CpuException(CpuError.Assembly, message, sourceName: source, lineNumber: number, sourceText: text);

    private sealed record ParsedLine(uint Address, int Number, string Original, string Statement);
}
