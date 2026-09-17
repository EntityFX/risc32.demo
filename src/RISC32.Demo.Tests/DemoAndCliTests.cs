using Risc32.Demo;

namespace RISC32.Demo.Tests;

[TestClass]
public class DemoAndCliTests
{
    [TestMethod]
    public void RecursiveFactorial_Returns120AndRestoresContext()
    {
        ProgramImage image = new Assembler32().Assemble(DemoProgram.Source, "factorial.asm");
        var cpu = new Cpu32();
        cpu.Load(image);
        uint initialSp = cpu.State.Sp;
        var firstRun = new List<string>();

        cpu.Run(100, trace => firstRun.Add(TraceFormatter.FormatStep(trace)));
        Assert.AreEqual((uint)120, cpu.ReadRegister(0));
        Assert.AreEqual(initialSp, cpu.State.Sp);
        Assert.AreEqual(0, cpu.State.CallDepth);
        Assert.IsTrue(cpu.State.Halted);

        cpu.Reset();
        var secondRun = new List<string>();
        cpu.Run(100, trace => secondRun.Add(TraceFormatter.FormatStep(trace)));
        CollectionAssert.AreEqual(firstRun, secondRun);
    }

    [TestMethod]
    public void Cli_NoArgumentsRunsVisualDemo()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = DemoApplication.Run([], new StringReader(""), output, error);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("", error.ToString());
        StringAssert.Contains(output.ToString(), "ожидается 120");
        StringAssert.Contains(output.ToString(), "FETCH → DECODE → EXECUTE → MEMORY → WRITEBACK");
        StringAssert.Contains(output.ToString(), "глубина вызова 5");
    }

    [TestMethod]
    public void Cli_HelpAndBadArgumentsUseDocumentedExitCodes()
    {
        var help = new StringWriter();
        Assert.AreEqual(0, DemoApplication.Run(["--help"], TextReader.Null, help, TextWriter.Null));
        StringAssert.Contains(help.ToString(), "--max-steps");

        var error = new StringWriter();
        Assert.AreEqual(2, DemoApplication.Run(["--unknown"], TextReader.Null, TextWriter.Null, error));
        StringAssert.Contains(error.ToString(), "Ошибка аргументов");
    }

    [TestMethod]
    public void Cli_ExecutesExternalFileAndPrintsDump()
    {
        string path = Path.Combine(Path.GetTempPath(), $"risc32-{Guid.NewGuid():N}.asm");
        try
        {
            File.WriteAllText(path, "LDI R0, 7\nHALT");
            var output = new StringWriter();
            var error = new StringWriter();
            int exitCode = DemoApplication.Run([path, "--dump", "0", "2"], TextReader.Null, output, error);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual("", error.ToString());
            StringAssert.Contains(output.ToString(), "R0 =0x00000007");
            StringAssert.Contains(output.ToString(), "ДАМП ПАМЯТИ");
            Assert.IsFalse(output.ToString().Contains("┌─ Шаг", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Cli_StepModeWaitsBeforeEveryInstruction()
    {
        string path = Path.Combine(Path.GetTempPath(), $"risc32-step-{Guid.NewGuid():N}.asm");
        try
        {
            File.WriteAllText(path, "NOP\nHALT");
            var output = new StringWriter();
            int exitCode = DemoApplication.Run([path, "--step"], new StringReader("\n\n"), output, TextWriter.Null);

            Assert.AreEqual(0, exitCode);
            Assert.AreEqual(2, Count(output.ToString(), "Нажмите Enter"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static int Count(string text, string value) =>
        (text.Length - text.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
}
