using System.CommandLine;
using System.CommandLine.Invocation;
using System.Device.Gpio;
using System.IO.Ports;
using System.Text.Json;
using FluentModbus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.Lib.Cron;
using Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET;
using Org.Grush.HomeBase.WeatherStation.Data.Storage;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0575;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;
using AprsWxNetSerializerContext = Org.Grush.HomeBase.WeatherStation.Data.APRSWXNET.AprsWxNetSerializerContext;

namespace Org.Grush.HomeBase.WeatherStation.Cli;

public class RootCommandAction(
  IServiceScopeFactory scopeFactory
) : AsynchronousCommandLineAction
{
  public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
  {
    CliOptionResult result = CliOptionResult.From(parseResult);

    await using var scope = scopeFactory.CreateAsyncScope();

    var executor = scope.ServiceProvider.GetRequiredService<Executor>();

    return await executor.ExecuteAsync(result, cancellationToken);
  }

  public class Executor(
    [FromKeyedServices("stdout")] Stream stdout,
    ILoggerFactory loggerFactory,
    CronService cron,
    StorageService storage,
    RainSenseService rainSense,
    AprsWxNetReporterService reporterService,
    AprsWxNetPacketSerializer packetSerializer,
    AprsWxNetStationInformation stationInformation,
    ILogger<RootCommandAction> programLogger
  )
  {
    public async Task<int> ExecuteAsync(CliOptionResult result, CancellationToken cancellationToken)
    {
      programLogger.LogDebug("Args: {cliOptionResult}", result);

      // using LibGpiodV2Driver driver = new(4);
      // using GpioController controller = new(driver);

      using GpioController controller = new();

      programLogger.LogDebug(
        "GPIO Controller ({type}) pinCount={pinCount}",
        controller.GetType().Name,
        controller.PinCount
      );

      int? rainPin = null;
      // GpioControllerExtensions.PinChangeEventScope? pinChangeEventScope = null;
      IAsyncDisposable? rainSenseListener = null;

      if (rainPin is not null)
      {
        PinChangeEventHandler handler = (sender, args) => rainSense.AddRainTipInterrupt();
        rainSenseListener = rainSense.Start(
          startup: () =>
            controller.RegisterCallbackForPinValueChangedEvent(rainPin.Value, PinEventTypes.Rising, handler),
          cleanup: () =>
          {
            controller.UnregisterCallbackForPinValueChangedEvent(rainPin.Value, handler);
            return null;
          }
        );
      }

      await using var _ = rainSenseListener;

      using SerialPort serialPort = new(
        result.Device,
        baudRate: result.BaudRate,
        parity: Parity.None,
        dataBits: 8,
        stopBits: StopBits.OnePointFive
      );

      ModbusRtuSerialPort modbusPort = new(serialPort);

      await using WeatherStationClient client = new(
        modbusPort,
        1,
        loggerFactory.CreateLogger<WeatherStationClient>()
      );

      Utf8JsonWriter stdoutWriterUtf8 = new(stdout, new()
      {
        Indented = true,
        NewLine = "\n",
      });

      await using var pollingJob = await cron.AddParameterizedJob<(WeatherStationClient, Utf8JsonWriter, StorageService, RainSenseService), bool>(new(
        JobDescription: "PollingJob",
        Interval: result.QueryInterval,
        ParameterizedJobFn: PollingJobFn,
        StartImmediately: false,
        CallContext: (
          client,
          stdoutWriterUtf8,
          storage,
          rainSense
        )
      ));
      await using var reportingJob = await cron.AddParameterizedJob<(StorageService, AprsWxNetReporterService, AprsWxNetPacketSerializer, CliOptionResult, AprsWxNetStationInformation), bool>(new(
        JobDescription: "ReportingJob",
        Interval: result.ReportInterval,
        ParameterizedJobFn: ReportingJobFn,
        StartImmediately: false,
        CallContext: (
          storage,
          reporterService,
          packetSerializer,
          result,
          stationInformation
        )
      ));


      await Task.WhenAny(
        pollingJob.WaitUntil(CronService.CronJob.StateEnum.Disposed),
        reportingJob.WaitUntil(CronService.CronJob.StateEnum.Disposed)
      ).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

      stdout.Write("\n}\n"u8);

      return 0;
    }

    private static async Task<bool> ReportingJobFn(CronService.CronJob.CronJobRunContext<(StorageService, AprsWxNetReporterService, AprsWxNetPacketSerializer, CliOptionResult, AprsWxNetStationInformation)> ctx, CancellationToken token)
    {
      var (storage, reporter, serializer, options, stationInfo) = ctx.CallContext;

      var report = await reporter.BuildReportAsync(token);
      if (report is null) return false;

      Memory<byte> buffer82char = new byte[82];
      serializer.Serialize(buffer82char.Span, report.Value);

      bool success = await reporter.SubmitReportAsync(reportUri: options.CwopUri, messageBuffer: buffer82char, afterConnectDelay: TimeSpan.Zero, userDataDelay: TimeSpan.FromSeconds(3), beforeDisconnectDelay: TimeSpan.FromSeconds(3), token);

      if (success) await storage.AddSuccessfulReportAsync(stationInfo, report.Value, token);

      return success;
    }

    private static async Task<bool> PollingJobFn(CronService.CronJob.CronJobRunContext<(WeatherStationClient, Utf8JsonWriter, StorageService, RainSenseService)> ctx, CancellationToken innerToken)
    {
      var (client, stdoutWriterUtf8, storage, rainSense) = ctx.CallContext;

      var rainfallEntry = rainSense.SaveRainfall();

      var results = await client.ReadAllDataAsync(innerToken);
      var now = DateTimeOffset.Now;
      var nowStr = now.ToString("u");

      stdoutWriterUtf8.WritePropertyName(nowStr);
      JsonSerializer.Serialize(writer: stdoutWriterUtf8, value: results, AprsWxNetSerializerContext.Default.SEN0658AllData);
      await stdoutWriterUtf8.FlushAsync(innerToken);

      await storage.AddEntryAsync(
        entry: new(
          UtcTimestamp: now.UtcDateTime,
          StationData: results,
          RainfallMillimetersSinceLastEntry: rainfallEntry?.RainfallInMillimeters
        ),
        innerToken
      );

      ctx.Logger.LogDebug("Wrote data at ts={ts}", nowStr);

      return true;
    }
  }
}
