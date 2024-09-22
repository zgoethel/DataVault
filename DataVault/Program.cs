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

        Span<byte> buffer = stackalloc byte[STRIPE_SIZE * 2];
        var a = buffer[..STRIPE_SIZE];
        var b = buffer[STRIPE_SIZE..];

        Span<byte> p = stackalloc byte[STRIPE_SIZE];
        Span<byte> q = stackalloc byte[STRIPE_SIZE];

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

            input.Dispose();
            File.Delete(fileName);
        } else
        {
            using var output = File.Create(fileName);

            Stripe provided = default;
            if (File.Exists($"{fileName}.a"))
            {
                provided |= Stripe.A;
            }
            if (File.Exists($"{fileName}.b"))
            {
                provided |= Stripe.B;
            }
            if (File.Exists($"{fileName}.p"))
            {
                provided |= Stripe.P;
            }
            if (File.Exists($"{fileName}.q"))
            {
                provided |= Stripe.Q;
            }

            if (provided == default)
            {
                Console.Error.WriteLine("File not found");
                return 1;
            }

            using var inputA = provided.HasFlag(Stripe.A)
                ? File.OpenRead($"{fileName}.a")
                : null;
            using var inputB = provided.HasFlag(Stripe.B)
                ? File.OpenRead($"{fileName}.b")
                : null;
            using var inputP = provided.HasFlag(Stripe.P)
                ? File.OpenRead($"{fileName}.p")
                : null;
            using var inputQ = provided.HasFlag(Stripe.Q)
                ? File.OpenRead($"{fileName}.q")
                : null;

            using var outputA = provided.HasFlag(Stripe.A)
                ? null
                : File.Create($"{fileName}.a");
            using var outputB = provided.HasFlag(Stripe.B)
                ? null
                : File.Create($"{fileName}.b");
            using var outputP = provided.HasFlag(Stripe.P)
                ? null
                : File.Create($"{fileName}.p");
            using var outputQ = provided.HasFlag(Stripe.Q)
                ? null
                : File.Create($"{fileName}.q");

            var size = -1L;
            {
                var sizeBytes = createSizeBytes(size);

                foreach (var _input in new[] { inputA, inputB, inputP, inputQ }
                    .Where((it) => it is not null))
                {
                    _input!.Read(sizeBytes);
                    if (size != -1L
                        & size != (size = createSize(sizeBytes)))
                    {
                        Console.Error.WriteLine("File parts disagree on size");
                        return 1;
                    }
                }

                foreach (var _output in new[] { outputA, outputB, outputP, outputQ }
                    .Where((it) => it is not null))
                {
                    _output!.Write(sizeBytes);
                }
            }

            for (var bytesRestored = 0L; bytesRestored < size; bytesRestored += buffer.Length)
            {
                if (provided.HasFlag(Stripe.A))
                {
                    inputA!.Read(a);
                }
                if (provided.HasFlag(Stripe.B))
                {
                    inputB!.Read(b);
                }
                if (provided.HasFlag(Stripe.P))
                {
                    inputP!.Read(p);
                }
                if (provided.HasFlag(Stripe.Q))
                {
                    inputQ!.Read(q);
                }

                var (result, restored) = Parity.TryRestore(provided, a, b, p, q, restoreP: true, restoreQ: true);
                if (result != RestoreResult.Success)
                {
                    Console.Error.WriteLine($"Restoration failed: {result}");
                    return 1;
                }

                if (restored.HasFlag(Stripe.A))
                {
                    outputA!.Write(a);
                }
                if (restored.HasFlag(Stripe.B))
                {
                    outputB!.Write(b);
                }
                if (restored.HasFlag(Stripe.P))
                {
                    outputP!.Write(p);
                }
                if (restored.HasFlag(Stripe.Q))
                {
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
