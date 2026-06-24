using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Data.Storage;

namespace Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

public class AprsWxNetReporterService(
  StorageService storage,
  AprsWxNetStationInformation stationInformation,
  ILogger<AprsWxNetReporterService> logger
)
{
  public static readonly TimeSpan WindSpeedAveragingPeriod = TimeSpan.FromMinutes(5);
  public static readonly TimeSpan PeakWindGusPeriod = TimeSpan.FromMinutes(5);

  private readonly ReadOnlyMemory<byte> _signInSpan
    = Encoding.ASCII.GetBytes($"user {stationInformation.CwNumber} pass -1 vers linux-1wire 1.00\r\n");

  private readonly SemaphoreSlim semaphoreSlim = new(1);

  public async Task<AprsWxNetPacketBody?> BuildReportAsync(
    CancellationToken cancellationToken
  )
  {
    DateTimeOffset now = DateTimeOffset.UtcNow;

    var lastLogEntry = await storage.GetLastLogEntry(cancellationToken);
    if (lastLogEntry is null)
    {
      logger.LogWarning("No last logged entry found.");
      return null;
    }

    var datumz = await storage.GetAggregateData(now.Subtract(WindSpeedAveragingPeriod).UtcDateTime, now.Subtract(PeakWindGusPeriod).UtcDateTime, cancellationToken);

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

  public async Task<bool> SubmitReportAsync(
    Uri reportUri,
    ReadOnlyMemory<byte> messageBuffer,
    TimeSpan afterConnectDelay,
    TimeSpan userDataDelay,
    TimeSpan beforeDisconnectDelay,
    CancellationToken cancellationToken
  )
  {
    await semaphoreSlim.WaitAsync(cancellationToken);
    try
    {
      using var _ = logger.BeginScope("[SubmitReportAsync]");

      logger.LogInformation("Getting IPs for reportUri {ReportUri}", reportUri);
      var ipAddresses = await Dns.GetHostAddressesAsync(reportUri.Host, cancellationToken);
      var firstIp = ipAddresses.FirstOrDefault();
      if (firstIp is null)
      {
        logger.LogWarning("Found no IP for reportUri {ReportUri}", reportUri);
        return false;
      }

      logger.LogInformation("Found {count} IPs, first is {firstIP}", ipAddresses.Length, firstIp);

      using TcpClient client = new();
      await client.ConnectAsync(firstIp, reportUri.Port, cancellationToken);

      logger.LogTrace("Connected to server");

      await using var stream = client.GetStream();

      await Task.Delay(afterConnectDelay, cancellationToken);
      logger.LogTrace("Sending `user` line...");
      await stream.WriteAsync(_signInSpan, cancellationToken);
      logger.LogTrace("Sent `user` line.");
      await Task.Delay(userDataDelay, cancellationToken);
      logger.LogTrace("Sending data line...");
      await stream.WriteAsync(messageBuffer, cancellationToken);
      logger.LogTrace("Sent data line.");
      await Task.Delay(beforeDisconnectDelay, cancellationToken);

      logger.LogTrace("Disconnecting client...");
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Failed to submit report");
      throw;
    }
    finally
    {
      semaphoreSlim.Release();
    }

    logger.LogInformation("Disconnected client successfully.");
    return true;
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
