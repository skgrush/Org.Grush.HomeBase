// using System.Buffers.Binary;
// using System.IO.Ports;
// using System.Runtime.InteropServices;
// using System.Text;
//
// namespace Org.Grush.HomeBase.WeatherStationLib;
//
// public class WeatherStationSerial
// {
//   public interface ISerialStruct
//   {
//     public static abstract ReadOnlySpan<byte> Com { get; }
//     public static abstract ReadOnlySpan<byte> ExpectedLeadingBytes { get; }
//     public static abstract int BinaryLength { get; }
//
//     public static abstract ISerialStruct Parse(ReadOnlySpan<byte> data);
//   }
//
//   public class BadCrcException<T>(
//     T FailedStruct,
//     ushort ComputedCrc,
//     ReadOnlySpan<byte> Span
//   )
//     : Exception where T : ISerialStruct;
//
//   /// <summary>
//   ///
//   /// </summary>
//   /// <param name="SlveAddress"></param>
//   /// <param name="FunctionId"></param>
//   /// <param name="ByteCount"></param>
//   /// <param name="RawBinaryWindSpeed"><see cref="WindSpeed"/></param>
//   /// <param name="Byte56"></param>
//   /// <param name="WindDirection"></param>
//   /// <param name="WindDirectionAngle"></param>
//   /// <param name="RawCrc"></param>
//   [StructLayout(LayoutKind.Explicit)]
//   public record struct WindSpeedAndDirection(
//     [field:FieldOffset(0)]
//     byte SlveAddress,
//     [field:FieldOffset(1)]
//     byte FunctionId,
//     [field:FieldOffset(2)]
//     byte ByteCount,
//     [field:FieldOffset(3)]
//     ushort RawBinaryWindSpeed,
//     [field:FieldOffset(5)]
//     ushort Byte56,
//     [field:FieldOffset(7)]
//     ushort WindDirection,
//     [field:FieldOffset(9)]
//     ushort WindDirectionAngle,
//     [field:FieldOffset(11)]
//     ushort RawCrc
//   ) : ISerialStruct
//   {
//     /// <summary>Wind speed in meters-per-second.</summary>
//     public double WindSpeed => RawBinaryWindSpeed / 100.0d;
//
//     /// <remarks>
//     /// Response:
//     ///   SlaveAddress=1
//     ///   FunctionCode=3  == read holding registers
//     /// </remarks>
//     public static ReadOnlySpan<byte> ExpectedLeadingBytes => [0x01, 0x03, 0x08];
//     /// <remarks>
//     /// Request:
//     ///   SlaveAddress=1
//     ///   FunctionCode=3  == read holding registers
//     ///   StartingAddress= 0x01_F4
//     ///   QuantityOfRegisters= 0x00_04
//     ///   ErrorCheck= 0x04_07
//     /// </remarks>
//     public static ReadOnlySpan<byte> Com => [0x01, 0x03, 0x01, 0xF4, 0x00, 0x04, 0x04, 0x07];
//
//     public static int BinaryLength => 13;
//
//     static ISerialStruct ISerialStruct.Parse(ReadOnlySpan<byte> data) => Parse(data);
//     public static WindSpeedAndDirection Parse(ReadOnlySpan<byte> data)
//     {
//       WindSpeedAndDirection obj = new(
//         SlveAddress: data[0],
//         FunctionId: data[1],
//         ByteCount: data[2],
//         RawBinaryWindSpeed: BinaryPrimitives.ReadUInt16BigEndian(data[3..5]),
//         Byte56: BinaryPrimitives.ReadUInt16BigEndian(data[5..7]),
//         WindDirection: BinaryPrimitives.ReadUInt16BigEndian(data[7..9]),
//         WindDirectionAngle: BinaryPrimitives.ReadUInt16BigEndian(data[9..11]),
//         RawCrc: BinaryPrimitives.ReadUInt16BigEndian(data[11..13])
//       );
//
//       ushort computedCrc = Crc16_2.Compute(data[..BinaryLength]);
//       if (computedCrc != obj.RawCrc)
//         throw new BadCrcException<WindSpeedAndDirection>(obj, computedCrc, data);
//
//       return obj;
//     }
//   }
//
//   [StructLayout(LayoutKind.Explicit)]
//   public record struct HumidityTemperatureNoise(
//     [field:FieldOffset(0)]
//     byte SlveAddress,
//     [field:FieldOffset(1)]
//     byte FunctionId,
//     [field:FieldOffset(2)]
//     byte ByteCount,
//     [field:FieldOffset(3)]
//     ushort RawBinaryHumidity,
//     [field:FieldOffset(5)]
//     ushort RawBinaryTemperature,
//     [field:FieldOffset(7)]
//     ushort RawBinaryNoiseDb,
//     [field:FieldOffset(9)]
//     ushort RawCrc
//   ) : ISerialStruct
//   {
//     /// <summary> %RH </summary>
//     public double Humidity => RawBinaryHumidity / 10.0d;
//     /// <summary> ºC </summary>
//     public double Temperature => RawBinaryTemperature / 10.0d;
//     /// <summary> dB </summary>
//     public double NoiseDb => RawBinaryNoiseDb / 10.0d;
//
//     public static ReadOnlySpan<byte> ExpectedLeadingBytes => [0x01, 0x03, 0x06];
//     /// <remarks>
//     /// Request:
//     ///   SlaveAddress=1
//     ///   FunctionCode=3  == read holding registers
//     ///   StartingAddress= 0x01_F8
//     ///   QuantityOfRegisters= 0x00_03
//     ///   ErrorCheck= 0x85_C6
//     /// </remarks>
//     public static ReadOnlySpan<byte> Com => [0x01, 0x03, 0x01, 0xF8, 0x00, 0x03, 0x85, 0xC6];
//     public static int BinaryLength => 11;
//     static ISerialStruct ISerialStruct.Parse(ReadOnlySpan<byte> data) => Parse(data);
//     public static HumidityTemperatureNoise Parse(ReadOnlySpan<byte> data)
//     {
//       HumidityTemperatureNoise obj = new(
//         SlveAddress: data[0],
//         FunctionId: data[1],
//         ByteCount: data[2],
//         RawBinaryHumidity: BinaryPrimitives.ReadUInt16BigEndian(data[3..5]),
//         RawBinaryTemperature: BinaryPrimitives.ReadUInt16BigEndian(data[5..7]),
//         RawBinaryNoiseDb: BinaryPrimitives.ReadUInt16BigEndian(data[7..9]),
//         RawCrc: BinaryPrimitives.ReadUInt16BigEndian(data[9..11])
//       );
//
//       ushort computedCrc = Crc16_2.Compute(data[..BinaryLength]);
//       if (computedCrc != obj.RawCrc)
//         throw new BadCrcException<HumidityTemperatureNoise>(obj, computedCrc, data);
//
//       return obj;
//     }
//   }
//
//   [StructLayout(LayoutKind.Explicit)]
//   public record struct Light(
//     [field:FieldOffset(0)]
//     byte SlveAddress,
//     [field:FieldOffset(1)]
//     byte FunctionId,
//     [field:FieldOffset(2)]
//     byte ByteCount,
//     [field:FieldOffset(3)]
//     UInt32 Lux,
//     [field:FieldOffset(7)]
//     ushort RawCrc
//   ) : ISerialStruct
//   {
//     public static ReadOnlySpan<byte> ExpectedLeadingBytes => [0x01, 0x03, 0x04];
//     /// <remarks>
//     /// Request:
//     ///   SlaveAddress=1
//     ///   FunctionCode=3  == read holding registers
//     ///   StartingAddress= 0x01_FE
//     ///   QuantityOfRegisters= 0x00_02
//     ///   ErrorCheck= 0xA4_07
//     /// </remarks>
//     public static ReadOnlySpan<byte> Com => [0x01, 0x03, 0x01, 0xFE, 0x00, 0x02, 0xA4, 0x07];
//     public static int BinaryLength => 9;
//     static ISerialStruct ISerialStruct.Parse(ReadOnlySpan<byte> data) => Parse(data);
//
//     public static Light Parse(ReadOnlySpan<byte> data)
//     {
//       Light obj = new(
//         SlveAddress: data[0],
//         FunctionId: data[1],
//         ByteCount: data[2],
//         Lux: BinaryPrimitives.ReadUInt32BigEndian(data[3..7]),
//         RawCrc: BinaryPrimitives.ReadUInt16BigEndian(data[9..11])
//       );
//
//       ushort computedCrc = Crc16_2.Compute(data[..BinaryLength]);
//       if (computedCrc != obj.RawCrc)
//         throw new BadCrcException<Light>(obj, computedCrc, data);
//
//       return obj;
//     }
//   }
//
//   /// <summary>
//   ///
//   /// </summary>
//   /// <param name="SlveAddress"></param>
//   /// <param name="FunctionId"></param>
//   /// <param name="ByteCount"></param>
//   /// <param name="Pm2_5"> µg/m³ </param>
//   /// <param name="Pm10"> µg/m³ </param>
//   /// <param name="RawBinaryAtmosphericPressure"> kPa </param>
//   /// <param name="RawCrc"></param>
//   [StructLayout(LayoutKind.Explicit)]
//   public record struct Pm25Pm10AtmosphericPressure(
//     [field:FieldOffset(0)]
//     byte SlveAddress,
//     [field:FieldOffset(1)]
//     byte FunctionId,
//     [field:FieldOffset(2)]
//     byte ByteCount,
//     [field:FieldOffset(3)]
//     ushort Pm2_5,
//     [field:FieldOffset(5)]
//     ushort Pm10,
//     [field:FieldOffset(7)]
//     ushort RawBinaryAtmosphericPressure,
//     [field:FieldOffset(9)]
//     ushort RawCrc
//   ) : ISerialStruct
//   {
//     public double AtmospherePressure => RawBinaryAtmosphericPressure / 10.0d;
//
//     public static ReadOnlySpan<byte> ExpectedLeadingBytes => [0x01, 0x03, 0x06];
//     public static ReadOnlySpan<byte> Com => [0x01, 0x03, 0x01, 0xFB, 0x00, 0x03, 0x75, 0xC6];
//     public static int BinaryLength => 11;
//     static ISerialStruct ISerialStruct.Parse(ReadOnlySpan<byte> data) => Parse(data);
//
//     public static Pm25Pm10AtmosphericPressure Parse(ReadOnlySpan<byte> data)
//     {
//       Pm25Pm10AtmosphericPressure obj = new(
//         SlveAddress: data[0],
//         FunctionId: data[1],
//         ByteCount: data[2],
//         Pm2_5: BinaryPrimitives.ReadUInt16BigEndian(data[3..5]),
//         Pm10: BinaryPrimitives.ReadUInt16BigEndian(data[5..7]),
//         RawBinaryAtmosphericPressure: BinaryPrimitives.ReadUInt16BigEndian(data[7..9]),
//         RawCrc: BinaryPrimitives.ReadUInt16BigEndian(data[9..11])
//       );
//
//       ushort computedCrc = Crc16_2.Compute(data[..BinaryLength]);
//       if (computedCrc != obj.RawCrc)
//         throw new BadCrcException<Pm25Pm10AtmosphericPressure>(obj, computedCrc, data);
//
//       return obj;
//     }
//
//   }
//
//   public const int BaudRate = 4800;
//   public const int ReadTimeoutMS = 1000;
//
//   public static void TMP_Setup(string portName)
//   {
//     SerialPort sensorSerial = new SerialPort(portName, BaudRate);
//
//     sensorSerial.Open();
//
//    sensorSerial.ReadTimeout = ReadTimeoutMS;
//   }
//
//   public static T Read<T>(SerialPort sensorSerial) where T : struct, ISerialStruct
//   {
//     sensorSerial.Write(T.Com.ToArray(), 0, T.Com.Length);
//
//     byte[] outputBytes = new byte[T.BinaryLength];
//
//     sensorSerial.ReadTo(Encoding.ASCII.GetString(T.ExpectedLeadingBytes));
//     T.ExpectedLeadingBytes.CopyTo(outputBytes);
//
//     int totalBytesRead = T.ExpectedLeadingBytes.Length;
//     do
//     {
//       totalBytesRead += sensorSerial.Read(outputBytes, totalBytesRead, T.BinaryLength - totalBytesRead);
//     } while (totalBytesRead < T.BinaryLength);
//
//     return (T)T.Parse(outputBytes);
//   }
//
//
//
//
//
//   // public static object TMP_readWind(SerialPort sensorSerial)
//   // {
//   //   ReadOnlySpan<byte> leadingBytesToRead = [0x01, 0x03, 0x08];
//   //   int bodyBytesToRead = 10;
//   //
//   //   sensorSerial.Write(Com_WindSpeedAndDirection.ToArray(), 0, Com_WindSpeedAndDirection.Length);
//   //
//   //   int totalBytesToBeRead = bodyBytesToRead + leadingBytesToRead.Length;
//   //   byte[] outputBytes = new byte[totalBytesToBeRead];
//   //
//   //   sensorSerial.ReadTo(Encoding.ASCII.GetString(leadingBytesToRead));
//   //   leadingBytesToRead.CopyTo(outputBytes);
//   //
//   //   int totalBytesRead = leadingBytesToRead.Length;
//   //
//   //   do
//   //   {
//   //     totalBytesRead += sensorSerial.Read(outputBytes, totalBytesRead, totalBytesToBeRead - totalBytesRead);
//   //   }
//   //   while (totalBytesRead < totalBytesToBeRead);
//   //
//   //   return WindSpeedAndDirection.Parse(outputBytes);
//   // }
//
//   public static class Crc16_2
//   {
//     public static ushort Compute(ReadOnlySpan<byte> data)
//     {
//       ushort crcWord = 0xFF_FF;
//
//       unchecked
//       {
//         foreach (var datum in data)
//         {
//           byte nTemp = (byte)(datum ^ (byte)crcWord);
//           crcWord >>= 8;
//           crcWord ^= CrcTable.Span[nTemp];
//         }
//       }
//       return crcWord;
//     }
//
//     public static readonly ReadOnlyMemory<ushort> CrcTable = (ushort[]) // 32 * 8
//     [
//       0X0000, 0XC0C1, 0XC181, 0X0140, 0XC301, 0X03C0, 0X0280, 0XC241,
//       0XC601, 0X06C0, 0X0780, 0XC741, 0X0500, 0XC5C1, 0XC481, 0X0440,
//       0XCC01, 0X0CC0, 0X0D80, 0XCD41, 0X0F00, 0XCFC1, 0XCE81, 0X0E40,
//       0X0A00, 0XCAC1, 0XCB81, 0X0B40, 0XC901, 0X09C0, 0X0880, 0XC841,
//       0XD801, 0X18C0, 0X1980, 0XD941, 0X1B00, 0XDBC1, 0XDA81, 0X1A40,
//       0X1E00, 0XDEC1, 0XDF81, 0X1F40, 0XDD01, 0X1DC0, 0X1C80, 0XDC41,
//       0X1400, 0XD4C1, 0XD581, 0X1540, 0XD701, 0X17C0, 0X1680, 0XD641,
//       0XD201, 0X12C0, 0X1380, 0XD341, 0X1100, 0XD1C1, 0XD081, 0X1040,
//       0XF001, 0X30C0, 0X3180, 0XF141, 0X3300, 0XF3C1, 0XF281, 0X3240,
//       0X3600, 0XF6C1, 0XF781, 0X3740, 0XF501, 0X35C0, 0X3480, 0XF441,
//       0X3C00, 0XFCC1, 0XFD81, 0X3D40, 0XFF01, 0X3FC0, 0X3E80, 0XFE41,
//       0XFA01, 0X3AC0, 0X3B80, 0XFB41, 0X3900, 0XF9C1, 0XF881, 0X3840,
//       0X2800, 0XE8C1, 0XE981, 0X2940, 0XEB01, 0X2BC0, 0X2A80, 0XEA41,
//       0XEE01, 0X2EC0, 0X2F80, 0XEF41, 0X2D00, 0XEDC1, 0XEC81, 0X2C40,
//       0XE401, 0X24C0, 0X2580, 0XE541, 0X2700, 0XE7C1, 0XE681, 0X2640,
//       0X2200, 0XE2C1, 0XE381, 0X2340, 0XE101, 0X21C0, 0X2080, 0XE041,
//       0XA001, 0X60C0, 0X6180, 0XA141, 0X6300, 0XA3C1, 0XA281, 0X6240,
//       0X6600, 0XA6C1, 0XA781, 0X6740, 0XA501, 0X65C0, 0X6480, 0XA441,
//       0X6C00, 0XACC1, 0XAD81, 0X6D40, 0XAF01, 0X6FC0, 0X6E80, 0XAE41,
//       0XAA01, 0X6AC0, 0X6B80, 0XAB41, 0X6900, 0XA9C1, 0XA881, 0X6840,
//       0X7800, 0XB8C1, 0XB981, 0X7940, 0XBB01, 0X7BC0, 0X7A80, 0XBA41,
//       0XBE01, 0X7EC0, 0X7F80, 0XBF41, 0X7D00, 0XBDC1, 0XBC81, 0X7C40,
//       0XB401, 0X74C0, 0X7580, 0XB541, 0X7700, 0XB7C1, 0XB681, 0X7640,
//       0X7200, 0XB2C1, 0XB381, 0X7340, 0XB101, 0X71C0, 0X7080, 0XB041,
//       0X5000, 0X90C1, 0X9181, 0X5140, 0X9301, 0X53C0, 0X5280, 0X9241,
//       0X9601, 0X56C0, 0X5780, 0X9741, 0X5500, 0X95C1, 0X9481, 0X5440,
//       0X9C01, 0X5CC0, 0X5D80, 0X9D41, 0X5F00, 0X9FC1, 0X9E81, 0X5E40,
//       0X5A00, 0X9AC1, 0X9B81, 0X5B40, 0X9901, 0X59C0, 0X5880, 0X9841,
//       0X8801, 0X48C0, 0X4980, 0X8941, 0X4B00, 0X8BC1, 0X8A81, 0X4A40,
//       0X4E00, 0X8EC1, 0X8F81, 0X4F40, 0X8D01, 0X4DC0, 0X4C80, 0X8C41,
//       0X4400, 0X84C1, 0X8581, 0X4540, 0X8701, 0X47C0, 0X4680, 0X8641,
//       0X8201, 0X42C0, 0X4380, 0X8341, 0X4100, 0X81C1, 0X8081, 0X4040,
//     ];
//   }
// }
