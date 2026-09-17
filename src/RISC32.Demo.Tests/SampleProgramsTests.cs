using Risc32.Demo;

namespace RISC32.Demo.Tests;

[TestClass]
public class SampleProgramsTests
{
    [TestMethod]
    [DataRow("01-arithmetic.asm", 70)]
    [DataRow("02-loop-sum.asm", 55)]
    [DataRow("03-memory.asm", 66)]
    [DataRow("04-bitwise-shifts.asm", 38)]
    [DataRow("05-stack.asm", 16)]
    [DataRow("06-nested-calls.asm", 7)]
    [DataRow("07-signed-division.asm", -9)]
    [DataRow("08-branches.asm", 123)]
    [DataRow("09-matrix-multiply.asm", 19)]
    [DataRow("10-cubic-roots.asm", 1)]
    [DataRow("11-matrix-7x7.asm", 28)]
    [DataRow("12-large-array-sum.asm", 5050)]
    public void SampleProgram_AssemblesRunsAndRestoresContext(string fileName, int expected)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Programs", fileName);
        string source = File.ReadAllText(path);
        ProgramImage image = new Assembler32().Assemble(source, fileName);
        var cpu = new Cpu32();
        cpu.Load(image);
        uint initialSp = cpu.State.Sp;

        cpu.Run(20_000);

        Assert.AreEqual(unchecked((uint)expected), cpu.ReadRegister(0), fileName);
        Assert.IsTrue(cpu.State.Halted, fileName);
        Assert.AreEqual(initialSp, cpu.State.Sp, fileName);
        Assert.AreEqual(0, cpu.State.CallDepth, fileName);
    }

    [TestMethod]
    public void MatrixMultiplication_WritesAllFourCells()
    {
        (Cpu32 cpu, ProgramImage image) = Run("09-matrix-multiply.asm");
        uint result = image.Symbols["result"];

        CollectionAssert.AreEqual(
            new uint[] { 19, 22, 43, 50 },
            Enumerable.Range(0, 4).Select(offset => cpu.Memory.Read(result + (uint)offset)).ToArray());
    }

    [TestMethod]
    public void CubicEquation_FindsRootsOneTwoThree()
    {
        (Cpu32 cpu, _) = Run("10-cubic-roots.asm");

        Assert.AreEqual((uint)1, cpu.ReadRegister(0));
        Assert.AreEqual((uint)2, cpu.ReadRegister(1));
        Assert.AreEqual((uint)3, cpu.ReadRegister(2));
        foreach (int root in new[] { 1, 2, 3 })
            Assert.AreEqual(0, root * root * root - 6 * root * root + 11 * root - 6);
    }

    [TestMethod]
    public void Matrix7By7_WritesAllFortyNineCells()
    {
        (Cpu32 cpu, ProgramImage image) = Run("11-matrix-7x7.asm");
        uint result = image.Symbols["result"];
        uint[] rowSums = [28, 77, 126, 175, 224, 273, 322];
        uint[] expected = rowSums.SelectMany(sum => Enumerable.Repeat(sum, 7)).ToArray();
        uint[] actual = Enumerable.Range(0, 49)
            .Select(offset => cpu.Memory.Read(result + (uint)offset)).ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void LargeArraySum_ProcessesAllOneHundredElements()
    {
        (Cpu32 cpu, ProgramImage image) = Run("12-large-array-sum.asm");

        Assert.AreEqual((uint)5050, cpu.ReadRegister(0));
        Assert.AreEqual((uint)100, cpu.ReadRegister(2));
        Assert.AreEqual((uint)1, cpu.Memory.Read(image.Symbols["numbers"]));
        Assert.AreEqual((uint)100, cpu.Memory.Read(image.Symbols["numbers"] + 99));
    }

    private static (Cpu32 Cpu, ProgramImage Image) Run(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Programs", fileName);
        ProgramImage image = new Assembler32().Assemble(File.ReadAllText(path), fileName);
        var cpu = new Cpu32();
        cpu.Load(image);
        cpu.Run(20_000);
        return (cpu, image);
    }
}
