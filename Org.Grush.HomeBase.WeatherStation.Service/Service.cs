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

namespace Org.Grush.HomeBase.WeatherStation.Service;

public sealed class Service(
  [FromKeyedServices("stdout")] Stream stdout,
  Factory factory,
  CronService cron,
  StorageService storage,
  RainSenseService rainSense,
  AprsWxNetReporterService reporterService,
  AprsWxNetPacketSerializer packetSerializer,
  AprsWxNetStationInformation stationInformation,
  ILogger<Service> programLogger
) : IAsyncDisposable
{
  private readonly GpioController _controller = factory.GetGpioController();



  public async Task<int> ExecuteAsync(ServiceOptions result)
  {
    programLogger.LogDebug("Args: {cliOptionResult}", result);

    programLogger.LogDebug(
      "GPIO Controller ({type}) pinCount={pinCount}",
      _controller.GetType().Name,
      _controller.PinCount
    );

    await using var _ = (
      result.RainPin is {} rainPin
        ? CreateRainSenseListener(_controller, rainPin)
        : null
    );

    using SerialPort serialPort = factory.SerialPort(
      result.Device,
      baudRate: result.BaudRate,
      parity: Parity.None,
      dataBits: 8,
      stopBits: StopBits.OnePointFive
    );

    ModbusRtuSerialPort modbusPort = new(serialPort);

    await using WeatherStationClient client = factory.WeatherStationClient(
      modbusPort,
      result.ModbusUnitIdentifier
    );

    Utf8JsonWriter? stdoutWriterUtf8 = null;
    if (result.PrintJson)
    {
      stdoutWriterUtf8 = new(stdout, new()
      {
        Indented = true,
        NewLine = "\n",
      });
      stdoutWriterUtf8.WriteStartObject();
    }

    await using var pollingJob = await cron.AddParameterizedJob<(WeatherStationClient, Utf8JsonWriter?, StorageService, RainSenseService), bool>(new(
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
    await using var reportingJob = await cron.AddParameterizedJob<(StorageService, AprsWxNetReporterService, AprsWxNetPacketSerializer, ServiceOptions, AprsWxNetStationInformation), bool>(new(
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

    // wait until any are DISPOSED, not just stopped, which is allowed without exiting
    await Task.WhenAny(
      pollingJob.WaitUntil(CronService.CronJob.StateEnum.Disposed),
      reportingJob.WaitUntil(CronService.CronJob.StateEnum.Disposed)
    );

    stdoutWriterUtf8?.WriteEndObject();

    return 0;
  }


  private IAsyncDisposable CreateRainSenseListener(
    GpioController controller,
    int rainPin
  )
  {
    PinChangeEventHandler handler = (sender, args) => rainSense.AddRainTipInterrupt();

    return rainSense.Start(
      startup: () =>
        controller.RegisterCallbackForPinValueChangedEvent(rainPin, PinEventTypes.Rising, handler),
      cleanup: () =>
      {
        controller.UnregisterCallbackForPinValueChangedEvent(rainPin, handler);
        return null;
      }
    );
  }

  private static async Task<bool> ReportingJobFn(
    CronService.CronJob.CronJobRunContext<(StorageService, AprsWxNetReporterService, AprsWxNetPacketSerializer, ServiceOptions, AprsWxNetStationInformation)> ctx,
    CancellationToken token
  )
  {
    var (storage, reporter, serializer, options, stationInfo) = ctx.CallContext;

    var report = await reporter.BuildReportAsync(token);
    if (report is null) return false;

    Memory<byte> buffer82Byte = new byte[82];
    serializer.Serialize(buffer82Byte.Span, report.Value);

    bool success = true;
    if (options.CwopUri is not null)
    {
      success = await reporter.SubmitReportAsync(reportUri: options.CwopUri, messageBuffer: buffer82Byte,
        afterConnectDelay: TimeSpan.Zero, userDataDelay: TimeSpan.FromSeconds(3),
        beforeDisconnectDelay: TimeSpan.FromSeconds(3), token);
    }

    if (success)
      await storage.AddSuccessfulReportAsync(stationInfo, report.Value, token);

    return success;
  }

  private static async Task<bool> PollingJobFn(CronService.CronJob.CronJobRunContext<(WeatherStationClient, Utf8JsonWriter?, StorageService, RainSenseService)> ctx, CancellationToken innerToken)
  {
    var (client, stdoutWriterUtf8, storage, rainSense) = ctx.CallContext;

    var rainfallEntry = rainSense.SaveRainfall();

    var results = await client.ReadAllDataAsync(innerToken);
    var now = DateTimeOffset.Now;
    var nowStr = now.ToString("u");

    if (stdoutWriterUtf8 is not null)
    {
      stdoutWriterUtf8.WritePropertyName(nowStr);
      JsonSerializer.Serialize(writer: stdoutWriterUtf8, value: results, AprsWxNetSerializerContext.Default.SEN0658AllData);
      await stdoutWriterUtf8.FlushAsync(innerToken);
    }

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

  public async ValueTask DisposeAsync()
  {
    if (_controller is IAsyncDisposable controllerAsyncDisposable)
      await controllerAsyncDisposable.DisposeAsync();
    else
      _controller.Dispose();
  }
}
