using System.Text;

namespace StarPlayer.Domain.Common;

/// <summary>PascalCase to snake_case: the documented stable field/enum spelling for SQLite and JSON.</summary>
public static class SnakeCase
{
    public static string From(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(pascal[i]));
        }

        return sb.ToString();
    }
}
