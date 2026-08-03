# ONX-100 .NET driver

Višekratno upotrebljiv asinkroni C#/.NET driver za simulirani ONX-100 AV uređaj.

Driver izlaže tipizirani .NET API za upravljanje vezom, napajanjem, odabirom ulaza, glasnoćom, mute stanjem, stanjem uređaja i eventima promjene stanja. Interno obrađuje ONX-100 ASCII-over-TCP protokol, framing poruka, korelaciju odgovora, neželjene evente, timeoute, prekide veze i granice reconnecta.

Protokol je istražen black-box analizom dostavljenog simulatora i uspoređen s isječkom protokola proizvođača. Detaljna opažanja dokumentirana su u [PROTOCOL.md](PROTOCOL.md).

## Struktura solutiona

```text
Onx100.sln
├── Onx100.Driver
├── Onx100.Driver.Tests
├── Onx100.ProtocolConsole
└── Onx100.Demo
```

| Projekt                   | Namjena                                                                                         |
|---------------------------|-------------------------------------------------------------------------------------------------|
| `Onx100.Driver`           | Višekratno upotrebljiva class library koja sadrži javni API drivera i implementaciju protokola. |
| `Onx100.Driver.Tests`     | xUnit testovi jedinica, konkurentnosti, životnog ciklusa i robusnosti.                          |
| `Onx100.ProtocolConsole`  | Niskorazinski konzolni alat korišten za istraživanje i provjeru sirovog ONX-100 protokola.      |
| `Onx100.Demo`             | Minimalna klijentska aplikacija koja demonstrira uobičajeno korištenje drivera.                 |

## Implementirane funkcionalnosti

- asinkrono povezivanje i prekidanje veze
- obavezni `*HELLO` protokolni handshake
- odbijanje `*BUSY` sesija
- uključivanje i isključivanje uz praćenje prijelaznih stanja
- postavljanje i dohvat odabranog ulaza
- postavljanje i dohvat glasnoće
- postavljanje i dohvat mute stanja
- eventi stanja veze i stanja uređaja
- obrada neželjenih power i signal eventa
- serijalizirano i thread-safe izvršavanje javnih naredbi
- TCP framing fragmentiranih poruka i više poruka u jednom čitanju
- timeoutovi naredbi i prijelaza napajanja
- eksplicitna podrška za reconnect
- čišćenje resursa kroz `IAsyncDisposable`

## Preduvjeti

- .NET 9.0 SDK
- dostavljeni ONX-100 simulator za integracijsko testiranje
- TCP pristup simulatoru, uobičajeno na `127.0.0.1:4999`

## Buildanje i pokretanje
Za upute za buildanje, testiranje i pokretanje pogledajte [BUILDING.md](BUILDING.md).

## Osnovno korištenje drivera

```csharp
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

device.Onx100DeviceStateChanged += (_, eventArgs) =>
{
    Onx100DeviceState state = eventArgs.CurrentState;
    Console.WriteLine($"Power={state.PowerState}, Input={state.SelectedInput}, Volume={state.Volume}, Muted={state.IsMuted}");
};

await device.ConnectAsync();
await device.PowerOnAsync();
await device.SelectInputAsync(2);
await device.SetVolumeAsync(50);
await device.SetMuteAsync(false);

int input = await device.GetSelectedInputAsync();
int volume = await device.GetVolumeAsync();
bool muted = await device.GetMuteAsync();

await device.DisconnectAsync();
```

Sve javne asinkrone operacije prihvaćaju opcionalni `CancellationToken`.

## Pregled javnog API-ja

### Veza i životni ciklus

```csharp
Task ConnectAsync(CancellationToken cancellationToken = default);
Task DisconnectAsync(CancellationToken cancellationToken = default);
ValueTask DisposeAsync();
```

### Napajanje

```csharp
Task<Onx100PowerState> GetPowerStateAsync(CancellationToken cancellationToken = default);
Task PowerOnAsync(CancellationToken cancellationToken = default);
Task PowerOffAsync(CancellationToken cancellationToken = default);
```

### Ulaz

```csharp
Task<int> GetSelectedInputAsync(CancellationToken cancellationToken = default);
Task SelectInputAsync(int input, CancellationToken cancellationToken = default);
```

### Glasnoća

```csharp
Task<int> GetVolumeAsync(CancellationToken cancellationToken = default);
Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default);
```

### Mute

```csharp
Task<bool> GetMuteAsync(CancellationToken cancellationToken = default);
Task SetMuteAsync(bool mute, CancellationToken cancellationToken = default);
```

### Stanje i eventi

```csharp
Onx100ConnectionState ConnectionState { get; }
Onx100DeviceState DeviceState { get; }

event EventHandler<Onx100ConnectionStateChangedEventArgs>? Onx100ConnectionStateChanged;
event EventHandler<Onx100DeviceStateChangedEventArgs>? Onx100DeviceStateChanged;
```

`Onx100DeviceState` sadrži posljednje poznato stanje napajanja, odabrani ulaz, glasnoću, mute stanje i signalno stanje ulaza od `1` do `4`.

## Arhitektura

Dolazni podaci prolaze kroz sljedeće komponente:

```text
TcpOnx100Transport
        ↓
Onx100MessageFramer
        ↓
Onx100ProtocolParser
        ↓
Onx100CommandDispatcher
        ↓
Onx100Device state and events
```

### Transport

`TcpOnx100Transport` upravlja objektima `TcpClient` i `NetworkStream`. Pruža asinkrone operacije povezivanja, slanja, primanja, prekidanja veze i čišćenja resursa.

### Framing poruka

TCP ne čuva granice aplikacijskih poruka. `Onx100MessageFramer` zato podržava:

- jednu poruku podijeljenu kroz više čitanja
- više poruka dostavljenih u jednom čitanju
- `CRLF` terminator podijeljen između čitanja
- zadržavanje nepotpunih podataka za sljedeće čitanje
- reset međuspremnika kada TCP sesija završi

Odlazne naredbe koriste isključivo `CR`, kako zahtijeva simulator.

### Parsiranje i dispatch

`Onx100ProtocolParser` pretvara sirove protokolne poruke u tipizirane protokolne modele. Dispatcher dopušta samo jedan aktivni odgovor na naredbu i sprječava neželjene poruke da potroše pending odgovor.

Protokol nema identifikatore zahtjeva, zbog čega je javno izvršavanje naredbi serijalizirano.

### Stanje i eventi

`Onx100Device` održava posljednje poznato stanje uređaja i podiže tipizirane .NET evente kada se promijeni stanje veze ili uređaja.

Svaki event subscriber poziva se zasebno. Iznimka iz korisničkog koda izolirana je kako ne bi prekinula receive petlju, životni ciklus veze ili obavještavanje ostalih subscribera.

## Životni ciklus veze

`ConnectAsync` ne prijavljuje uspjeh odmah nakon otvaranja TCP socketa. Čeka dok simulator ne pošalje:

```text
*HELLO ONX-100 FW:2.13
```

Stanje veze prelazi u `Connected` tek nakon uspješnog handshakea.

Poruka `*BUSY` znači da drugi klijent već koristi jedinu podržanu sesiju. Driver to tretira kao odbijeni pokušaj povezivanja, zatvara transport i prepušta retry/backoff politiku pozivatelju.

Idle `BYE`, prisilno zatvaranje socketa i ostali udaljeni prekidi veze odmah završavaju pending operacije greškom te prebacuju driver u stanje `Disconnected`.

## Prijelazi napajanja

Promjene napajanja asinkrone su operacije uređaja:

```text
OFF -> WARM -> ON
ON  -> COOL -> OFF
```

`PWR ON` ili `PWR OFF` vraća `OK` prije nego što je fizički prijelaz završen. Driver zato završava `PowerOnAsync` i `PowerOffAsync` tek nakon odgovarajućeg završnog eventa:

```text
EVT PWR ON
EVT PWR OFF
```

`PowerTransitionTimeout` odvojen je od običnog command timeouta.

Završni power event tretira se kao autoritativan čak i kada stigne prije potvrde settera.

## Politika timeouta i cancellationa

Simulator može obraditi naredbu, ali namjerno odbaciti njezin odgovor. TCP veza nakon toga može ostati otvorena.

Međutim, protokol nema identifikator zahtjeva. Zakašnjeli odgovor naredbe koja je timeoutala mogao bi se pogrešno prihvatiti kao odgovor na kasniju naredbu istog tipa. Zbog toga:

- command timeout invalidira i zatvara trenutačnu sesiju drivera
- nova naredba nije dopuštena dok pozivatelj ne napravi reconnect
- cancellation nakon početka slanja slijedi isto pravilo
- cancellation dok poziv samo čeka serijalizirani execution lock ne invalidira aktivnu sesiju

### Timeout queryja

Napraviti reconnect i ponoviti query u novoj sesiji.

### Timeout settera

Ne ponavljati setter naslijepo. Uređaj ga je možda već primijenio prije nego što je potvrda odbačena.

Potrebno je napraviti reconnect i odgovarajućim queryjem provjeriti stvarno stanje.

Primjer oporavka:

```csharp
try
{
    await device.SetVolumeAsync(50);
}
catch (Onx100TimeoutException)
{
    await device.ConnectAsync();
    int actualVolume = await device.GetVolumeAsync();

    Console.WriteLine($"Volume after reconnect: {actualVolume}");
}
```

## Obrada grešaka

Driver izlaže protokolno specifične iznimke u namespaceu `Onx100.Driver.Exceptions`:

| Iznimka                  | Značenje                                                             |
|--------------------------|----------------------------------------------------------------------|
| `Onx100CommandException` | Uređaj je vratio protokolni `ERR` odgovor.                           |
| `Onx100TimeoutException` | Naredba ili prijelaz napajanja prekoračili su konfigurirani timeout. |

Greške transporta i udaljene sesije izlažu se kao I/O iznimke. Pozivanje command metoda dok driver nije spojen završava greškom nevaljane operacije.

Značenja protokolnih grešaka i opaženo ponašanje simulatora dokumentirani su u [PROTOCOL.md](PROTOCOL.md).

## Pokrivenost testovima

Automatizirani testni skup, među ostalim, pokriva:

- fragmentaciju poruka i više poruka u jednom čitanju
- parsiranje protokola i formatiranje naredbi
- valjane odgovore i `ERR 01/02/03`
- neželjene signal i power evente
- serijalizaciju naredbi i concurrency stress test
- odbačene query i setter odgovore
- zakašnjele odgovore nakon timeouta
- cancellation prije i nakon slanja
- udaljeni prekid veze tijekom pending naredbe
- udaljeni prekid veze tijekom prijelaza napajanja
- `BYE` i `*BUSY`
- izostanak `*HELLO` handshakea
- malformed i nepoznate poruke
- race condition između power eventa i potvrde
- dispose tijekom aktivnih operacija
- iznimke iz korisničkih event handlera
- reconnect nakon idle ili prisilnog prekida

Ručna provjera protiv simulatora također je uspješno završena:

- idle disconnect i reconnect
- prekid simulatora i reconnect nakon restarta
- 100 connect/query/disconnect ciklusa
- namjerno odbačeni odgovor tijekom cycle testa
- nepromijenjen broj process handleova kroz cycle test
- završni end-to-end demo s izlaznim kodom `0`

Tijekom tih testova nije opažen deadlock, zaglavljena receive petlja ni indikacija curenja resursa.

## Poznata ograničenja

- simulator podržava samo jedan aktivni TCP klijent
- driver se ne reconnecta automatski niti automatski ponavlja naredbe
- naredba koja timeouta ili bude otkazana nakon početka slanja zahtijeva novu vezu
- rezultat settera neodređen je ako je njegova potvrda izgubljena
- input operacije nisu dostupne dok je uređaj isključen, u zagrijavanju ili u hlađenju
- ponašanje protocol console aplikacije dijagnostičko je i nije dio višekratno upotrebljivog API-ja drivera

## Referenca protokola

Detalji transporta, sintakse naredbi, prijelaza stanja, formata eventa, odgovora s greškom, odbacivanja odgovora i implikacija za implementaciju nalaze se u [PROTOCOL.md](PROTOCOL.md).
