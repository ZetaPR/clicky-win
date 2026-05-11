using System.Text.RegularExpressions;
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Extracts <c>[POINT:...]</c> tags appended by the LLM at the end of a response,
/// stripping the tag and returning both the spoken text and the parsed coordinates.
/// </summary>
public static partial class PointTagParser
{
    [GeneratedRegex(
        @"\[POINT:(?:none|(\d+)\s*,\s*(\d+)(?::([^\]]*?))?(?::screen(\d+))?)\]\s*$",
        RegexOptions.Compiled)]
    private static partial Regex PointTagRegex();

    /// <summary>
    /// Parses an LLM response string, extracting the trailing <c>[POINT:...]</c> tag if present.
    /// Never throws; malformed input falls back to returning the full response as spoken text.
    /// </summary>
    public static PointTagParseResult Parse(string? fullResponse)
    {
        if (string.IsNullOrEmpty(fullResponse))
            return new PointTagParseResult(string.Empty, null, null, null, null);

        var match = PointTagRegex().Match(fullResponse);

        if (!match.Success)
            return new PointTagParseResult(fullResponse.Trim(), null, null, null, null);

        var spokenText = fullResponse[..match.Index].Trim();

        if (!match.Groups[1].Success)
            return new PointTagParseResult(spokenText, null, null, null, null);

        var x = int.Parse(match.Groups[1].Value);
        var y = int.Parse(match.Groups[2].Value);
        var label = match.Groups[3].Success && !string.IsNullOrEmpty(match.Groups[3].Value) ? match.Groups[3].Value : null;
        var screenNumber = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : (int?)null;

        return new PointTagParseResult(spokenText, x, y, label, screenNumber);
    }
}
