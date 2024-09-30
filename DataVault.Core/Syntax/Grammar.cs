namespace DataVault.Core.Syntax;

using static Token;

public class Grammar(
    Fsa dfa)
{
    public static Fsa CreateDfa()
    {
        var nfa = new Fsa();

        nfa.Build("[ \n\r\t\v\f]*", (int)Discard);

        var dfa = nfa.ConvertToDfa().MinimizeDfa();
        return dfa;
    }
}
