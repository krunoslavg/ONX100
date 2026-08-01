using System.Net.Sockets;
using System.Text;

const string host = "127.0.0.1";
const int port = 4999;

// TCP klijent koji održava vezu prema ONX100 simulatoru.
using var client = new TcpClient();

// CancellationTokenSource koristimo za kontrolirano zaustavljanje
// glavne petlje i asinkrone petlje za primanje poruka.
using var cancellation = new CancellationTokenSource();

// Ctrl+C ne prekida program odmah, nego prvo šalje signal
// svim asinkronim operacijama da se uredno zaustave.
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    Console.WriteLine($"Spajanje na {host}:{port}...");

    // Otvara TCP vezu prema simulatoru.
    await client.ConnectAsync(host, port, cancellation.Token);

    Console.WriteLine($"Spojeno na ONX100 preko {host}:{port}");
    Console.WriteLine("Upiši ONX naredbu, 'clear' za čišćenje konzole ili 'exit' za izlaz.\n");

    // NetworkStream koristimo za slanje i primanje podataka preko TCP veze.
    await using NetworkStream stream = client.GetStream();

    // Primanje poruka radi u zasebnom asinkronom tasku kako bi konzola
    // istovremeno mogla prihvaćati korisničke naredbe.
    Task receiveTask = ReceiveLoopAsync(stream, cancellation.Token);

    while (!cancellation.IsCancellationRequested)
    {
        Console.Write("> ");

        string? command = Console.ReadLine();

        // EOF, exit ili quit završavaju rad klijenta.
        if (command is null ||
            command.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        // Lokalna naredba: ne šalje se simulatoru.
        if (command.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Console.Clear();
            Console.WriteLine($"Spojeno na ONX100 preko {host}:{port}");
            Console.WriteLine("Upiši ONX naredbu, 'clear' za čišćenje konzole ili 'exit' za izlaz.\n");
            continue;
        }

        // Prazan unos se ignorira.
        if (string.IsNullOrWhiteSpace(command))
        {
            continue;
        }

        // ONX100 protokol očekuje ASCII naredbu završenu CR znakom (\r).
        byte[] data = Encoding.ASCII.GetBytes(command + "\r");

        // Šalje cijelu naredbu simulatoru.
        await stream.WriteAsync(data, cancellation.Token);
        await stream.FlushAsync(cancellation.Token);
    }

    // Zaustavlja receive petlju prije zatvaranja TCP veze.
    cancellation.Cancel();

    try
    {
        await receiveTask;
    }
    catch (OperationCanceledException)
    {
        // Očekivano ponašanje pri urednom zatvaranju programa.
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nVeza je zatvorena.");
}
catch (SocketException exception)
{
    Console.WriteLine($"TCP greška: {exception.Message}");
}
catch (IOException exception)
{
    Console.WriteLine($"Greška veze: {exception.Message}");
}

static async Task ReceiveLoopAsync(
    NetworkStream stream,
    CancellationToken cancellationToken)
{
    // Buffer predstavlja samo veličinu jednog TCP čitanja.
    // Ne mora biti velik kao cijela poruka jer se nepotpune poruke
    // spremaju u pendingString i nadopunjuju sljedećim readovima.
    byte[] buffer = new byte[1024];

    // Čuva podatke koji su primljeni, ali još ne sadrže cijelu
    // poruku završenu CRLF terminatorom.
    var pendingString = new StringBuilder();

    while (!cancellationToken.IsCancellationRequested)
    {
        // Čeka sljedeći blok podataka sa simulatora.
        int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

        // ReadAsync vraća 0 kada udaljena strana zatvori TCP vezu.
        if (bytesRead == 0)
        {
            Console.WriteLine("\nONX100 je zatvorio vezu!");
            return;
        }

        // Dodaje novoprimljene znakove iza eventualno nepotpune poruke
        // koja je ostala od prethodnog čitanja.
        pendingString.Append(
            Encoding.ASCII.GetString(buffer, 0, bytesRead));

        // Jedan TCP read može sadržavati jednu, više ili samo dio poruke.
        // Zato izdvajamo sve potpune poruke koje trenutno postoje u bufferu.
        while (true)
        {
            string current = pendingString.ToString();

            // Odgovori simulatora završavaju CRLF sekvencom (\r\n).
            int terminatorIndex = current.IndexOf(
                "\r\n",
                StringComparison.Ordinal);

            // Nema još cijele poruke; čekamo sljedeći TCP read.
            if (terminatorIndex < 0)
            {
                break;
            }

            // Izdvaja sadržaj poruke bez CRLF terminatora.
            string message = current[..terminatorIndex];

            // Uklanja obrađenu poruku i njezin CRLF iz pending buffera.
            pendingString.Remove(0, terminatorIndex + 2);

            Console.WriteLine();
            Console.WriteLine($"< {message}");
            Console.Write("> ");
        }
    }
}