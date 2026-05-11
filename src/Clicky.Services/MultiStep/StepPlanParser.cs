using System.Text;
using System.Text.RegularExpressions;
using Clicky.Core;

namespace Clicky.Services;

/// <summary>
/// Incrementally parses [STEP:n:x,y:label]text[/STEP] tags from an SSE delta stream.
/// Call Feed() with each arriving delta; it returns any steps that completed in that chunk.
/// Tag boundaries may safely split across deltas — the parser buffers internally.
/// </summary>
public sealed partial class StepPlanParser
{
    [GeneratedRegex(
        @"\[STEP:(\d+)(?::(\d+),(\d+)(?::([^\]]*))?)?\]",
        RegexOptions.Compiled)]
    private static partial Regex StepOpenRegex();

    private const string CloseTag = "[/STEP]";

    private readonly StringBuilder _buffer = new();
    private int _scanStart;
    private PendingStep? _pending;

    private sealed record PendingStep(int Number, int? X, int? Y, string? Label, int TextStart);

    /// <summary>
    /// Feeds a delta string into the parser. Returns any steps whose [/STEP] closing tag
    /// was found within this or a prior delta.
    /// </summary>
    public IEnumerable<Step> Feed(string delta)
    {
        if (string.IsNullOrEmpty(delta))
            yield break;

        _buffer.Append(delta);
        var buf = _buffer.ToString();

        while (true)
        {
            if (_pending is null)
            {
                var m = StepOpenRegex().Match(buf, _scanStart);
                if (!m.Success) yield break;

                int? x = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : null;
                int? y = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : null;
                string? label = m.Groups[4].Success && m.Groups[4].Length > 0 ? m.Groups[4].Value : null;

                _pending = new PendingStep(
                    Number: int.Parse(m.Groups[1].Value),
                    X: x, Y: y, Label: label,
                    TextStart: m.Index + m.Length);
                _scanStart = _pending.TextStart;
            }
            else
            {
                var closeIdx = buf.IndexOf(CloseTag, _scanStart, StringComparison.Ordinal);
                if (closeIdx == -1) yield break;

                var text = buf[_pending.TextStart..closeIdx].Trim();
                yield return new Step
                {
                    Number = _pending.Number,
                    Text = text,
                    X = _pending.X,
                    Y = _pending.Y,
                    Label = _pending.Label,
                };

                _scanStart = closeIdx + CloseTag.Length;
                _pending = null;
            }
        }
    }
}
