using System.IO;
using Onx100.Driver;
using Onx100.Driver.Configuration;
using Onx100.Driver.Exceptions;
using Onx100.Driver.Models;

Onx100Options options = new Onx100Options
{
    Host = "127.0.0.1",
    Port = 4999,
    ConnectionTimeout = TimeSpan.FromSeconds(5),
    CommandTimeout = TimeSpan.FromSeconds(3),
    PowerTransitionTimeout = TimeSpan.FromSeconds(20)
};

await using Onx100Device device = new Onx100Device(options);

device.Onx100ConnectionStateChanged += (_, eventArgs) =>
{
    Console.WriteLine($"Connection: {eventArgs.PreviousState} -> {eventArgs.CurrentState}");
};

device.Onx100DeviceStateChanged += (_, eventArgs) =>
{
    Onx100DeviceState state = eventArgs.CurrentState;
    Console.WriteLine($"State: power={state.PowerState}, input={state.SelectedInput}, volume={state.Volume}, muted={state.IsMuted}");
};

try
{
    Console.WriteLine("Connecting...");
    await device.ConnectAsync();

    Console.WriteLine("Powering on...");
    await ExecuteOperationWithRecoveryAsync(device, "Power on", () => device.PowerOnAsync());

    Console.WriteLine("Selecting input 2...");
    await ExecuteSetterWithVerificationAsync(device, "Select input 2", 2, () => device.SelectInputAsync(2), () => device.GetSelectedInputAsync());

    Console.WriteLine("Setting volume to 50...");
    await ExecuteSetterWithVerificationAsync(device, "Set volume to 50", 50, () => device.SetVolumeAsync(50), () => device.GetVolumeAsync());

    Console.WriteLine("Disabling mute...");
    await ExecuteSetterWithVerificationAsync(device, "Disable mute", false, () => device.SetMuteAsync(false), () => device.GetMuteAsync());

    Console.WriteLine("Reading final device state...");

    int input = await ExecuteQueryWithRecoveryAsync(device, "IN ?", () => device.GetSelectedInputAsync());
    int volume = await ExecuteQueryWithRecoveryAsync(device, "VOL ?", () => device.GetVolumeAsync());
    bool muted = await ExecuteQueryWithRecoveryAsync(device, "MUTE ?", () => device.GetMuteAsync());

    Console.WriteLine();
    Console.WriteLine($"Final state: power={device.DeviceState.PowerState}, input={input}, volume={volume}, muted={muted}");

    await device.DisconnectAsync();

    Console.WriteLine("Demo completed successfully.");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Driver error: {exception}");
    Environment.ExitCode = 1;
}

static async Task ExecuteOperationWithRecoveryAsync(Onx100Device device, string operationName, Func<Task> operation)
{
    const int maximumAttempts = 2;

    for (int attempt = 1; attempt <= maximumAttempts; attempt++)
    {
        try
        {
            await operation();
            return;
        }
        catch (Exception exception) when (attempt < maximumAttempts && IsRecoverableCommunicationException(exception))
        {
            Console.WriteLine($"{operationName} failed: {exception.Message}");
            Console.WriteLine("Reconnecting and retrying once...");
            await ReconnectAsync(device);
        }
    }
}

static async Task<T> ExecuteQueryWithRecoveryAsync<T>(Onx100Device device, string commandName, Func<Task<T>> query)
{
    const int maximumAttempts = 2;

    for (int attempt = 1; attempt <= maximumAttempts; attempt++)
    {
        try
        {
            return await query();
        }
        catch (Exception exception) when (attempt < maximumAttempts && IsRecoverableCommunicationException(exception))
        {
            Console.WriteLine($"{commandName} failed: {exception.Message}");
            Console.WriteLine("Reconnecting and retrying query once...");
            await ReconnectAsync(device);
        }
    }

    throw new InvalidOperationException($"{commandName} failed after recovery attempt.");
}

static async Task ExecuteSetterWithVerificationAsync<T>(Onx100Device device, string operationName, T expectedValue, Func<Task> setter, Func<Task<T>> query)
{
    const int maximumSetterAttempts = 2;

    for (int attempt = 1; attempt <= maximumSetterAttempts; attempt++)
    {
        try
        {
            await setter();
            return;
        }
        catch (Exception exception) when (IsRecoverableCommunicationException(exception))
        {
            Console.WriteLine($"{operationName} acknowledgement was not received: {exception.Message}");
            Console.WriteLine("The command may still have been applied. Reconnecting and verifying state...");

            await ReconnectAsync(device);

            T actualValue = await ExecuteQueryWithRecoveryAsync(device, $"{operationName} verification", query);

            if (EqualityComparer<T>.Default.Equals(actualValue, expectedValue))
            {
                Console.WriteLine($"{operationName} was applied successfully.");
                return;
            }

            if (attempt == maximumSetterAttempts)
            {
                throw new InvalidOperationException($"{operationName} was not applied after {maximumSetterAttempts} attempts.", exception);
            }

            Console.WriteLine($"{operationName} was not applied. Retrying setter once...");
        }
    }
}

static async Task ReconnectAsync(Onx100Device device)
{
    const int maximumConnectAttempts = 3;

    await device.DisconnectAsync();

    for (int attempt = 1; attempt <= maximumConnectAttempts; attempt++)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt));
            await device.ConnectAsync();

            Console.WriteLine("Reconnected successfully.");
            return;
        }
        catch (Exception exception) when (attempt < maximumConnectAttempts && IsRecoverableCommunicationException(exception))
        {
            Console.WriteLine($"Reconnect attempt {attempt} failed: {exception.Message}");
        }
    }
}

static bool IsRecoverableCommunicationException(Exception exception)
{
    return exception is Onx100TimeoutException or IOException;
}