#define VECTORIZE

#if VECTORIZE
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
#endif

namespace DataVault.Core.Algorithm;

#if VECTORIZE
using _Vector = Vector256;
using _VectorT = Vector256<byte>;
#endif

public static partial class Parity
{
#if VECTORIZE
    private static readonly _VectorT _EVEN_BITS = _Vector.Create<byte>(0b10101010);
    private static readonly _VectorT _ODD_BITS = _Vector.Create<byte>(0b01010101);
#else
    private const byte _EVEN_BITS = 0b10101010;
    private const byte _ODD_BITS = 0b01010101;
#endif

    public static void CalculateP(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> p)
    {
        if (a.Length != b.Length || b.Length != p.Length)
        {
            throw new ArgumentException("Mismatch in sizes of provided spans");
        }
#if VECTORIZE
        if (a.Length % _VectorT.Count != 0)
        {
            throw new ArgumentException($"Span size must be evently divisible by {_VectorT.Count}");
        }

        var aVector = MemoryMarshal.Cast<byte, _VectorT>(a);
        var bVector = MemoryMarshal.Cast<byte, _VectorT>(b);

        var pVector = MemoryMarshal.Cast<byte, _VectorT>(p);

        for (var i = 0; i < aVector.Length; i++)
        {
            pVector[i] = _Vector.Xor(aVector[i], bVector[i]);
        }
#else
        for (var i = 0; i < a.Length; i++)
        {
            p[i] = (byte)(a[i] ^ b[i]);
        }
#endif
    }

    public static void Mangle(ReadOnlySpan<byte> b, Span<byte> mangle)
    {
        if (b.Length != mangle.Length)
        {
            throw new ArgumentException("Mismatch in sizes of provided spans");
        }
#if VECTORIZE
        if (b.Length % _VectorT.Count != 0)
        {
            throw new ArgumentException($"Span size must be evently divisible by {_VectorT.Count}");
        }

        var bVector = MemoryMarshal.Cast<byte, _VectorT>(b);

        var mangleVector = MemoryMarshal.Cast<byte, _VectorT>(mangle);

        for (var i = 0; i < bVector.Length; i++)
        {
            var b0 = _Vector.BitwiseAnd(bVector[i], _EVEN_BITS);
            var b1 = _Vector.BitwiseAnd(bVector[i], _ODD_BITS);

            var _b0 = _Vector.ShiftRightLogical(b0, 1);
            var _b1 = _Vector.ShiftLeft(b1, 1);

            mangleVector[i] = _Vector.BitwiseOr(_Vector.Xor(b0, _b1), _b0);
        }
#else
        for (var i = 0; i < b.Length; i++)
        {
            var b0 = b[i] & _EVEN_BITS;
            var b1 = b[i] & _ODD_BITS;

            var _b0 = b0 >>> 1;
            var _b1 = b1 << 1;

            mangle[i] = (byte)((b0 ^ _b1) | _b0);
        }
#endif
    }

    public static void Unmangle(ReadOnlySpan<byte> mangle, Span<byte> b)
    {
        Mangle(mangle, b);
        Mangle(b, b);
    }

    public static void CalculateQ(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, Span<byte> q)
    {
        Mangle(b, q);
        CalculateP(a, q, q);
    }
}
