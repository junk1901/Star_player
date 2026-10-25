namespace StarPlayer.SimHarness.Evidence;

/// <summary>The first JSON Pointer at which two evidence trees differ (object members by name, arrays by position, scalars by their JCS text), or null when they are the same value.</summary>
public static class JDiff
{
    public static string? First(JNode a, JNode b, string pointer = "")
    {
        switch (a, b)
        {
            case (JObject x, JObject y):
                foreach (var name in x.Members.Select(m => m.Key).Union(y.Members.Select(m => m.Key), StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    var child = $"{pointer}/{StrictJson.Token(name)}";
                    if (x.Get(name) is not { } xv || y.Get(name) is not { } yv)
                        return child;
                    if (First(xv, yv, child) is { } d)
                        return d;
                }

                return null;
            case (JArray x, JArray y):
                for (var i = 0; i < Math.Max(x.Items.Count, y.Items.Count); i++)
                {
                    if (i >= x.Items.Count || i >= y.Items.Count)
                        return $"{pointer}/{i}";
                    if (First(x.Items[i], y.Items[i], $"{pointer}/{i}") is { } d)
                        return d;
                }

                return null;
            default:
                return a.GetType() == b.GetType() && Jcs.Text(a) == Jcs.Text(b) ? null : pointer;
        }
    }
}
