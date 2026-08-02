using Onx100.Driver;
using Onx100.Driver.Configuration;
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

device.Onx100DeviceStateChanged+= (_, eventArgs) =>
{
    Onx100DeviceState state = eventArgs.CurrentState;
    Console.WriteLine($"State: power={state.PowerState}, input={state.SelectedInput}, volume={state.Volume}, muted={state.IsMuted}");
};

try
{
    Console.WriteLine("Connecting...");
    await device.ConnectAsync();

    Console.WriteLine("Powering on...");
    await device.PowerOnAsync();

    Console.WriteLine("Selecting input 2...");
    await device.SelectInputAsync(2);

    Console.WriteLine("Setting volume to 50...");
    await device.SetVolumeAsync(50);

    Console.WriteLine("Disabling mute...");
    await device.SetMuteAsync(false);

    int input = await device.GetSelectedInputAsync();
    int volume = await device.GetVolumeAsync();
    bool muted = await device.GetMuteAsync();

    Console.WriteLine();
    Console.WriteLine($"Final state: power={device.DeviceState.PowerState}, input={input}, volume={volume}, muted={muted}");
    Console.WriteLine("Press Enter to disconnect.");

    Console.ReadLine();

    await device.DisconnectAsync();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Driver error: {exception.Message}");
}