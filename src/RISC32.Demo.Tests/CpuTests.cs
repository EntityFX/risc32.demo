using Risc32.Demo;

namespace RISC32.Demo.Tests;

[TestClass]
public class CpuTests
{
    private static Cpu32 Load(string source, int memoryWords = 65_536)
    {
        var cpu = new Cpu32(memoryWords);
        cpu.Load(new Assembler32().Assemble(source));
        return cpu;
    }

    [TestMethod]
    public void ArithmeticLogicAndShifts_ProduceExpectedResults()
    {
        Cpu32 cpu = Load("""
            LDI R1, 7
            LDI R2, 3
            ADD R3, R1, R2
            SUB R4, R1, R2
            MUL R5, R1, R2
            DIV R6, R1, R2
            MOD R7, R1, R2
            AND R8, R1, R2
            OR  R9, R1, R2
            XOR R10, R1, R2
            NOT R11, R1
            SHL R12, R1, R2
            SHR R13, R1, R2
            LDI R0, -8
            SAR R14, R0, R2
            MOV R15, R5
            HALT
            """);

        cpu.Run(100);
        uint[] expected = [10, 4, 21, 2, 1, 3, 7, 4, 0xFFFF_FFF8, 56, 0, 0xFFFF_FFFF, 21];
        int[] registers = [3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        for (int i = 0; i < registers.Length; i++) Assert.AreEqual(expected[i], cpu.ReadRegister(registers[i]));
    }

    [TestMethod]
    public void LoadStore_UseWordAddressAndSignedOffset()
    {
        Cpu32 cpu = Load("""
            LDI R1, slot
            LDI R2, 42
            STORE R2, [R1]
            LOAD R3, [R1 + 0]
            HALT
            slot: .word 0
            """);

        cpu.Run(20);
        Assert.AreEqual((uint)42, cpu.ReadRegister(3));
        Assert.AreEqual((uint)42, cpu.Memory.Read(5));
    }

    [TestMethod]
    public void AddAndCompare_SetCarryOverflowAndSignedBranchesCorrectly()
    {
        Cpu32 overflow = Load("""
            LDI R1, max
            LOAD R1, [R1]
            LDI R2, 1
            ADD R3, R1, R2
            HALT
            max: .word 0x7FFFFFFF
            """);
        overflow.Run(20);
        Assert.IsTrue(overflow.State.Flags.HasFlag(CpuFlags.Negative));
        Assert.IsTrue(overflow.State.Flags.HasFlag(CpuFlags.Overflow));
        Assert.IsFalse(overflow.State.Flags.HasFlag(CpuFlags.Carry));

        Cpu32 branch = Load("""
            LDI R0, -2
            LDI R1, 1
            CMP R0, R1
            JL less
            LDI R2, 0
            HALT
            less: LDI R2, 42
            HALT
            """);
        branch.Run(20);
        Assert.AreEqual((uint)42, branch.ReadRegister(2));
    }

    [TestMethod]
    public void PushPop_AreLifoAndRestoreStackPointer()
    {
        Cpu32 cpu = Load("""
            LDI R1, 10
            LDI R2, 20
            PUSH R1
            PUSH R2
            POP R3
            POP R4
            HALT
            """, 32);
        uint initialSp = cpu.State.Sp;

        cpu.Run(20);
        Assert.AreEqual((uint)20, cpu.ReadRegister(3));
        Assert.AreEqual((uint)10, cpu.ReadRegister(4));
        Assert.AreEqual(initialSp, cpu.State.Sp);
    }

    [TestMethod]
    public void CallRet_SaveReturnAddressAndTrackDepth()
    {
        Cpu32 cpu = Load("""
            LDI R0, 7
            CALL function
            HALT
            function:
            PUSH R0
            LDI R0, 99
            POP R0
            RET
            """, 32);
        var depths = new List<int>();
        uint initialSp = cpu.State.Sp;

        cpu.Run(20, trace => depths.Add(trace.After.CallDepth));
        Assert.AreEqual((uint)7, cpu.ReadRegister(0));
        Assert.AreEqual(initialSp, cpu.State.Sp);
        Assert.AreEqual(0, cpu.State.CallDepth);
        CollectionAssert.Contains(depths, 1);
    }

    [TestMethod]
    public void DivisionByZero_DoesNotPartiallyChangeState()
    {
        Cpu32 cpu = Load("LDI R1, 9\nLDI R2, 0\nDIV R3, R1, R2\nHALT");
        cpu.Step(); cpu.Step();
        CpuState before = cpu.State;

        CpuException exception = Assert.ThrowsExactly<CpuException>(() => cpu.Step());
        Assert.AreEqual(CpuError.DivisionByZero, exception.Kind);
        Assert.AreEqual(before.Pc, cpu.State.Pc);
        Assert.AreEqual(before.Steps, cpu.State.Steps);
        CollectionAssert.AreEqual(before.Registers.ToArray(), cpu.State.Registers.ToArray());
    }

    [TestMethod]
    public void InvalidMemoryAccess_DoesNotChangeDestinationOrPc()
    {
        Cpu32 cpu = Load("LDI R1, 100\nLOAD R2, [R1]\nHALT", 8);
        cpu.Step();
        CpuState before = cpu.State;

        CpuException exception = Assert.ThrowsExactly<CpuException>(() => cpu.Step());
        Assert.AreEqual(CpuError.MemoryAccess, exception.Kind);
        Assert.AreEqual(before.Pc, cpu.State.Pc);
        Assert.AreEqual((uint)0, cpu.ReadRegister(2));
    }

    [TestMethod]
    public void HaltLimitAndEmptyStack_AreDiagnosed()
    {
        Cpu32 halted = Load("HALT");
        halted.Step();
        Assert.AreEqual(CpuError.StepAfterHalt,
            Assert.ThrowsExactly<CpuException>(() => halted.Step()).Kind);

        Cpu32 loop = Load("JMP -1");
        Assert.AreEqual(CpuError.StepLimit,
            Assert.ThrowsExactly<CpuException>(() => loop.Run(3)).Kind);

        Cpu32 pop = Load("POP R0");
        Assert.AreEqual(CpuError.StackUnderflow,
            Assert.ThrowsExactly<CpuException>(() => pop.Step()).Kind);

        Cpu32 ret = Load("RET");
        Assert.AreEqual(CpuError.StackUnderflow,
            Assert.ThrowsExactly<CpuException>(() => ret.Step()).Kind);
    }

    [TestMethod]
    public void StackCannotOverwriteProgramImage()
    {
        Cpu32 cpu = Load("LDI R0, 1\nPUSH R0\nJMP -2", 8);
        CpuException exception = Assert.ThrowsExactly<CpuException>(() => cpu.Run(20));
        Assert.AreEqual(CpuError.StackOverflow, exception.Kind);
        Assert.AreEqual((uint)3, cpu.State.Sp);
    }

    [TestMethod]
    public void TraceContainsFiveStagesAndRegisterDiff()
    {
        Cpu32 cpu = Load("LDI R3, 12\nHALT");
        StepTrace trace = cpu.Step();
        string text = TraceFormatter.FormatStep(trace);

        foreach (string stage in new[] { "FETCH", "DECODE", "EXECUTE", "MEMORY", "WRITEBACK" })
            StringAssert.Contains(text, stage);
        StringAssert.Contains(text, "R3: 0x00000000→0x0000000C");
    }
}
