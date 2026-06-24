using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;

namespace Org.Grush.HomeBase.WeatherStation.Data.Storage.Entities;

public class SubmittedReport
{
  [Key]
  public uint Id { get; private set; }

  public DateTime UtcTimestamp { get; private set; }

  public string AprsWxNetPacketBody  { get; private set; }

  public string AprsWxNetStationInformation { get; private set; }

  public static SubmittedReport From(AprsWxNetPacketBody packetBody, AprsWxNetStationInformation stationInformation)
    => new()
    {
      UtcTimestamp = packetBody.Time.UtcDateTime,
      AprsWxNetPacketBody = packetBody.Serialize(),
      AprsWxNetStationInformation = stationInformation.Serialize(),
    };
}
