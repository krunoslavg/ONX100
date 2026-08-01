using System.IO;
using System.Net.Sockets;
using System.Text;

const string host = "127.0.0.1";
const int port = 4999;

object consoleLock = new();

using var applicationCancellation = new CancellationTokenSource();

bool exitRequested = false;
string? pendingCommand = null;

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    applicationCancellation.Cancel();
};

while (!applicationCancellation.IsCancellationRequested && !exitRequested)
{
    using var connectionCancellation =
        CancellationTokenSource.CreateLinkedTokenSource(
            applicationCancellation.Token);

    using var client = new TcpClient();

    NetworkStream? clientStream = null;
    Task? receiveTask = null;

    try
    {
        WriteStatus($"Spajanje na {host}:{port}...", ConsoleColor.DarkYellow);

        await client.ConnectAsync(host, port, applicationCancellation.Token);
        clientStream = client.GetStream();

        WriteStatus( $"Spojeno na ONX100 preko {host}:{port}", ConsoleColor.Green);
        WriteStatus("Naredbe: clear, reconnect, exit\n", ConsoleColor.DarkGray);

        // Receive loop neovisno čeka odgovore i unsolicited evente.
        receiveTask = ReceiveLoopAsync(clientStream, connectionCancellation.Token, WriteSimulatorMessage, WriteDisconnectMessage);

        while (!applicationCancellation.IsCancellationRequested)
        {
            // Ako prethodna naredba nije poslana zbog disconnecta,
            // ponovno je šaljemo nakon uspostave nove veze.
            string? command = pendingCommand;
            pendingCommand = null;

            command ??= ReadCommand();
         

            if (command is null || command.Equals("exit", StringComparison.OrdinalIgnoreCase) || command.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                exitRequested = true;
                break;
            }

            if (command.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                Console.Clear();
                WriteStatus($"Spojeno na ONX100 preko {host}:{port}", ConsoleColor.Green);
                WriteStatus("Naredbe: clear, reconnect, exit\n", ConsoleColor.DarkGray);
                continue;
            }

            if (command.Equals("burst", StringComparison.OrdinalIgnoreCase))
            {
                string burstCommands =
                    "VOL 10\r" +
                    "VOL 20\r" +
                    "VOL ?\r" +
                    "MUTE ON\r" +
                    "MUTE ?\r" +
                    "IN 2\r" +
                    "IN ?\r";

                byte[] burstData = Encoding.ASCII.GetBytes(burstCommands);

                await clientStream.WriteAsync(burstData, applicationCancellation.Token);
                await clientStream.FlushAsync(applicationCancellation.Token);

                continue;
            }
            // Ručno prekida trenutnu vezu i otvara novu.
            if (command.Equals("reconnect", StringComparison.OrdinalIgnoreCase))
            {
                WriteStatus( "Ponovno povezivanje...", ConsoleColor.DarkYellow);
                break;
            }

            if (string.IsNullOrWhiteSpace(command))
                continue;

            // Ako je receive loop završio, simulator je vjerojatno
            // zatvorio vezu zbog idle timeouta.
            if (receiveTask.IsCompleted)
            {
                WriteStatus( "Veza nije aktivna. Ponovno povezivanje...", ConsoleColor.DarkYellow);

                // Nakon reconnecta pokušat ćemo poslati istu naredbu.
                pendingCommand = command;
                break;
            }

            try
            {
                // ONX100 naredbe završavaju CR znakom.
                byte[] data = Encoding.ASCII.GetBytes(command + "\r");

                await clientStream.WriteAsync(data, applicationCancellation.Token);
                await clientStream.FlushAsync( applicationCancellation.Token);
            }
            catch (Exception exception)
                when (exception is IOException
                      or SocketException
                      or ObjectDisposedException)
            {
                WriteStatus( $"Slanje nije uspjelo: {exception.Message}", ConsoleColor.Red);

                // Sačuvaj naredbu i pokušaj ponovno nakon reconnecta.
                pendingCommand = command;
                break;
            }
        }
    }
    catch (OperationCanceledException)
        when (applicationCancellation.IsCancellationRequested)
    {
        // Normalno zatvaranje aplikacije.
    }
    catch (SocketException exception)
    {
        WriteStatus( $"TCP greška: {exception.Message}", ConsoleColor.Red);
    }
    finally
    {
        connectionCancellation.Cancel();

        if (receiveTask is not null)
        {
            try
            {
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Normalno zaustavljanje receive loopa.
            }
        }

        if (clientStream is not null)
        {
            await clientStream.DisposeAsync();
        }
    }

    // Kratka pauza prije ponovnog povezivanja.
    if (!exitRequested && !applicationCancellation.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(500, applicationCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Aplikacija se zatvara.
        }
    }
}

WriteStatus("Klijent je zatvoren.", ConsoleColor.DarkGray);

string? ReadCommand()
{
    lock (consoleLock)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("> ");

        // Console.ReadLine ispisuje uneseni tekst trenutnom bojom.
        Console.ForegroundColor = ConsoleColor.Yellow;
    }

    string? command = Console.ReadLine();

    lock (consoleLock)
    {
        Console.ResetColor();
    }

    return command;
}

void WriteSimulatorMessage(string message)
{
    lock (consoleLock)
    {
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"< {message}");

        // Ponovno ispisujemo prompt jer odgovor može stići
        // dok korisnik upravo upisuje naredbu.
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("> ");

        Console.ForegroundColor = ConsoleColor.Yellow;
    }
}

void WriteDisconnectMessage(string message)
{
    lock (consoleLock)
    {
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("> ");

        Console.ForegroundColor = ConsoleColor.Yellow;
    }
}

void WriteStatus(string message, ConsoleColor color)
{
    lock (consoleLock)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}

static async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken, Action<string> onMessage, Action<string> onDisconnect)
{
    byte[] buffer = new byte[1024];
    var pendingString = new StringBuilder();

    try
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead = await stream.ReadAsync(
                buffer,
                cancellationToken);

            // ReadAsync vraća 0 kada simulator zatvori vezu.
            if (bytesRead == 0)
            {
                onDisconnect("ONX100 je zatvorio vezu.");
                return;
            }

            pendingString.Append(
                Encoding.ASCII.GetString(
                    buffer,
                    0,
                    bytesRead));

            // Jedan TCP read može sadržavati dio poruke
            // ili više CRLF-završenih poruka.
            while (true)
            {
                string current = pendingString.ToString();

                int terminatorIndex = current.IndexOf(
                    "\r\n",
                    StringComparison.Ordinal);

                if (terminatorIndex < 0)
                {
                    break;
                }

                string message = current[..terminatorIndex];

                pendingString.Remove(
                    0,
                    terminatorIndex + 2);

                onMessage(message);
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Normalno zatvaranje.
    }
    catch (IOException exception)
    {
        onDisconnect(
            $"Veza prema ONX100 je prekinuta: {exception.Message}");
    }
    catch (SocketException exception)
    {
        onDisconnect(
            $"TCP veza je prekinuta: {exception.Message}");
    }
}