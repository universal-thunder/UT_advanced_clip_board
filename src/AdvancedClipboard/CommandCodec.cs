using System.Text;

namespace AdvancedClipboard;

internal static class CommandCodec
{
    internal static string Encode(IEnumerable<string> args) =>
        string.Join(".", args.Select(a => Convert.ToBase64String(Encoding.UTF8.GetBytes(a))));

    internal static string[] Decode(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return [];
        try
        {
            return line.Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Encoding.UTF8.GetString(Convert.FromBase64String(x))).ToArray();
        }
        catch { return []; }
    }
}
