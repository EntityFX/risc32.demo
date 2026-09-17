using System.Collections.ObjectModel;

namespace Risc32.Demo;

/// <summary>Учебное ядро RISC32: выборка, декодирование, исполнение, память и фиксация результата.</summary>
public sealed class Cpu32
{
    private const uint SignBit = 0x8000_0000;
    private readonly uint[] _registers = new uint[16];
    private ProgramImage? _image;
    private uint _pc;
    private uint _sp;
    private CpuFlags _flags;
    private bool _halted;
    private int _steps;
    private int _callDepth;

    public Cpu32(int memoryWords = 65_536) => Memory = new Memory32(memoryWords);

    public Memory32 Memory { get; }
    public CpuState State => Snapshot();

    public uint ReadRegister(int register)
    {
        CheckRegister(register);
        return _registers[register];
    }

    public void SetRegister(int register, uint value)
    {
        CheckRegister(register);
        _registers[register] = value;
    }

    public void Load(ProgramImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Words.Count == 0 || image.Words.Count > Memory.WordCount)
            throw new CpuException(CpuError.MemoryAccess,
                $"Образ из {image.Words.Count} слов не помещается в память из {Memory.WordCount} слов.");
        if (image.EntryPoint >= image.Words.Count)
            throw new CpuException(CpuError.MemoryAccess, $"Точка входа 0x{image.EntryPoint:X8} вне образа.");
        _image = image;
        Reset();
    }

    /// <summary>Возвращает загруженную программу и архитектурное состояние к начальному виду.</summary>
    public void Reset()
    {
        Memory.Clear();
        Array.Clear(_registers);
        if (_image is not null)
        {
            for (uint i = 0; i < _image.Words.Count; i++) Memory.Write(i, _image.Words[(int)i]);
            _pc = _image.EntryPoint;
        }
        else _pc = 0;
        _sp = (uint)Memory.WordCount;
        _flags = CpuFlags.None;
        _halted = false;
        _steps = 0;
        _callDepth = 0;
    }

    /// <summary>Выполняет одну команду. Все проверки происходят до архитектурной фиксации.</summary>
    public StepTrace Step()
    {
        if (_halted) throw Fault(CpuError.StepAfterHalt, "Процессор уже остановлен командой HALT.");
        if (_pc >= Memory.WordCount) throw Fault(CpuError.MemoryAccess, "PC указывает за пределы памяти.");

        CpuState before = Snapshot();
        uint raw = Memory.Read(_pc);
        Instruction32 instruction;
        try { instruction = Instruction32.Decode(raw); }
        catch (CpuException ex) { throw Fault(ex.Kind, ex.Message); }

        uint[] registers = (uint[])_registers.Clone();
        uint nextPc = _pc + 1;
        uint nextSp = _sp;
        CpuFlags nextFlags = _flags;
        bool nextHalted = _halted;
        int nextDepth = _callDepth;
        (uint Address, uint Value)? pendingWrite = null;
        string execute = "Операция не изменяет данные.";
        string memory = "—";

        uint X() => _registers[instruction.B];
        uint Y() => _registers[instruction.C];
        void Result(uint value, CpuFlags extra = CpuFlags.None)
        {
            registers[instruction.A] = value;
            nextFlags = Zn(value) | extra;
        }

        switch (instruction.Opcode)
        {
            case Opcode.Nop:
                execute = "NOP: состояние данных не изменяется.";
                break;
            case Opcode.Halt:
                nextHalted = true;
                execute = "HALT: установлен признак останова.";
                break;
            case Opcode.Ldi:
                Result(unchecked((uint)instruction.Immediate));
                execute = $"R{instruction.A} ← {instruction.Immediate}.";
                break;
            case Opcode.Mov:
                Result(X());
                execute = $"R{instruction.A} ← R{instruction.B}.";
                break;
            case Opcode.Load:
            {
                uint address = EffectiveAddress(X(), instruction.Immediate);
                uint value = Memory.Read(address);
                Result(value);
                execute = $"Вычислен адрес 0x{address:X8}.";
                memory = $"READ  mem[0x{address:X8}] → 0x{value:X8}.";
                break;
            }
            case Opcode.Store:
            {
                uint address = EffectiveAddress(_registers[instruction.B], instruction.Immediate);
                uint value = _registers[instruction.A];
                pendingWrite = (address, value);
                execute = $"Вычислен адрес 0x{address:X8}.";
                memory = $"WRITE mem[0x{address:X8}] ← 0x{value:X8}.";
                break;
            }
            case Opcode.Push:
            {
                uint address = PushAddress(nextSp);
                uint value = _registers[instruction.A];
                pendingWrite = (address, value);
                nextSp = address;
                execute = $"PUSH R{instruction.A}: SP уменьшается.";
                memory = $"WRITE stack[0x{address:X8}] ← 0x{value:X8}.";
                break;
            }
            case Opcode.Pop:
            {
                uint value = PeekStack(nextSp);
                registers[instruction.A] = value;
                nextSp++;
                nextFlags = Zn(value);
                execute = $"POP R{instruction.A}: SP увеличивается.";
                memory = $"READ  stack[0x{_sp:X8}] → 0x{value:X8}.";
                break;
            }
            case Opcode.Add:
            {
                uint x = X(), y = Y(), result = unchecked(x + y);
                ulong wide = (ulong)x + y;
                CpuFlags f = wide > uint.MaxValue ? CpuFlags.Carry : CpuFlags.None;
                if (((~(x ^ y) & (x ^ result)) & SignBit) != 0) f |= CpuFlags.Overflow;
                Result(result, f);
                execute = $"0x{x:X8} + 0x{y:X8} = 0x{result:X8}.";
                break;
            }
            case Opcode.Sub:
            {
                uint x = X(), y = Y(), result = unchecked(x - y);
                Result(result, SubFlags(x, y, result));
                execute = $"0x{x:X8} − 0x{y:X8} = 0x{result:X8}.";
                break;
            }
            case Opcode.Mul:
            {
                uint x = X(), y = Y();
                ulong unsignedWide = (ulong)x * y;
                long signedWide = (long)(int)x * (int)y;
                uint result = unchecked((uint)unsignedWide);
                CpuFlags f = unsignedWide > uint.MaxValue ? CpuFlags.Carry : CpuFlags.None;
                if (signedWide is < int.MinValue or > int.MaxValue) f |= CpuFlags.Overflow;
                Result(result, f);
                execute = $"{(int)x} × {(int)y} = {(int)result} (младшие 32 бита).";
                break;
            }
            case Opcode.Div:
            case Opcode.Mod:
            {
                int x = unchecked((int)X()), y = unchecked((int)Y());
                if (y == 0) throw Fault(CpuError.DivisionByZero, "Деление на ноль.");
                long wide = instruction.Opcode == Opcode.Div ? (long)x / y : (long)x % y;
                uint result = unchecked((uint)wide);
                CpuFlags f = wide is < int.MinValue or > int.MaxValue ? CpuFlags.Overflow : CpuFlags.None;
                Result(result, f);
                execute = instruction.Opcode == Opcode.Div
                    ? $"{x} / {y} = {unchecked((int)result)}."
                    : $"{x} % {y} = {unchecked((int)result)}.";
                break;
            }
            case Opcode.And:
                Result(X() & Y()); execute = $"0x{X():X8} AND 0x{Y():X8}."; break;
            case Opcode.Or:
                Result(X() | Y()); execute = $"0x{X():X8} OR 0x{Y():X8}."; break;
            case Opcode.Xor:
                Result(X() ^ Y()); execute = $"0x{X():X8} XOR 0x{Y():X8}."; break;
            case Opcode.Not:
                Result(~X()); execute = $"NOT 0x{X():X8}."; break;
            case Opcode.Shl:
            case Opcode.Shr:
            case Opcode.Sar:
            {
                uint x = X(); int count = (int)(Y() & 31); uint result; bool carry;
                if (count == 0) { result = x; carry = false; }
                else if (instruction.Opcode == Opcode.Shl)
                {
                    carry = ((x >> (32 - count)) & 1) != 0; result = unchecked(x << count);
                }
                else if (instruction.Opcode == Opcode.Shr)
                {
                    carry = ((x >> (count - 1)) & 1) != 0; result = x >> count;
                }
                else
                {
                    carry = ((x >> (count - 1)) & 1) != 0; result = unchecked((uint)((int)x >> count));
                }
                Result(result, carry ? CpuFlags.Carry : CpuFlags.None);
                execute = $"{instruction.Opcode.ToString().ToUpperInvariant()} 0x{x:X8} на {count} → 0x{result:X8}.";
                break;
            }
            case Opcode.Cmp:
            {
                uint x = _registers[instruction.A], y = _registers[instruction.B], result = unchecked(x - y);
                nextFlags = Zn(result) | SubFlags(x, y, result);
                execute = $"CMP {(int)x} и {(int)y}: вычислено 0x{result:X8}, регистры не изменены.";
                break;
            }
            case Opcode.Jmp:
            case Opcode.Jz:
            case Opcode.Jnz:
            case Opcode.Jl:
            case Opcode.Jle:
            case Opcode.Jg:
            case Opcode.Jge:
            {
                bool taken = BranchCondition(instruction.Opcode, _flags);
                if (taken) nextPc = JumpTarget(nextPc, instruction.Immediate);
                execute = taken ? $"Условие истинно: PC ← 0x{nextPc:X8}." : "Условие ложно: переход не выполнен.";
                break;
            }
            case Opcode.Call:
            {
                uint target = JumpTarget(nextPc, instruction.Immediate);
                uint address = PushAddress(nextSp);
                pendingWrite = (address, nextPc);
                nextSp = address;
                nextPc = target;
                nextDepth++;
                execute = $"CALL 0x{target:X8}: сохранён адрес возврата 0x{before.Pc + 1:X8}.";
                memory = $"WRITE stack[0x{address:X8}] ← return 0x{before.Pc + 1:X8}.";
                break;
            }
            case Opcode.Ret:
            {
                if (nextDepth == 0) throw Fault(CpuError.StackUnderflow, "RET без соответствующего CALL.");
                uint target = PeekStack(nextSp);
                ValidateTarget(target);
                nextSp++;
                nextPc = target;
                nextDepth--;
                execute = $"RET: PC ← сохранённый адрес 0x{target:X8}.";
                memory = $"READ  stack[0x{_sp:X8}] → return 0x{target:X8}.";
                break;
            }
            default:
                throw Fault(CpuError.InvalidInstruction, $"Команда {instruction.Opcode} не реализована.");
        }

        if (pendingWrite is { } write) Memory.Write(write.Address, write.Value);
        Array.Copy(registers, _registers, registers.Length);
        _pc = nextPc; _sp = nextSp; _flags = nextFlags;
        _halted = nextHalted; _callDepth = nextDepth; _steps++;
        CpuState after = Snapshot();
        SourceLocation? source = null;
        _image?.SourceMap.TryGetValue(before.Pc, out source);
        string writeBack = DescribeChanges(before, after);
        return new StepTrace(_steps, raw, instruction, source, before, after,
            $"PC=0x{before.Pc:X8}; слово=0x{raw:X8}.",
            $"{instruction.Format}-формат: {instruction.Disassemble()}.", execute, memory, writeBack);
    }

    public RunResult Run(int maxSteps, Action<StepTrace>? traceSink = null)
    {
        if (maxSteps <= 0) throw new CpuException(CpuError.StepLimit, "Лимит шагов должен быть положительным.");
        int executed = 0;
        while (!_halted)
        {
            if (executed >= maxSteps)
                throw Fault(CpuError.StepLimit, $"Превышен лимит {maxSteps} шагов.");
            StepTrace trace = Step();
            traceSink?.Invoke(trace);
            executed++;
        }
        return new RunResult(Snapshot(), executed);
    }

    private uint EffectiveAddress(uint basis, int offset)
    {
        long address = (long)basis + offset;
        if (address < 0 || address >= Memory.WordCount)
            throw Fault(CpuError.MemoryAccess, $"Эффективный адрес {address} вне памяти.");
        return (uint)address;
    }

    private uint JumpTarget(uint followingPc, int offset)
    {
        long target = (long)followingPc + offset;
        if (target < 0 || target >= Memory.WordCount)
            throw Fault(CpuError.MemoryAccess, $"Цель перехода {target} вне памяти.");
        return (uint)target;
    }

    private void ValidateTarget(uint target)
    {
        if (target >= Memory.WordCount)
            throw Fault(CpuError.MemoryAccess, $"Адрес возврата 0x{target:X8} вне памяти.");
    }

    private uint PushAddress(uint sp)
    {
        uint floor = (uint)(_image?.Words.Count ?? 0);
        if (sp <= floor)
            throw Fault(CpuError.StackOverflow, "Стек столкнулся с загруженной программой и данными.");
        return sp - 1;
    }

    private uint PeekStack(uint sp)
    {
        if (sp >= Memory.WordCount) throw Fault(CpuError.StackUnderflow, "Стек пуст.");
        return Memory.Read(sp);
    }

    private static CpuFlags Zn(uint value) =>
        (value == 0 ? CpuFlags.Zero : CpuFlags.None) |
        ((value & SignBit) != 0 ? CpuFlags.Negative : CpuFlags.None);

    private static CpuFlags SubFlags(uint x, uint y, uint result)
    {
        CpuFlags flags = x >= y ? CpuFlags.Carry : CpuFlags.None;
        if ((((x ^ y) & (x ^ result)) & SignBit) != 0) flags |= CpuFlags.Overflow;
        return flags;
    }

    private static bool BranchCondition(Opcode op, CpuFlags flags)
    {
        bool z = flags.HasFlag(CpuFlags.Zero);
        bool n = flags.HasFlag(CpuFlags.Negative);
        bool v = flags.HasFlag(CpuFlags.Overflow);
        return op switch
        {
            Opcode.Jmp => true, Opcode.Jz => z, Opcode.Jnz => !z,
            Opcode.Jl => n != v, Opcode.Jle => z || n != v,
            Opcode.Jg => !z && n == v, Opcode.Jge => n == v,
            _ => false,
        };
    }

    private CpuState Snapshot() => new(
        new ReadOnlyCollection<uint>((uint[])_registers.Clone()),
        _pc, _sp, _flags, _halted, _steps, _callDepth);

    private CpuException Fault(CpuError kind, string message)
    {
        SourceLocation? source = null;
        _image?.SourceMap.TryGetValue(_pc, out source);
        return new CpuException(kind, message, _pc, source?.SourceName, source?.LineNumber, source?.Text);
    }

    private static string DescribeChanges(CpuState before, CpuState after)
    {
        var changes = new List<string>();
        for (int i = 0; i < 16; i++)
            if (before.Registers[i] != after.Registers[i])
                changes.Add($"R{i}: 0x{before.Registers[i]:X8}→0x{after.Registers[i]:X8}");
        if (before.Flags != after.Flags) changes.Add($"FLAGS: {before.Flags}→{after.Flags}");
        if (before.Sp != after.Sp) changes.Add($"SP: 0x{before.Sp:X8}→0x{after.Sp:X8}");
        if (before.Pc != after.Pc) changes.Add($"PC: 0x{before.Pc:X8}→0x{after.Pc:X8}");
        if (before.CallDepth != after.CallDepth) changes.Add($"DEPTH: {before.CallDepth}→{after.CallDepth}");
        if (before.Halted != after.Halted) changes.Add("HALTED=true");
        return changes.Count == 0 ? "Нет архитектурных изменений." : string.Join("; ", changes) + ".";
    }

    private static void CheckRegister(int register)
    {
        if (register is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(register));
    }
}
