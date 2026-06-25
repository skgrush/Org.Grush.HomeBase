using System.Text.RegularExpressions;

namespace Org.Grush.HomeBase.WeatherStation.Service;

public static partial class CustomParsing
{
  public static readonly Regex TimeSpanRe = Compile_TimeSpanRe();

  public static bool TryParseTimeSpan(string str, out TimeSpan timeSpan, Action<string>? reportError)
  {
    if (str.IsWhiteSpace())
    {
      reportError?.Invoke("value is only whitespace");
      timeSpan = TimeSpan.Zero;
      return false;
    }

    if (TimeSpan.TryParse(str, out timeSpan))
    {
      return true;
    }

    if (TimeSpanRe.Match(str) is not { Success: true } match)
    {
      reportError?.Invoke("value is not a custom TimeSpan string");
      timeSpan = TimeSpan.Zero;
      return false;
    }

    timeSpan = TimeSpanGroupParsers
      .Select(kvp =>
        match.Groups[kvp.Key] is { Success: true, ValueSpan: { } vs }
          ? kvp.Value(double.Parse(vs))
          : TimeSpan.Zero
      )
      .Where(t => t != TimeSpan.Zero)
      .Aggregate((x, y) => x + y);

    return true;
  }

  #region TimeSpanParsing
  private static readonly IReadOnlyDictionary<string, Func<double, TimeSpan>> TimeSpanGroupParsers = new Dictionary<string, Func<double, TimeSpan>>
  {
    { "second", TimeSpan.FromSeconds },
    { "minute", TimeSpan.FromMinutes },
    { "hour",   TimeSpan.FromHours },
  }.AsReadOnly();

  [GeneratedRegex(
    """
    ^\ *
    (?:(?<hour>   \d+(?:\.\d+)? ) \ *(?:hours?|hrs?|h)  )?\ *
    (?:(?<minute> \d+(?:\.\d+)? ) \ *(?:minutes?|mins?|m)  )?\ *
    (?:(?<second> \d+(?:\.\d+)? ) \ *(?:seconds?|secs?|s)  )?\ *
    $
    """, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant)]
  private static partial Regex Compile_TimeSpanRe();

  #endregion
}
