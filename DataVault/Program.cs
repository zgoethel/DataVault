using System.Runtime.InteropServices;

namespace DataVault;

internal static class Program
{
    private static readonly int PARITY_SIZE = 4096;

    static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: ./DataVault.exe file_name");
            return 1;
        }
        var fileName = args[0];

        var buffer = new byte[PARITY_SIZE * 2];

        var pBuffer = new byte[PARITY_SIZE];
        var qBuffer = new byte[PARITY_SIZE];

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

        async Task<int> encode()
        {
            using var input = File.OpenRead(fileName);

            using var outputA = File.Create($"{fileName}.a");
            using var outputB = File.Create($"{fileName}.b");
            using var outputP = File.Create($"{fileName}.p");
            using var outputQ = File.Create($"{fileName}.q");

            var inputInfo = new FileInfo(fileName);
            var sizeBytes = createSizeBytes(inputInfo.Length);

            await Task.WhenAll(
                outputA.WriteAsync(sizeBytes, 0, sizeBytes.Length),
                outputB.WriteAsync(sizeBytes, 0, sizeBytes.Length),
                outputP.WriteAsync(sizeBytes, 0, sizeBytes.Length),
                outputQ.WriteAsync(sizeBytes, 0, sizeBytes.Length)
                );

            for (;;)
            {
                var readCount = await input.ReadAsync(buffer);
                if (readCount == 0)
                {
                    break;
                }
                
                await Task.WhenAll(

                    outputA.WriteAsync(buffer, 0, PARITY_SIZE),
                    outputB.WriteAsync(buffer, PARITY_SIZE, PARITY_SIZE),

                    Task.Run(async () =>
                    {
                        Parity.CalculateP(
                            buffer.AsSpan(0, PARITY_SIZE),
                            buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                            pBuffer);

                        await outputP.WriteAsync(pBuffer);
                    }),

                    Task.Run(async () =>
                    {
                        Parity.CalculateQ(
                            buffer.AsSpan(0, PARITY_SIZE),
                            buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                            qBuffer);

                        await outputQ.WriteAsync(qBuffer);
                    })

                    );
            }

            return 0;
        }

        async Task<int> restore()
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
                    await _input!.ReadAsync(sizeBytes);
                    if (size != -1L & size != (size = createSize(sizeBytes)))
                    {
                        throw new Exception("File parts disagree on size");
                    }
                }

                foreach (var _output in new[] { outputA, outputB, outputP, outputQ }
                    .Where((it) => it is not null))
                {
                    await _output!.WriteAsync(sizeBytes);
                }
            }

            for (var bytesRestored = 0L; bytesRestored < size; bytesRestored += PARITY_SIZE * 2)
            {
                if (aExists)
                {
                    await inputA!.ReadAsync(buffer.AsMemory(0, PARITY_SIZE));
                }
                if (bExists)
                {
                    await inputB!.ReadAsync(buffer.AsMemory(PARITY_SIZE, PARITY_SIZE));
                }
                if (pExists)
                {
                    await inputP!.ReadAsync(pBuffer);
                }
                if (qExists)
                {
                    await inputQ!.ReadAsync(qBuffer);
                }

                if (aExists && bExists)
                {
                    // Lucky you
                } else if (aExists && pExists)
                {
                    Parity.RestoreBFromAAndP(
                        buffer.AsSpan(0, PARITY_SIZE),
                        pBuffer,
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE));
                } else if (bExists && pExists)
                {
                    Parity.RestoreAFromBAndP(
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                        pBuffer,
                        buffer.AsSpan(0, PARITY_SIZE));
                } else if (aExists && qExists)
                {
                    Parity.RestoreBFromAAndQ(
                        buffer.AsSpan(0, PARITY_SIZE),
                        qBuffer,
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE));
                } else if (bExists && qExists)
                {
                    Parity.RestoreAFromBAndQ(
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                        qBuffer,
                        buffer.AsSpan(0, PARITY_SIZE));
                } else if (pExists && qExists)
                {
                    Parity.RestoreBFromPAndQ(
                        pBuffer,
                        qBuffer,
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE));

                    Parity.RestoreAFromBAndP(
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                        pBuffer,
                        buffer.AsSpan(0, PARITY_SIZE));
                } else
                {
                    Console.Error.WriteLine("He's dead, Jim");
                    return 1;
                }

                if (!aExists)
                {
                    await outputA!.WriteAsync(buffer.AsMemory(0, PARITY_SIZE));
                }
                if (!bExists)
                {
                    await outputB!.WriteAsync(buffer.AsMemory(PARITY_SIZE, PARITY_SIZE));
                }
                if (!pExists)
                {
                    Parity.CalculateP(
                        buffer.AsSpan(0, PARITY_SIZE),
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                        pBuffer);

                    await outputP!.WriteAsync(pBuffer);
                }
                if (!qExists)
                {
                    Parity.CalculateQ(
                        buffer.AsSpan(0, PARITY_SIZE),
                        buffer.AsSpan(PARITY_SIZE, PARITY_SIZE),
                        qBuffer);

                    await outputQ!.WriteAsync(qBuffer);
                }

                // Result will always be within a 32-bit value
                var writeCount = (int)Math.Min(PARITY_SIZE * 2, size - bytesRestored);
                await output.WriteAsync(buffer.AsMemory(0, writeCount));
            }

            return 0;
        }

        var start = DateTime.Now;

        var exists = File.Exists(fileName);
        var exitCode = exists 
            ? await encode()
            : await restore();

        var runtime = DateTime.Now - start;
        Console.WriteLine(runtime.TotalMilliseconds.ToString("#,##0.00") + "ms");

        return exitCode;
    }
}
