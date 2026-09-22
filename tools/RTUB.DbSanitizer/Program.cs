// Offline sanitizer for a production SQLite snapshot on its way to Azure DEV.
//
//   dotnet run --project tools/RTUB.DbSanitizer -- --source <snapshot.db> --destination <dev.db>
//
// The source is immutable input: it is opened read-only, and the run fails if its bytes change.
// The development password is read from the environment, never from an argument, so it cannot end
// up in a process listing, a shell history or a CI command echo.
//
// Exit codes: 0 sanitized, 1 a check failed, 2 the invocation was wrong.
using RTUB.Application.Services;

const string PasswordVariable = "RTUB_DEV_PASSWORD";

var source = ArgumentValue(args, "--source");
var destination = ArgumentValue(args, "--destination");

if (source is null || destination is null)
{
    Console.Error.WriteLine("usage: RTUB.DbSanitizer --source <snapshot.db> --destination <sanitized.db>");
    Console.Error.WriteLine($"       the development password is read from ${PasswordVariable}.");
    return 2;
}

var devPassword = Environment.GetEnvironmentVariable(PasswordVariable);
if (string.IsNullOrWhiteSpace(devPassword))
{
    Console.Error.WriteLine($"${PasswordVariable} is not set. Nothing was written.");
    return 2;
}

var result = DatabaseSanitizer.Sanitize(source, destination, devPassword);

if (!result.Success)
{
    // DatabaseSanitizer keeps row content out of its reasons, so this is safe to print in CI.
    Console.Error.WriteLine($"Sanitization failed: {result.FailureReason}");
    return 1;
}

Console.WriteLine(
    $"Sanitized {result.UsersSanitized} user(s), cleared credentials on {result.UsersWithoutUserName} "
    + $"user(s) without a usable user name, removed {result.PushSubscriptionsRemoved} push subscription(s).");
Console.WriteLine($"Source left byte-identical: {Path.GetFullPath(source)}");

return 0;

static string? ArgumentValue(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
