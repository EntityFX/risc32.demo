namespace Risc32.Demo;

/// <summary>Коды операций учебной RISC32 ISA. Старшие восемь бит слова содержат код.</summary>
public enum Opcode : byte
{
    Nop = 0x00, Halt = 0x01,
    Ldi = 0x10, Mov = 0x11, Load = 0x12, Store = 0x13, Push = 0x14, Pop = 0x15,
    Add = 0x20, Sub = 0x21, Mul = 0x22, Div = 0x23, Mod = 0x24,
    And = 0x28, Or = 0x29, Xor = 0x2A, Not = 0x2B, Shl = 0x2C, Shr = 0x2D, Sar = 0x2E,
    Cmp = 0x30,
    Jmp = 0x40, Jz = 0x41, Jnz = 0x42, Jl = 0x43, Jle = 0x44, Jg = 0x45, Jge = 0x46,
    Call = 0x48, Ret = 0x49,
}

public enum InstructionFormat { Simple, R, I, U, J, S }

[Flags]
public enum CpuFlags { None = 0, Zero = 1, Negative = 2, Carry = 4, Overflow = 8 }

public enum CpuError
{
    Assembly, InvalidInstruction, MemoryAccess, StackOverflow, StackUnderflow,
    DivisionByZero, StepAfterHalt, StepLimit,
}

/// <summary>Единая диагностическая ошибка ассемблера или процессора.</summary>
public sealed class CpuException : Exception
{
    public CpuError Kind { get; }
    public uint? Pc { get; }
    public string? SourceName { get; }
    public int? LineNumber { get; }
    public string? SourceText { get; }

    public CpuException(CpuError kind, string message, uint? pc = null,
        string? sourceName = null, int? lineNumber = null, string? sourceText = null)
        : base(message)
    {
        Kind = kind;
        Pc = pc;
        SourceName = sourceName;
        LineNumber = lineNumber;
        SourceText = sourceText;
    }

    public override string ToString()
    {
        string location = SourceName is null ? "" : $"{SourceName}:{LineNumber}: ";
        string line = SourceText is null ? "" : $"\n    {SourceText.Trim()}";
        string pc = Pc is null ? "" : $" [PC=0x{Pc:X8}]";
        return $"{location}{Message}{pc}{line}";
    }
}

/// <summary>Декодированная 32-битная команда и функции её безопасного кодирования.</summary>
public readonly record struct Instruction32(
    uint Raw, Opcode Opcode, InstructionFormat Format, byte A, byte B, byte C, int Immediate)
{
    private const uint Low24Mask = 0x00FF_FFFF;

    public static Instruction32 Decode(uint raw)
    {
        byte code = (byte)(raw >> 24);
        if (!Enum.IsDefined(typeof(Opcode), code))
            throw new CpuException(CpuError.InvalidInstruction, $"Неизвестный opcode 0x{code:X2}.");

        Opcode op = (Opcode)code;
        InstructionFormat format = FormatOf(op);
        byte a = (byte)((raw >> 20) & 15);
        byte b = (byte)((raw >> 16) & 15);
        byte c = (byte)((raw >> 12) & 15);
        int immediate = 0;

        switch (format)
        {
            case InstructionFormat.Simple:
                Require((raw & Low24Mask) == 0, "У простой команды должны быть нулевые операнды.");
                break;
            case InstructionFormat.R:
                Require((raw & 0xFFF) == 0, "Резервные 12 бит R-формата должны быть нулевыми.");
                if (op is Opcode.Mov or Opcode.Not or Opcode.Cmp)
                    Require(c == 0, $"Команда {op.ToString().ToUpperInvariant()} не использует третий регистр.");
                break;
            case InstructionFormat.I:
                c = 0;
                immediate = SignExtend(raw & 0xFFFF, 16);
                break;
            case InstructionFormat.U:
                b = c = 0;
                immediate = SignExtend(raw & 0xF_FFFF, 20);
                break;
            case InstructionFormat.J:
                a = b = c = 0;
                immediate = SignExtend(raw & Low24Mask, 24);
                break;
            case InstructionFormat.S:
                b = c = 0;
                Require((raw & 0xF_FFFF) == 0, "Резервные 20 бит S-формата должны быть нулевыми.");
                break;
        }

        return new Instruction32(raw, op, format, a, b, c, immediate);
    }

    public static uint EncodeSimple(Opcode op)
    {
        RequireFormat(op, InstructionFormat.Simple);
        return (uint)op << 24;
    }

    public static uint EncodeR(Opcode op, int a, int b, int c = 0)
    {
        RequireFormat(op, InstructionFormat.R);
        CheckRegister(a); CheckRegister(b); CheckRegister(c);
        if (op is Opcode.Mov or Opcode.Not or Opcode.Cmp) Require(c == 0, "Лишний третий регистр.");
        return ((uint)op << 24) | ((uint)a << 20) | ((uint)b << 16) | ((uint)c << 12);
    }

    public static uint EncodeI(Opcode op, int a, int b, int immediate)
    {
        RequireFormat(op, InstructionFormat.I);
        CheckRegister(a); CheckRegister(b); CheckSigned(immediate, 16);
        return ((uint)op << 24) | ((uint)a << 20) | ((uint)b << 16) | ((uint)immediate & 0xFFFF);
    }

    public static uint EncodeU(Opcode op, int register, int immediate)
    {
        RequireFormat(op, InstructionFormat.U);
        CheckRegister(register); CheckSigned(immediate, 20);
        return ((uint)op << 24) | ((uint)register << 20) | ((uint)immediate & 0xF_FFFF);
    }

    public static uint EncodeJ(Opcode op, int offset)
    {
        RequireFormat(op, InstructionFormat.J); CheckSigned(offset, 24);
        return ((uint)op << 24) | ((uint)offset & Low24Mask);
    }

    public static uint EncodeS(Opcode op, int register)
    {
        RequireFormat(op, InstructionFormat.S); CheckRegister(register);
        return ((uint)op << 24) | ((uint)register << 20);
    }

    public string Disassemble() => Opcode switch
    {
        Opcode.Nop or Opcode.Halt or Opcode.Ret => Name,
        Opcode.Ldi => $"{Name} R{A}, {Immediate}",
        Opcode.Load => $"{Name} R{A}, {Address(B, Immediate)}",
        Opcode.Store => $"{Name} R{A}, {Address(B, Immediate)}",
        Opcode.Push or Opcode.Pop => $"{Name} R{A}",
        Opcode.Mov or Opcode.Not => $"{Name} R{A}, R{B}",
        Opcode.Cmp => $"{Name} R{A}, R{B}",
        Opcode.Jmp or Opcode.Jz or Opcode.Jnz or Opcode.Jl or Opcode.Jle or Opcode.Jg or Opcode.Jge or Opcode.Call
            => $"{Name} {(Immediate >= 0 ? "+" : "")}{Immediate}",
        _ => $"{Name} R{A}, R{B}, R{C}",
    };

    private string Name => Opcode.ToString().ToUpperInvariant();
    private static string Address(int register, int offset) => offset switch
    {
        0 => $"[R{register}]",
        > 0 => $"[R{register} + {offset}]",
        _ => $"[R{register} - {-offset}]",
    };

    private static InstructionFormat FormatOf(Opcode op) => op switch
    {
        Opcode.Nop or Opcode.Halt or Opcode.Ret => InstructionFormat.Simple,
        Opcode.Ldi => InstructionFormat.U,
        Opcode.Load or Opcode.Store => InstructionFormat.I,
        Opcode.Push or Opcode.Pop => InstructionFormat.S,
        Opcode.Jmp or Opcode.Jz or Opcode.Jnz or Opcode.Jl or Opcode.Jle or Opcode.Jg or Opcode.Jge or Opcode.Call
            => InstructionFormat.J,
        _ => InstructionFormat.R,
    };

    private static void RequireFormat(Opcode op, InstructionFormat expected)
    {
        Require(Enum.IsDefined(typeof(Opcode), op), $"Неизвестный opcode 0x{(byte)op:X2}.");
        Require(FormatOf(op) == expected, $"Команда {op} не относится к формату {expected}.");
    }

    private static void CheckRegister(int register) =>
        Require(register is >= 0 and < 16, $"Регистр R{register} вне диапазона R0..R15.");

    private static void CheckSigned(int value, int bits)
    {
        int min = -(1 << (bits - 1));
        int max = (1 << (bits - 1)) - 1;
        Require(value >= min && value <= max, $"Значение {value} не помещается в знаковое поле {bits} бит.");
    }

    private static int SignExtend(uint value, int bits)
    {
        uint sign = 1u << (bits - 1);
        uint mask = (1u << bits) - 1;
        value &= mask;
        return unchecked((int)((value ^ sign) - sign));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new CpuException(CpuError.InvalidInstruction, message);
    }
}
