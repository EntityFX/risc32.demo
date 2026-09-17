using Risc32.Demo;

namespace RISC32.Demo.Tests;

[TestClass]
public class InstructionTests
{
    [TestMethod]
    public void AllFormats_EncodeAndDecodeRoundTrip()
    {
        uint[] words =
        [
            Instruction32.EncodeSimple(Opcode.Halt),
            Instruction32.EncodeR(Opcode.Add, 15, 1, 9),
            Instruction32.EncodeI(Opcode.Load, 2, 3, -32768),
            Instruction32.EncodeU(Opcode.Ldi, 4, 524287),
            Instruction32.EncodeJ(Opcode.Call, -8388608),
            Instruction32.EncodeS(Opcode.Push, 14),
        ];

        Assert.AreEqual(InstructionFormat.Simple, Instruction32.Decode(words[0]).Format);
        Assert.AreEqual((byte)15, Instruction32.Decode(words[1]).A);
        Assert.AreEqual(-32768, Instruction32.Decode(words[2]).Immediate);
        Assert.AreEqual(524287, Instruction32.Decode(words[3]).Immediate);
        Assert.AreEqual(-8388608, Instruction32.Decode(words[4]).Immediate);
        Assert.AreEqual((byte)14, Instruction32.Decode(words[5]).A);
    }

    [TestMethod]
    public void SignedFields_AcceptBothBoundaries()
    {
        Assert.AreEqual(-524288, Instruction32.Decode(Instruction32.EncodeU(Opcode.Ldi, 0, -524288)).Immediate);
        Assert.AreEqual(524287, Instruction32.Decode(Instruction32.EncodeU(Opcode.Ldi, 0, 524287)).Immediate);
        Assert.AreEqual(8388607, Instruction32.Decode(Instruction32.EncodeJ(Opcode.Jmp, 8388607)).Immediate);
    }

    [TestMethod]
    public void Disassembly_ShowsReadableMemoryAndRelativeOperands()
    {
        Assert.AreEqual("LOAD R2, [R3 - 7]", Instruction32.Decode(
            Instruction32.EncodeI(Opcode.Load, 2, 3, -7)).Disassemble());
        Assert.AreEqual("CALL +4", Instruction32.Decode(Instruction32.EncodeJ(Opcode.Call, 4)).Disassemble());
    }

    [TestMethod]
    public void Decode_RejectsUnknownOpcodeAndReservedBits()
    {
        CpuException unknown = Assert.ThrowsExactly<CpuException>(() => Instruction32.Decode(0xFF00_0000));
        CpuException reserved = Assert.ThrowsExactly<CpuException>(() => Instruction32.Decode(0x2000_0001));
        Assert.AreEqual(CpuError.InvalidInstruction, unknown.Kind);
        Assert.AreEqual(CpuError.InvalidInstruction, reserved.Kind);
    }

    [TestMethod]
    public void Encoders_RejectWrongFormatRegisterAndImmediate()
    {
        Assert.ThrowsExactly<CpuException>(() => Instruction32.EncodeR(Opcode.Jmp, 0, 0));
        Assert.ThrowsExactly<CpuException>(() => Instruction32.EncodeS(Opcode.Push, 16));
        Assert.ThrowsExactly<CpuException>(() => Instruction32.EncodeI(Opcode.Load, 0, 0, 32768));
        Assert.ThrowsExactly<CpuException>(() => Instruction32.EncodeU(Opcode.Ldi, 0, -524289));
    }
}
