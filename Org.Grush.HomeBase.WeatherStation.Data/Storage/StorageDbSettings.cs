namespace Org.Grush.HomeBase.WeatherStation.Data.Storage;

public record StorageDbSettings(
  FileInfo? DbFile = null
)
{
  public StorageDbSettings WithDbFile(string appName)
  {
    DirectoryInfo localAppDataDir = new(Environment.GetFolderPath(
      Environment.SpecialFolder.LocalApplicationData,
      Environment.SpecialFolderOption.Create
    ));

    DirectoryInfo myAppDataDir = localAppDataDir.CreateSubdirectory(appName);

    return this with {
      DbFile = new(Path.Combine(myAppDataDir.FullName, appName + ".sqlite3")),
    };
  }
}
