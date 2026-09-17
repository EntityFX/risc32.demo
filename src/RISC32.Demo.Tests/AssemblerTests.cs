using Risc32.Demo;

namespace RISC32.Demo.Tests;

[TestClass]
public class AssemblerTests
{
    private readonly Assembler32 _assembler = new();

    [TestMethod]
    public void TwoPassAssembler_ResolvesEntryForwardAndBackwardLabels()
    {
        ProgramImage image = _assembler.Assemble("""
            data:  .word 0xFFFFFFFF
            .entry start
            start: LDI R2, data
                   CALL worker
                   HALT
            worker: JMP start
            """, "labels.asm");

        Assert.AreEqual((uint)1, image.EntryPoint);
        Assert.AreEqual((uint)0, image.Symbols["DATA"]);
        Assert.AreEqual(0, Instruction32.Decode(image.Words[1]).Immediate);
        Assert.AreEqual(1, Instruction32.Decode(image.Words[2]).Immediate);
        Assert.AreEqual(-4, Instruction32.Decode(image.Words[4]).Immediate);
        Assert.AreEqual("labels.asm", image.SourceMap[2].SourceName);
    }

    [TestMethod]
    public void NumbersAndMemorySyntax_SupportAllDocumentedForms()
    {
        ProgramImage image = _assembler.Assemble("""
            LDI R1, 0b1010
            STORE R1, [R2 + 0x10]
            LOAD R3, [r2 - 2]
            .word -1
            .word 4294967295
            """);

        Assert.AreEqual(10, Instruction32.Decode(image.Words[0]).Immediate);
        Assert.AreEqual(16, Instruction32.Decode(image.Words[1]).Immediate);
        Assert.AreEqual(-2, Instruction32.Decode(image.Words[2]).Immediate);
        Assert.AreEqual(uint.MaxValue, image.Words[3]);
        Assert.AreEqual(uint.MaxValue, image.Words[4]);
    }

    [TestMethod]
    [DataRow("same: NOP\nsame: HALT", "повторно")]
    [DataRow("JMP nowhere", "Неизвестная метка")]
    [DataRow("LDI R16, 1", "регистр")]
    [DataRow("LDI R0, 999999", "20 бит")]
    [DataRow("WHAT R0", "Неизвестная команда")]
    [DataRow("LOAD R0, R1", "адрес вида")]
    public void InvalidSource_ReportsLineAndSourceText(string source, string expected)
    {
        CpuException exception = Assert.ThrowsExactly<CpuException>(() => _assembler.Assemble(source, "bad.asm"));
        Assert.AreEqual(CpuError.Assembly, exception.Kind);
        StringAssert.Contains(exception.ToString(), "bad.asm:");
        StringAssert.Contains(exception.Message, expected);
        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.SourceText));
    }

    [TestMethod]
    public void EntryMustPointInsideImage()
    {
        CpuException exception = Assert.ThrowsExactly<CpuException>(() =>
            _assembler.Assemble(".entry 10\nHALT", "entry.asm"));
        StringAssert.Contains(exception.Message, "не указывает");
    }
}
