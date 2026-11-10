using System.Runtime.CompilerServices;
using System.Text.Json;

Console.WriteLine($"DynamicCode={RuntimeFeature.IsDynamicCodeSupported}; JsonReflection={JsonSerializer.IsReflectionEnabledByDefault}");
await TransportChecks.RunAsync();
await OperationChecks.RunAsync();
await MediaChecks.RunAsync();
await SocketChecks.RunAsync();
Console.WriteLine("SDK checks passed.");
