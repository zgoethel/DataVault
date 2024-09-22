using DataVault.Core;
using DataVault.Core.Algorithm;
using System.Runtime.InteropServices;

namespace DataVault;

using static Constants;

internal static class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: ./DataVault.exe file_name");
            return 1;
        }
        var fileName = args[0];

        var start = DateTime.Now;

        Span<byte> buffer = stackalloc byte[PARITY_SIZE * 2];
        var a = buffer[..PARITY_SIZE];
        var b = buffer[PARITY_SIZE..];

        Span<byte> p = stackalloc byte[PARITY_SIZE];
        Span<byte> q = stackalloc byte[PARITY_SIZE];

        byte[] createSizeBytes(long size)
        {
            var _size = new[] { size };
            return MemoryMarshal.Cast<long, byte>(_size).ToArray();
        }

        long createSize(ReadOnlySpan<byte> sizeBytes)
        {
            var _size = MemoryMarshal.Cast<byte, long>(sizeBytes);
            return _size[0];
        }

        var exists = File.Exists(fileName);
        if (exists)
        {
            using var input = File.OpenRead(fileName);

            using var outputA = File.Create($"{fileName}.a");
            using var outputB = File.Create($"{fileName}.b");
            using var outputP = File.Create($"{fileName}.p");
            using var outputQ = File.Create($"{fileName}.q");

            var inputInfo = new FileInfo(fileName);
            var sizeBytes = createSizeBytes(inputInfo.Length);

            outputA.Write(sizeBytes);
            outputB.Write(sizeBytes);
            outputP.Write(sizeBytes);
            outputQ.Write(sizeBytes);

            for (;;)
            {
                var readCount = input.Read(buffer);
                if (readCount == 0)
                {
                    break;
                }

                outputA.Write(a);
                outputB.Write(b);

                Parity.CalculateP(a, b, p);
                outputP.Write(p);

                Parity.CalculateQ(a, b, q);
                outputQ.Write(q);
            }
        } else
        {
            using var output = File.Create(fileName);

            var aExists = File.Exists($"{fileName}.a");
            var bExists = File.Exists($"{fileName}.b");
            var pExists = File.Exists($"{fileName}.p");
            var qExists = File.Exists($"{fileName}.q");

            if (!aExists && !bExists && !pExists && !qExists)
            {
                Console.Error.WriteLine("File not found");
                return 1;
            }

            using var inputA = aExists
                ? File.OpenRead($"{fileName}.a")
                : null;
            using var inputB = bExists
                ? File.OpenRead($"{fileName}.b")
                : null;
            using var inputP = pExists
                ? File.OpenRead($"{fileName}.p")
                : null;
            using var inputQ = qExists
                ? File.OpenRead($"{fileName}.q")
                : null;

            using var outputA = aExists
                ? null
                : File.Create($"{fileName}.a");
            using var outputB = bExists
                ? null
                : File.Create($"{fileName}.b");
            using var outputP = pExists
                ? null
                : File.Create($"{fileName}.p");
            using var outputQ = qExists
                ? null
                : File.Create($"{fileName}.q");

            var size = -1L;
            {
                var sizeBytes = createSizeBytes(size);

                foreach (var _input in new[] { inputA, inputB, inputP, inputQ }
                    .Where((it) => it is not null))
                {
                    _input!.Read(sizeBytes);
                    if (size != -1L & size != (size = createSize(sizeBytes)))
                    {
                        throw new Exception("File parts disagree on size");
                    }
                }

                foreach (var _output in new[] { outputA, outputB, outputP, outputQ }
                    .Where((it) => it is not null))
                {
                    _output!.Write(sizeBytes);
                }
            }

            for (var bytesRestored = 0L; bytesRestored < size; bytesRestored += PARITY_SIZE * 2)
            {
                if (aExists)
                {
                    inputA!.Read(a);
                }
                if (bExists)
                {
                    inputB!.Read(b);
                }
                if (pExists)
                {
                    inputP!.Read(p);
                }
                if (qExists)
                {
                    inputQ!.Read(q);
                }

                if (aExists && bExists)
                {
                    // Lucky you
                } else if (aExists && pExists)
                {
                    Parity.RestoreBFromAAndP(a, p, b);
                } else if (bExists && pExists)
                {
                    Parity.RestoreAFromBAndP(b, p, a);
                } else if (aExists && qExists)
                {
                    Parity.RestoreBFromAAndQ(a, q, b);
                } else if (bExists && qExists)
                {
                    Parity.RestoreAFromBAndQ(b, q, a);
                } else if (pExists && qExists)
                {
                    Parity.RestoreBFromPAndQ(p, q, b);

                    Parity.RestoreAFromBAndP(b, p, a);
                } else
                {
                    Console.Error.WriteLine("He's dead, Jim");
                    return 1;
                }

                if (!aExists)
                {
                    outputA!.Write(a);
                }
                if (!bExists)
                {
                    outputB!.Write(b);
                }
                if (!pExists)
                {
                    Parity.CalculateP(a, b, p);
                    outputP!.Write(p);
                }
                if (!qExists)
                {
                    Parity.CalculateQ(a, b, q);
                    outputQ!.Write(q);
                }

                // Result will always be within a 32-bit value
                var writeCount = (int)Math.Min(buffer.Length, size - bytesRestored);
                output.Write(buffer[..writeCount]);
            }
        }

        var runtime = DateTime.Now - start;
        Console.WriteLine($"{runtime.TotalMilliseconds:#,##0.00}ms");

        return 0;
    }
}
