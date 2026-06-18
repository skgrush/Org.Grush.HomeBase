using Org.Grush.HomeBase.WeatherStation.Data.Storage;

namespace Org.Grush.HomeBase.WeatherStation.Data;

public class AprsWxNetReporterService(
  StorageService storage
)
{

  public static readonly TimeSpan WindSpeedAveragingPeriod = TimeSpan.FromMinutes(5);
  public static readonly TimeSpan PeakWindGusPeriod = TimeSpan.FromMinutes(5);

  public async Task<AprsWxNetPacketBody?> BuildReportAsync(
    CancellationToken cancellationToken
  )
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;

    var lastLogEntry = await storage.GetLastLogEntry(cancellationToken);
    if (lastLogEntry is null)
      return null;

    var datumz = await storage.GetDatumz(now.Add(WindSpeedAveragingPeriod).DateTime, now.Add(PeakWindGusPeriod).DateTime, cancellationToken);

    return new AprsWxNetPacketBody(
      Time: now,
      StationData: lastLogEntry.StationData!,
      AverageWindSpeed: datumz.AverageWindSpeed,
      PeakWindGust: datumz.PeakWindGust,
      LastHourRainfallMillimeters: datumz.LastHourRainfallMillimeters,
      LastDayRainfallMillimeters: datumz.LastDayRainfallMillimeters,
      TodayRainfallMillimeters: datumz.TodayRainfallMillimeters
    );
  }
}

// public class DateTimeSortedList<T>
// {
//   public SortedList<DateTimeOffset, T> List { get; } = [];
//   // public required TimeSpan ApproximateDistanceBetweenEntries { get; set; }
//
//   public (int Index, DateTimeOffset Key, T Value)? Last()
//   {
//     int index = List.Count - 1;
//     return (
//       index,
//       List.Keys[index],
//       List.Values[index]
//     );
//   }
//
//   public (int Index, DateTimeOffset Key, T Value)? FindNearest(DateTimeOffset to, bool notAfter)
//   {
//     int result = BinarySearch(to);
//
//     if (result >= 0)
//     {
//       return (
//         result,
//         List.Keys[result],
//         List.Values[result]
//       );
//     }
//     else
//     {
//
//       result = ~result;
//
//       // If value is not found and value is greater than all elements in array,
//       //    the negative number returned is the bitwise complement of (the index of the last element plus 1).
//       if (result >= List.Count)
//       {
//         if (notAfter)
//           return null;
//
//         return Last();
//       }
//
//       // If value is not found and value is less than one or more elements in array,
//       //    the negative number returned is the bitwise complement of the index of the first element that is larger than value.
//       else if (notAfter)
//         return (
//           result,
//           List.Keys[result],
//           List.Values[result]
//         );
//       else
//       {
//         // TODO
//         return null!;
//       }
//
//     }
//   }
//
//   public int BinarySearch(DateTimeOffset key)
//     => BinarySearch(0, List.Count, key);
//
//   /// <summary>
//   /// Literally just a trimmed copy of Array.BinarySearch but converted.
//   /// </summary>
//   public int BinarySearch(int index, int length, DateTimeOffset key)
//   {
//     IList<DateTimeOffset> keys = List.Keys;
//
//     int lo = index;
//     int hi = index + length - 1;
//     while (lo <= hi)
//     {
//       int i = lo + ((hi - lo) >> 1);
//       int order = keys[i].CompareTo(key);
//
//       if (order is 0)
//         return i;
//
//       if (order < 0)
//         lo = i + 1;
//       else
//         hi = i - 1;
//     }
//
//     return ~lo;
//   }
//
//
// }
