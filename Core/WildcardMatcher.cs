namespace FileFinder.Core;

/// <summary>
/// Glob matcher for queries containing <c>*</c> (any run of characters) or
/// <c>?</c> (any single character). Unlike the substring search, a glob is
/// anchored to the whole file name, so <c>*.gif</c> means "ends with .gif" and
/// <c>report*</c> means "starts with report". Operates on the same lower-cased
/// UTF-8 name bytes the SIMD path uses, so matching stays allocation-free.
/// </summary>
public static unsafe class WildcardMatcher
{
    private const byte Star = (byte)'*';
    private const byte Question = (byte)'?';

    public static bool HasWildcard(string s)
    {
        for (int i = 0; i < s.Length; i++)
            if (s[i] == '*' || s[i] == '?') return true;
        return false;
    }

    /// <summary>
    /// Classic linear glob match with backtracking on the last <c>*</c>.
    /// O(name * pattern) worst case, no allocations.
    /// </summary>
    public static bool Match(byte* s, int slen, byte* p, int plen)
    {
        int si = 0, pi = 0;
        int starP = -1, starS = 0;

        while (si < slen)
        {
            if (pi < plen && (p[pi] == s[si] || p[pi] == Question))
            {
                si++;
                pi++;
            }
            else if (pi < plen && p[pi] == Star)
            {
                starP = pi;       // remember the star position...
                starS = si;       // ...and where we tried to match it
                pi++;
            }
            else if (starP != -1)
            {
                pi = starP + 1;   // backtrack: let the star swallow one more char
                si = ++starS;
            }
            else
            {
                return false;
            }
        }

        // Trailing stars in the pattern can match the empty remainder.
        while (pi < plen && p[pi] == Star) pi++;
        return pi == plen;
    }
}
