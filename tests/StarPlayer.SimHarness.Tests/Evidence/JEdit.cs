using StarPlayer.SimHarness.Evidence;

namespace StarPlayer.SimHarness.Tests.Evidence;

/// <summary>Copy-on-write edits of an immutable evidence tree by JSON Pointer (tests only: every tamper case is a real, re-serialized document).</summary>
internal static class JEdit
{
    public static JNode Get(JNode root, string pointer)
    {
        var n = root;
        foreach (var t in Tokens(pointer))
            n = n is JObject o ? o[t] : ((JArray)n).Items[int.Parse(t)];
        return n;
    }

    public static JNode Set(JNode root, string pointer, JNode value) => Change(root, Tokens(pointer), 0, _ => value, remove: false);

    public static JNode Remove(JNode root, string pointer) => Change(root, Tokens(pointer), 0, _ => JNode.Null, remove: true);

    public static JNode Update(JNode root, string pointer, Func<JNode, JNode> f) => Change(root, Tokens(pointer), 0, f, remove: false);

    /// <summary>Adds a member to the object at <paramref name="pointer"/>.</summary>
    public static JNode Add(JNode root, string pointer, string name, JNode value) =>
        Update(root, pointer, n => new JObject([.. ((JObject)n).Members, new KeyValuePair<string, JNode>(name, value)]));

    private static string[] Tokens(string pointer) => pointer.Length == 0 ? [] : pointer[1..].Split('/').Select(t => t.Replace("~1", "/").Replace("~0", "~")).ToArray();

    private static JNode Change(JNode n, string[] tokens, int at, Func<JNode, JNode> f, bool remove)
    {
        if (at == tokens.Length)
            return f(n);
        var t = tokens[at];
        if (n is JObject o)
        {
            if (remove && at == tokens.Length - 1)
                return new JObject(o.Members.Where(m => m.Key != t).ToList());
            return new JObject(o.Members.Select(m => m.Key == t ? new KeyValuePair<string, JNode>(m.Key, Change(m.Value, tokens, at + 1, f, remove)) : m).ToList());
        }

        var a = (JArray)n;
        var i = int.Parse(t);
        if (remove && at == tokens.Length - 1)
            return new JArray(a.Items.Where((_, k) => k != i).ToList());
        return new JArray(a.Items.Select((x, k) => k == i ? Change(x, tokens, at + 1, f, remove) : x).ToList());
    }
}
