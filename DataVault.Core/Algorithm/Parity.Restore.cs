namespace DataVault.Core.Algorithm;

public static partial class Parity
{
    public static void RestoreAFromBAndP(ReadOnlySpan<byte> b, ReadOnlySpan<byte> p, Span<byte> a)
    {
        CalculateP(b, p, a);
    }

    public static void RestoreBFromAAndP(ReadOnlySpan<byte> a, ReadOnlySpan<byte> p, Span<byte> b)
    {
        CalculateP(a, p, b);
    }

    public static void RestoreAFromBAndQ(ReadOnlySpan<byte> b, ReadOnlySpan<byte> q, Span<byte> a)
    {
        Mangle(b, a);
        CalculateP(q, a, a);
    }

    public static void RestoreBFromAAndQ(ReadOnlySpan<byte> a, ReadOnlySpan<byte> q, Span<byte> b)
    {
        CalculateP(a, q, b);
        Unmangle(b, b);
    }

    public static void RestoreBFromPAndQ(ReadOnlySpan<byte> p, ReadOnlySpan<byte> q, Span<byte> b)
    {
        CalculateP(p, q, b);
        Mangle(b, b);
    }

    public static (RestoreResult result, Stripe restored) TryRestore(Stripe provided, Span<byte> a, Span<byte> b, Span<byte> p, Span<byte> q, bool restoreP = false, bool restoreQ = false/*, bool validate = false*/)
    {
        Stripe restored = default;

        if (provided.HasFlag(Stripe.A) && provided.HasFlag(Stripe.B))
        {
            // Lucky you
        } else if (provided.HasFlag(Stripe.A) && provided.HasFlag(Stripe.P))
        {
            RestoreBFromAAndP(a, p, b);
            restored |= Stripe.B;
        } else if (provided.HasFlag(Stripe.B) && provided.HasFlag(Stripe.P))
        {
            RestoreAFromBAndP(b, p, a);
            restored |= Stripe.A;
        } else if (provided.HasFlag(Stripe.A) && provided.HasFlag(Stripe.Q))
        {
            RestoreBFromAAndQ(a, q, b);
            restored |= Stripe.B;
        } else if (provided.HasFlag(Stripe.B) && provided.HasFlag(Stripe.Q))
        {
            RestoreAFromBAndQ(b, q, a);
            restored |= Stripe.A;
        } else if (provided.HasFlag(Stripe.P) && provided.HasFlag(Stripe.Q))
        {
            RestoreBFromPAndQ(p, q, b);
            restored |= Stripe.B;

            RestoreAFromBAndP(b, p, a);
            restored |= Stripe.A;
        } else
        {
            return (RestoreResult.NotEnoughData, default);
        }

        if (restoreP && !provided.HasFlag(Stripe.P))
        {
            CalculateP(a, b, p);
            restored |= Stripe.P;
        }   
        if (restoreQ && !provided.HasFlag(Stripe.Q))
        {
            CalculateQ(a, b, q);
            restored |= Stripe.Q;
        }

        return (RestoreResult.Success, restored);
    }
}
