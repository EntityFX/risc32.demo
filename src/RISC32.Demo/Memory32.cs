namespace Risc32.Demo;

/// <summary>Единая память команд, данных и стека, адресуемая 32-битными словами.</summary>
public sealed class Memory32
{
    private readonly uint[] _words;

    public Memory32(int wordCount = 65_536)
    {
        if (wordCount <= 0) throw new ArgumentOutOfRangeException(nameof(wordCount));
        _words = new uint[wordCount];
    }

    public int WordCount => _words.Length;

    public uint Read(uint address)
    {
        Check(address);
        return _words[(int)address];
    }

    public void Write(uint address, uint value)
    {
        Check(address);
        _words[(int)address] = value;
    }

    public void Clear() => Array.Clear(_words);

    private void Check(uint address)
    {
        if (address >= _words.Length)
            throw new CpuException(CpuError.MemoryAccess,
                $"Адрес слова 0x{address:X8} вне памяти 0..0x{_words.Length - 1:X8}.");
    }
}
