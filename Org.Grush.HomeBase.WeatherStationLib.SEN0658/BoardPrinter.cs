using System.Device;
using System.Text.Json;

namespace Org.Grush.HomeBase.WeatherStationLib.SEN0658;

public static class BoardPrinter
{
  public static void PrintComponentInfo(ComponentInformation info, string prefix)
  {
    Console.WriteLine("{0} {1} ({2})", prefix, info.Name, info.Description);
    Console.WriteLine("{0}   Properties: {1}", prefix, JsonSerializer.Serialize(info.Properties));
    if (info.SubComponents.Count is not 0)
    {
      Console.WriteLine("{0}   Subcomponents:", prefix);
      foreach (var subcomponent in info.SubComponents)
        PrintComponentInfo(subcomponent, prefix + "|    ");
    }
  }
}
