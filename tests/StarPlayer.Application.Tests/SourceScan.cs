using System.Text;
using System.Text.RegularExpressions;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Deliberately small source scanner for architecture rules (no analyzer framework). It removes comments
/// and plain string literals so prose cannot trigger a rule, but keeps interpolated strings because their
/// holes are code, then applies named regular expressions.
/// </summary>
internal static class SourceScan
{
    public sealed record Rule(string Name, Regex Pattern);

    public static Rule R(string name, string pattern) => new(name, new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline));

    /// <summary>Constructs that make Simulation non-deterministic or let it touch files/network/threads.</summary>
    public static readonly Rule[] SimulationRules =
    [
        R("new Random", @"\bnew\s+(?:global::)?(?:System\.)?Random\s*\("),
        R("target-typed new Random", @"\bRandom\s+\w+\s*=\s*new\s*\("),
        R("Random.Shared", @"(?<![\w.])(?:global::)?(?:System\.)?Random\s*\.\s*Shared\b"),
        R("RandomNumberGenerator", @"\bRandomNumberGenerator\b"),
        R("DateTime clock", @"(?<![\w.])(?:global::)?(?:System\.)?DateTime(?:Offset)?\s*\.\s*(?:Now|UtcNow|Today)\b"),
        R("Guid.NewGuid", @"(?<![\w.])(?:global::)?(?:System\.)?Guid\s*\.\s*(?:NewGuid|CreateVersion7)\b"),
        R("Environment", @"(?<![\w.])(?:global::)?(?:System\.)?Environment\s*\."),
        R("Stopwatch/TimeProvider", @"\b(?:Stopwatch|TimeProvider)\b"),
        R("System.IO", @"\bSystem\s*\.\s*IO\b"),
        R("File static", @"(?<![\w.])(?:File|Directory)\s*\.\s*\w+"),
        R("file stream types", @"\bnew\s+(?:FileStream|StreamReader|StreamWriter|FileInfo|DirectoryInfo|FileSystemWatcher)\b"),
        R("Path temp", @"\bPath\s*\.\s*(?:GetTempPath|GetTempFileName|GetRandomFileName)\b"),
        R("System.Net", @"\bSystem\s*\.\s*Net\b|\bnew\s+(?:HttpClient|WebClient|Socket|TcpClient|UdpClient)\b"),
        R("thread scheduling", @"(?<![\w.])(?:Parallel|Thread|ThreadPool)\s*\.|\bTask\s*\.\s*(?:Run|Delay|Factory)\b"),
        R("randomized hash", @"\.\s*GetHashCode\s*\(|(?<![\w.])HashCode\s*\."),
    ];

    public static readonly Rule[] PresenterRules =
    [
        R("StarPlayer.Infrastructure", @"\bStarPlayer\s*\.\s*Infrastructure\b"),
        R("StarPlayer.Simulation", @"\bStarPlayer\s*\.\s*Simulation\b"),
        R("StarPlayer.Domain", @"\bStarPlayer\s*\.\s*Domain\b"),
        R("Microsoft.Data", @"\bMicrosoft\s*\.\s*Data\b"),
        R("SQLitePCL", @"\bSQLitePCL\b"),
        R("System.Data", @"\bSystem\s*\.\s*Data\b"),
        R("System.Net", @"\bSystem\s*\.\s*Net\b"),
        R("SqliteConnection", @"\bSqliteConnection\b"),
    ];

    public static List<string> Find(string source, IEnumerable<Rule> rules)
    {
        var code = Strip(source);
        return rules.Where(rule => rule.Pattern.IsMatch(code)).Select(rule => rule.Name).ToList();
    }

    public static string Strip(string s)
    {
        var sb = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            var next = i + 1 < s.Length ? s[i + 1] : '\0';

            if (c == '/' && next == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }

            if (c == '/' && next == '*')
            {
                i += 2;
                while (i < s.Length && !(s[i] == '*' && i + 1 < s.Length && s[i + 1] == '/'))
                {
                    if (s[i] == '\n') sb.Append('\n');
                    i++;
                }

                i += 2;
                continue;
            }

            if (c == '\'')
            {
                var j = i + 1;
                j += j < s.Length && s[j] == '\\' ? 2 : 1;
                while (j < s.Length && s[j] != '\'') j++;
                sb.Append("''");
                i = j + 1;
                continue;
            }

            if (c == '"' && next == '"' && i + 2 < s.Length && s[i + 2] == '"')
            {
                var end = s.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                sb.Append("\"\"");
                i = end < 0 ? s.Length : end + 3;
                continue;
            }

            if (c is '$' or '@' or '"')
            {
                var k = i;
                var verbatim = false;
                var interpolated = false;
                while (k < s.Length && s[k] is '$' or '@')
                {
                    if (s[k] == '@') verbatim = true; else interpolated = true;
                    k++;
                }

                if (k < s.Length && s[k] == '"')
                {
                    var j = k + 1;
                    while (j < s.Length)
                    {
                        if (verbatim)
                        {
                            if (s[j] == '"') { if (j + 1 < s.Length && s[j + 1] == '"') { j += 2; continue; } break; }
                            j++;
                        }
                        else
                        {
                            if (s[j] == '\\') { j += 2; continue; }
                            if (s[j] == '"') break;
                            j++;
                        }
                    }

                    var stop = Math.Min(j, s.Length - 1);
                    if (interpolated) sb.Append(s, i, stop + 1 - i); else sb.Append("\"\"");
                    i = stop + 1;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
