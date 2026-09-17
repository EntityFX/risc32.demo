using System.Globalization;

namespace Risc32.Demo;

/// <summary>Тестируемая консольная оболочка симулятора.</summary>
public static class DemoApplication
{
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        try
        {
            Options options = Parse(args);
            if (options.Help)
            {
                PrintHelp(output);
                return 0;
            }

            bool builtIn = options.ProgramPath is null;
            string source = builtIn ? DemoProgram.Source : File.ReadAllText(options.ProgramPath!);
            string sourceName = builtIn ? "factorial.asm (встроенный)" : Path.GetFullPath(options.ProgramPath!);
            bool traceEnabled = builtIn || options.Trace || options.Step;

            ProgramImage image = new Assembler32().Assemble(source, sourceName);
            var cpu = new Cpu32();
            cpu.Load(image);

            output.WriteLine("RISC32.Demo — учебный 32-разрядный процессор");
            output.WriteLine("Каждая команда проходит FETCH → DECODE → EXECUTE → MEMORY → WRITEBACK.");
            output.WriteLine();
            output.WriteLine("ЛИСТИНГ");
            output.WriteLine(TraceFormatter.FormatListing(image));
            output.WriteLine();
            output.WriteLine("ВЫПОЛНЕНИЕ");

            RunResult result;
            if (options.Step)
            {
                int executed = 0;
                while (!cpu.State.Halted)
                {
                    if (executed >= options.MaxSteps)
                        throw new CpuException(CpuError.StepLimit, $"Превышен лимит {options.MaxSteps} шагов.", cpu.State.Pc);
                    output.Write("Нажмите Enter для следующей команды... ");
                    input.ReadLine();
                    StepTrace step = cpu.Step();
                    output.WriteLine();
                    output.WriteLine(TraceFormatter.FormatStep(step));
                    executed++;
                }
                result = new RunResult(cpu.State, executed);
            }
            else
            {
                result = cpu.Run(options.MaxSteps, traceEnabled
                    ? step => output.WriteLine(TraceFormatter.FormatStep(step))
                    : null);
            }

            output.WriteLine();
            output.WriteLine("ИТОГОВОЕ СОСТОЯНИЕ");
            output.WriteLine(TraceFormatter.FormatState(result.State));
            if (builtIn) output.WriteLine($"Результат: R0 = {cpu.ReadRegister(0)}; ожидается 120 (5!).");

            if (options.Dump is { } dump)
            {
                output.WriteLine();
                output.WriteLine($"ДАМП ПАМЯТИ: начало 0x{dump.Start:X8}, слов {dump.Count}");
                output.WriteLine(TraceFormatter.FormatDump(cpu.Memory, dump.Start, dump.Count));
            }
            return 0;
        }
        catch (ArgumentException ex)
        {
            error.WriteLine($"Ошибка аргументов: {ex.Message}");
            error.WriteLine("Используйте --help для справки.");
            return 2;
        }
        catch (CpuException ex)
        {
            error.WriteLine($"Ошибка {ex.Kind}: {ex}");
            return 1;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Ошибка чтения программы: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Нет доступа к программе: {ex.Message}");
            return 1;
        }
    }

    public static void PrintHelp(TextWriter output)
    {
        output.WriteLine("Использование: RISC32.Demo [program.asm] [параметры]");
        output.WriteLine("Без файла запускается наглядный рекурсивный пример 5!.");
        output.WriteLine("  --trace              показать пять стадий каждой команды");
        output.WriteLine("  --step               ждать Enter перед каждой командой");
        output.WriteLine("  --max-steps N        ограничить число команд (по умолчанию 100000)");
        output.WriteLine("  --dump START COUNT   вывести COUNT слов памяти");
        output.WriteLine("  --help               показать эту справку");
        output.WriteLine("Числа принимаются в десятичном, 0x-шестнадцатеричном и 0b-двоичном виде.");
    }

    private static Options Parse(string[] args)
    {
        string? path = null;
        bool trace = false, step = false, help = false;
        int maxSteps = 100_000;
        (uint Start, int Count)? dump = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--trace": trace = true; break;
                case "--step": step = trace = true; break;
                case "--help" or "-h": help = true; break;
                case "--max-steps":
                    if (++i >= args.Length || !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out maxSteps) || maxSteps <= 0)
                        throw new ArgumentException("--max-steps требует положительное целое число.");
                    break;
                case "--dump":
                    if (i + 2 >= args.Length) throw new ArgumentException("--dump требует START и COUNT.");
                    uint start = ParseUnsigned(args[++i], "START");
                    uint countValue = ParseUnsigned(args[++i], "COUNT");
                    if (countValue == 0 || countValue > int.MaxValue) throw new ArgumentException("COUNT должен быть от 1 до 2147483647.");
                    dump = (start, (int)countValue);
                    break;
                default:
                    if (args[i].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException($"Неизвестный параметр '{args[i]}'.");
                    if (path is not null) throw new ArgumentException("Можно указать только один файл программы.");
                    path = args[i];
                    break;
            }
        }
        return new Options(path, trace, step, help, maxSteps, dump);
    }

    private static uint ParseUnsigned(string text, string name)
    {
        try
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return Convert.ToUInt32(text[2..], 16);
            if (text.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) return Convert.ToUInt32(text[2..], 2);
            return uint.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            throw new ArgumentException($"{name}: некорректное беззнаковое число '{text}'.");
        }
    }

    private sealed record Options(string? ProgramPath, bool Trace, bool Step, bool Help,
        int MaxSteps, (uint Start, int Count)? Dump);
}
