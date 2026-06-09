using System.Text.Json.Serialization;

namespace Org.Grush.Port.DFRobot.CH432T;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(IntStatusReg))]
[JsonSerializable(typeof(LSRRegister))]
[JsonSerializable(typeof(ModemConfigReg))]
public partial class Ch432tSerializerContext : JsonSerializerContext;
