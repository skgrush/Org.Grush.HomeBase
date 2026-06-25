using System.Device.Gpio;
using System.IO.Ports;
using FluentModbus;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Org.Grush.HomeBase.WeatherStation.Lib.SEN0658;

namespace Org.Grush.HomeBase.WeatherStation.Service;

public class Factory(ILoggerFactory loggerFactory)
{
  [MustDisposeResource]
  public GpioController GetGpioController() => new();

  [MustDisposeResource]
  public SerialPort SerialPort(string portName, int baudRate, System.IO.Ports.Parity parity, int dataBits, System.IO.Ports.StopBits stopBits)
    => new(portName, baudRate, parity, dataBits, stopBits);

  [MustDisposeResource]
  public WeatherStationClient WeatherStationClient(IModbusRtuSerialPort modbusPort, byte modbusUnitIdentifier)
    => new(
      modbusPort,
      modbusUnitIdentifier,
      loggerFactory.CreateLogger<WeatherStationClient>()
    );
}
