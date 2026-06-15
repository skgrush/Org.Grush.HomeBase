using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Org.Grush.HomeBase.WeatherStationLib.SEN0658;

namespace Org.Grush.HomeBase.APRSWXNET.Storage.Entities;

public record WeatherLogEntry(
  [property: Key]
  [property: DatabaseGenerated(DatabaseGeneratedOption.None)]
  DateTime Timestamp,
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
