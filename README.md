# ONX-100 upravljački program i upravljačka aplikacija

Višekratno upotrebljiv asinkroni C#/.NET upravljački program i web-aplikacija za upravljanje simuliranim ONX-100 AV uređajem.

Repozitorij sadrži:

- tipizirani .NET upravljački program za ONX-100 ASCII-over-TCP protokol
- ASP.NET Core API koji upravlja vezom prema uređaju i izlaže REST krajnje točke
- React sučelje za upravljanje uređajem i praćenje njegova stanja
- SignalR ažuriranja stanja u stvarnom vremenu
- dijagnostičke i demonstracijske aplikacije
- automatizirane testove upravljačkog programa te skripte za buildanje i pokretanje na više platformi

Protokol je istražen metodom crne kutije korištenjem dostavljenog simulatora te uspoređen s izvatkom proizvođačke protokolne dokumentacije. Detaljna opažanja nalaze se u dokumentu [PROTOCOL.md](PROTOCOL.md).

## Arhitektura

```text
Preglednik / React
    │
    ├── REST API
    └── SignalR
         │
         ▼
Onx100.Api (ASP.NET Core)
         │
         ▼
Onx100.Driver
         │ TCP 127.0.0.1:4999
         ▼
ONX-100 simulator / uređaj
```

`Onx100.Api` jedini upravlja TCP sesijom. Preglednik nikada ne komunicira izravno sa simulatorom.

## Struktura repozitorija

```text
Onx100.sln
├── Onx100.Driver
├── Onx100.Driver.Tests
├── Onx100.ProtocolConsole
├── Onx100.Demo
├── Onx100.Api
├── Onx100.Web
├── scripts
├── PROTOCOL.md
├── BUILDING.md
└── README.md
```

| Projekt | Namjena |
|---|---|
| `Onx100.Driver` | Višekratno upotrebljiva biblioteka klasa koja sadrži javni API upravljačkog programa i implementaciju protokola. |
| `Onx100.Driver.Tests` | xUnit testovi jedinica koda, konkurentnosti, životnog ciklusa i robusnosti. |
| `Onx100.ProtocolConsole` | Konzolni alat niske razine za ručno istraživanje protokola i dijagnostiku. |
| `Onx100.Demo` | Minimalna primjerna aplikacija koja prikazuje korištenje upravljačkog programa i oporavak nakon grešaka. |
| `Onx100.Api` | ASP.NET Core REST API, SignalR hub, servisni sloj uređaja i poslužitelj produkcijske React verzije. |
| `Onx100.Web` | React, TypeScript i Vite upravljačko sučelje. |

## Implementirane funkcionalnosti

### Upravljački program

- asinkrono spajanje i prekid veze
- obavezni `*HELLO` protokolni handshake
- odbijanje `*BUSY` sesija
- uključivanje i isključivanje uređaja uz praćenje prijelaznih stanja
- odabir i dohvat aktivnog ulaza
- postavljanje i dohvat glasnoće
- postavljanje i dohvat mute stanja
- događaji promjene veze i stanja uređaja
- obrada neželjenih događaja napajanja i signala
- serijalizirano i thread-safe izvršavanje javnih naredbi
- obrada fragmentiranih i višestrukih TCP poruka
- isteci vremena za naredbe i prijelaze napajanja
- izričita podrška za ponovno spajanje
- uredno oslobađanje resursa putem `IAsyncDisposable`

### API servis

- jedna instanca `Onx100Device` koja jedina upravlja vezom
- automatsko uspostavljanje veze prije operacija nad uređajem
- serijalizirane API operacije i postupci oporavka
- REST krajnje točke za stanje, vezu, napajanje, ulaz, glasnoću i mute
- strukturirani HTTP odgovori za greške
- oporavak nakon isteka vremena pri upitima i postavljanju vrijednosti
- SignalR slanje promjena veze i stanja uređaja
- posluživanje ugrađenog React sučelja

### React sučelje

- spajanje i prekid veze
- osvježavanje stanja
- uključivanje i isključivanje uređaja
- odabir ulaza
- upravljanje glasnoćom
- uključivanje i isključivanje mute funkcije
- prikaz stanja veze, napajanja, ulaza, glasnoće, mute funkcije i signala
- SignalR ažuriranja u stvarnom vremenu
- prikaz učitavanja i prijelaznih stanja
- onemogućavanje kontrola kada operacija nije dostupna
- razumljive poruke grešaka za korisnika

## Preduvjeti

- **.NET 9 SDK**
- **Node.js i npm**
- dostavljeni ONX-100 simulator za integracijsko testiranje
- TCP pristup simulatoru na `127.0.0.1:4999`

## Brzi početak

### 1. Buildanje i testiranje

Windows PowerShell:

```powershell
.\scripts\build.ps1
```

Bash, Git Bash, Linux ili macOS:

```bash
./scripts/build.sh
```

Build skripte:

1. instaliraju frontend ovisnosti naredbom `npm ci`
2. izrađuju produkcijsku React verziju
3. kopiraju frontend u `Onx100.Api/wwwroot`
4. obnavljaju .NET ovisnosti
5. buildaju cijelo rješenje u konfiguraciji Release
6. pokreću testove upravljačkog programa

Automatizirani skup trenutačno sadrži **103 uspješna testa**, bez neuspješnih ili preskočenih testova.

### 2. Pokretanje simulatora

Pokrenuti dostavljeni simulator i provjeriti:

- da sluša na `127.0.0.1:4999`
- da nijedan drugi klijent nije spojen

Simulator podržava samo jednu aktivnu TCP vezu.

### 3. Pokretanje integrirane aplikacije

Windows PowerShell:

```powershell
.\scripts\run-api.ps1
```

Bash, Git Bash, Linux ili macOS:

```bash
./scripts/run-api.sh
```

Otvoriti URL koji ispiše ASP.NET Core. React sučelje, REST API i SignalR hub poslužuju se iz istog procesa.

## Dodatni razvojni alati

### React razvojni poslužitelj

Za razvoj frontenda uz Vite automatsko ponovno učitavanje potrebno je pokrenuti API u jednom terminalu, a razvojni frontend poslužitelj u drugom:

```powershell
.\scripts\run-api.ps1
.\scripts\run-web.ps1
```

ili:

```bash
./scripts/run-api.sh
./scripts/run-web.sh
```

Vite razvojni poslužitelj prosljeđuje zahtjeve za `/api` i `/hubs` prema `Onx100.Api`.

### Demo aplikacija

```powershell
.\scripts\run-demo.ps1
```

ili:

```bash
./scripts/run-demo.sh
```

Demo se spaja na simulator, uključuje uređaj, odabire ulaz `2`, postavlja glasnoću na `50`, isključuje mute, provjerava završno stanje i uredno prekida vezu.

### Protokolna konzola

```powershell
.\scripts\run-protocol-console.ps1
```

ili:

```bash
./scripts/run-protocol-console.sh
```

Protokolna konzola omogućuje ručno slanje sirovih ONX-100 naredbi, dok se odgovori i neželjeni događaji primaju asinkrono.

## REST API

| Metoda | Krajnja točka | Namjena |
|---|---|---|
| `GET` | `/api/device/state` | Vraća trenutačno poznato stanje bez prisilnog upita prema uređaju. |
| `POST` | `/api/device/refresh` | Dohvaća svježe stanje s uređaja i vraća rezultat. |
| `POST` | `/api/device/connect` | Uspostavlja vezu s uređajem. |
| `POST` | `/api/device/disconnect` | Prekida vezu s uređajem. |
| `POST` | `/api/device/power/on` | Uključuje uređaj. |
| `POST` | `/api/device/power/off` | Isključuje uređaj. |
| `PUT` | `/api/device/input/{input}` | Odabire ulaz od `1` do `4`. |
| `PUT` | `/api/device/volume/{volume}` | Postavlja glasnoću od `0` do `100`. |
| `PUT` | `/api/device/mute/{enabled}` | Uključuje ili isključuje mute. |

SignalR hub dostupan je na:

```text
/hubs/device
```

Promjene stanja šalju se porukom `DeviceStateChanged`.

## API odgovori za greške

API pretvara greške upravljačkog programa i transportnog sloja u strukturirane HTTP odgovore:

| HTTP status | Kod | Značenje |
|---|---|---|
| `400` | `invalid_argument` | Neispravna vrijednost ulaza ili glasnoće. |
| `409` | `device_command_error` | Uređaj je odbio protokolnu naredbu. |
| `409` | `invalid_device_state` | Tražena operacija nije dopuštena u trenutačnom stanju uređaja. |
| `503` | `device_unavailable` | Greška veze ili transportnog sloja. |
| `504` | `device_timeout` | Istek vremena naredbe ili prijelaza napajanja. |
| `500` | `internal_error` | Neočekivana greška na poslužitelju. |

## Osnovni primjer korištenja upravljačkog programa

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

await device.ConnectAsync();
await device.PowerOnAsync();
await device.SelectInputAsync(2);
await device.SetVolumeAsync(50);
await device.SetMuteAsync(false);

Onx100DeviceState state = device.DeviceState;

await device.DisconnectAsync();
```

Sve javne asinkrone operacije prihvaćaju neobavezni `CancellationToken`.

## Arhitektura upravljačkog programa

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
Onx100Device stanje i događaji
```

### Životni ciklus veze

`ConnectAsync` završava tek nakon što simulator pošalje obavezni handshake:

```text
*HELLO ONX-100 FW:2.13
```

Odgovor `*BUSY` znači da drugi klijent već koristi jedinu dostupnu sesiju. Poruka `BYE` nakon neaktivnosti, prisilno zatvaranje socketa i ostali udaljeni prekidi veze prekidaju operacije na čekanju te upravljački program prelazi u stanje `Disconnected`.

### Prijelazi napajanja

Promjene napajanja su asinkrone:

```text
OFF -> WARM -> ON
ON  -> COOL -> OFF
```

`PWR ON` i `PWR OFF` vraćaju `OK` prije dovršetka fizičkog prijelaza. Odgovarajuća operacija upravljačkog programa završava tek nakon završnog događaja `EVT PWR ON` ili `EVT PWR OFF`.

### Pravila isteka vremena i oporavka

Protokol nema identifikatore zahtjeva. Zakašnjeli odgovor naredbe kojoj je isteklo vrijeme ne smije se prihvatiti kao odgovor na neku kasniju naredbu. Zbog toga istek vremena ili otkazivanje nakon slanja naredbe poništava trenutačnu sesiju.

API servis primjenjuje pravila oporavka prilagođena vrsti operacije:

- upiti se ponovno spajaju i pokušavaju još jednom
- operacije postavljanja ponovno se spajaju, dohvaćaju stvarno stanje i ponavljaju naredbu samo ako je potrebno
- operacije napajanja ponovno se spajaju i provjeravaju trenutačno stanje napajanja prije nastavka

## Pokrivenost testovima

Skup testova između ostalog pokriva:

- fragmentaciju poruka i više poruka u jednom čitanju
- parsiranje protokola i formatiranje naredbi
- valjane odgovore i `ERR 01/02/03`
- neželjene događaje signala i napajanja
- serijalizaciju naredbi i stres-testove konkurentnosti
- odbačene i zakašnjele odgovore
- otkazivanje prije i nakon slanja naredbe
- udaljene prekide veze i `BYE`
- `*BUSY` i izostanak `*HELLO` poruke
- neispravne i nepoznate poruke
- race condition situacije između potvrde naredbe i događaja napajanja
- oslobađanje resursa tijekom aktivnih operacija
- ponovno spajanje nakon neaktivnosti ili prisilnog prekida

Ručna provjera dodatno je obuhvatila ponovljene cikluse spajanja, upita i prekida veze, ponovno pokretanje simulatora, odbačene odgovore, postupke oporavka te integriranu React/API aplikaciju.

## Poznata ograničenja

- simulator podržava samo jednu aktivnu TCP vezu
- operacije nad ulazom nisu dostupne dok je uređaj isključen ili u stanju zagrijavanja odnosno hlađenja
- izostanak završnog događaja napajanja može uzrokovati istek vremena iako je simulator promijenio stanje
- ponovno spajanje neposredno nakon takvog isteka vremena može privremeno dobiti `*BUSY` dok simulator ne oslobodi prethodnu sesiju
- upravljački program niske razine namjerno prepušta pravila automatskog ponavljanja pozivatelju; API i demo imaju vlastita sigurna pravila oporavka
- protokolna konzola dijagnostički je alat i nije dio javnog integracijskog sučelja

## Dodatna dokumentacija

- [BUILDING.md](BUILDING.md) — upute za buildanje, testiranje i pokretanje
- [PROTOCOL.md](PROTOCOL.md) — opaženo ponašanje protokola i posljedice za implementaciju
