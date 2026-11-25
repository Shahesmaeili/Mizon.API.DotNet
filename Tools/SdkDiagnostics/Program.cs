using System.Runtime.CompilerServices;
using System.Text.Json;

Console.WriteLine($"DynamicCode={RuntimeFeature.IsDynamicCodeSupported}; JsonReflection={JsonSerializer.IsReflectionEnabledByDefault}");
if (args is ["--operations-only"])
{
    await OperationChecks.RunAsync();
    return;
}

if (args is not ["--presence-only"])
{
    await TransportChecks.RunAsync();
    await OperationChecks.RunAsync();
    await MediaChecks.RunAsync();
}
await SocketChecks.RunAsync();
await WindowPresenceChecks.RunAsync();
await AccountPresenceChecks.RunAsync();
Console.WriteLine("SDK checks passed.");
