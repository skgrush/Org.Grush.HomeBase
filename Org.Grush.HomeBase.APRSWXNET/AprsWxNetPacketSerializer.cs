
namespace Org.Grush.HomeBase.APRSWXNET;

/// <summary>
///
/// </summary>
/// <seealso href="https://pond1.gladstonefamily.net/aprswxnet.html"/>
/// <seealso href="http://wxqa.com/faq.html"/>
public class AprsWxNetPacketSerializer(
  AprsWxNetStationInformation stationInformation
)
{
  public const double MetersPerSecondToMph_Factor = 1d / 1609.34d * 60 * 60;
  public static double MetersPerSecondToMph(double metersPerSecond) => metersPerSecond * MetersPerSecondToMph_Factor;

  public static double CelsiusToFahrenheit(double celsius) => celsius * 9 / 5 + 32;

  public static double MillimetersToHundredsInch(double millimeters) => millimeters / 0.254d;
  public static void FormatMillimetersToHundredsInch(double? maybeMillimeters, Span<char> buffer)
  {
    if (maybeMillimeters is double millimeters)
      MillimetersToHundredsInch(millimeters).TryFormat(buffer, out var _, new string('0', buffer.Length));
    else
      buffer.Fill('.');
  }

  public static double RelativeHumidityRemainder(float relativeHumidity)
    => Math.Round(relativeHumidity, MidpointRounding.AwayFromZero) % 100d;

  public const double StandardAtmosphericPressureKPA = 101.325;
  public const double MolarMassOfAirKG_MOL = 0.0289644;
  public const double g = 9.80665;
  public const double UniversalGasConstant = 8.3144598;

  public static double KpaToDecimillibars(double kpa) => kpa * 100;

  public ReadOnlyMemory<char> LatLonChars { get; } = LatLonString(stationInformation.Latitude, stationInformation.Longitude);

  public void Serialize(
    Span<char> buffer82char,
    AprsWxNetPacketBody body
  )
  {
    // "CW0003>APRS,TCPIP*:/241505z4220.45N/07128.59W_032/005g008t054r001p078P048h50b10245e1w"

    stationInformation.CwNumber.CopyTo(buffer82char[0..6]);
    ">APRS,TCPIP*:@".CopyTo(buffer82char[6..20]);
    body.Time.TryFormat(buffer82char[20..27], out _, "ddhhmm\\z");

    LatLonChars.Span.CopyTo(buffer82char[27..45]);

    ///The underscore "_" followed by 3 numbers represents wind direction in degrees from true north.
    /// This is the direction that the wind is blowing from.
    ///
    /// The slash "/" followed by 3 numbers represents the average wind speed in miles per hour.
    ///
    /// The letter "g" followed by 3 numbers represents the peak instaneous value of wind in miles per hour.
    ///
    /// The letter "t" followed by 3 characters (numbers and minus sign) represents the temperature in degrees F.
    ///
    /// The letter "r" followed by 3 numbers represents the amount of rain in hundredths of inches that fell the past hour.
    /// The letter "p" followed by 3 numbers represents the amount of rain in hundredths of inches that fell in the past 24 hours.
    /// Only these two precipitation values are accepted by MADIS.
    ///
    /// The letter "P" followed by 3 numbers represents the amount of rain in hundredths of inches that fell since local midnight.
    ///
    /// The letter "b" followed by 5 numbers represents the barometric pressure in tenths of a millibar.
    ///
    /// The letter "h" followed by 2 numbers represents the relative humidity in percent, where "h00" implies 100% RH.
    ///
    /// The first four fields (wind direction, wind speed, temperature and gust) are required, in that order, and if a particular measurement is not present, the three numbers should be replaced by "..." to indicate no data available. Solar radiation data can also be coded into the data packet.
    ///


    buffer82char[45] = '_';
    body.StationData.WindDirectionAngle.TryFormat(buffer82char[46..49], out _, "000");

    buffer82char[49] = '/';
    MetersPerSecondToMph(body.AverageWindSpeed ?? body.StationData.WindSpeed).TryFormat(buffer82char[50..53], out _, "000");

    buffer82char[53] = 'g';
    MetersPerSecondToMph(body.PeakWindGust ?? body.StationData.WindSpeed).TryFormat(buffer82char[54..57], out _, "000");

    buffer82char[57] = 't';
    CelsiusToFahrenheit(body.StationData.Temperature).TryFormat(buffer82char[58..61], out _, "000");

    buffer82char[61] = 'r';
    FormatMillimetersToHundredsInch(body.LastHourRainfallMillimeters, buffer82char[62..65]);

    buffer82char[65] = 'p';
    FormatMillimetersToHundredsInch(body.LastDayRainfallMillimeters, buffer82char[66..69]);

    buffer82char[69] = 'P';
    FormatMillimetersToHundredsInch(body.TodayRainfallMillimeters, buffer82char[70..73]);

    buffer82char[73] = 'h';
    RelativeHumidityRemainder(body.StationData.RelativeHumidity).TryFormat(buffer82char[74..76], out _, "00");

    buffer82char[77] = 'b';
    KpaToDecimillibars(body.StationData.AtmosphericPressure).TryFormat(buffer82char[78..83], out _, "00000");


  }

  private static ReadOnlyMemory<char> LatLonString(double latitude, double longitude)
  {
    // Xddmm.hhN/dddmm.hhW

    Memory<char> characters = new char[19];
    Span<char> span = characters.Span;

    LatLonParts(latitude, characters.Span, out bool latPositive);
    span[8] = latPositive ? 'N' : 'S';

    span[9] = '/';

    LatLonParts(longitude, characters.Span[10..], out bool lonPositive);
    span[18] =  lonPositive ? 'E' : 'W';

    // drop the first latitude char
    return characters[1..];
  }

  public static void LatLonParts(double value, Span<char> span, out bool positive)
  {
    (byte degrees, double minutes, positive) = LatLonParts(value);

    degrees.TryFormat(span[0..3], out _, format: "000");
    minutes.TryFormat(span[3..8], out _, format: "00.00");
  }
  public static (byte degrees, double minutes, bool positive) LatLonParts(double value)
  {
    double degrees = Math.Round(value, MidpointRounding.ToZero);
    double minutes = 60d * (value - degrees);

    return (
      degrees: (byte)Math.Abs(degrees),
      minutes: Math.Abs(minutes),
      positive: degrees >= 0
    );
  }
}

