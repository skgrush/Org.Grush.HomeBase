using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Org.Grush.HomeBase.WeatherStationLib.SEN0658;

namespace Org.Grush.HomeBase.APRSWXNET.Storage.Entities;

/// <summary>
///
/// <para>
/// <b>Size:</b> DateTime + nullablePtr + nullableDouble = 8 + 8 + 16. <br/>
/// <b>Total Size: 8 + 32 + 16 = 56 bytes</b>
/// </para>
/// <para>
/// The implication of this size is the following bytes-per-day storage cost:
/// <list type="number">
///   <item>period=1min => 1440/day => 80kB</item>
///   <item>period=10sec => 8640/day => 483kB</item>
///   <item></item>
/// </list>
/// </para>
/// </summary>
///
public record WeatherLogEntry(
  [property: Key]
  [property: DatabaseGenerated(DatabaseGeneratedOption.None)]
  DateTime UtcTimestamp,
  SEN0658AllData? StationData,
  double? RainfallMillimetersSinceLastEntry
)
{
  internal static void Configure(EntityTypeBuilder<WeatherLogEntry> e)
  {
    e.OwnsOne(
      p => p.StationData,
      owned => owned.ToJson()
    );
  }
}
