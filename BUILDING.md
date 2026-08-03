# Buildanje, testiranje i pokretanje

## Preduvjeti

Za buildanje i pokretanje cijelog projekta potrebno je:

- instaliran **.NET 9 SDK**
- instalirani **Node.js i npm**
- dostavljeni ONX-100 simulator
- slobodan TCP port `4999`

Provjera instaliranih verzija:

```bash
dotnet --version
node --version
npm --version
```

.NET projekti ciljaju:

```text
net9.0
```

## Struktura izvršavanja

```text
React frontend
    -> REST API + SignalR
Onx100.Api
    -> Onx100.Driver
ONX-100 simulator
    -> TCP 127.0.0.1:4999
```

Produkcijska React verzija kopira se u `Onx100.Api/wwwroot`, pa se cijela aplikacija pokreće iz jednog ASP.NET Core procesa.

## Skripte

Sve pomoćne skripte nalaze se u direktoriju `scripts`.

| Namjena | PowerShell | Bash |
|---|---|---|
| Buildanje frontenda i .NET-a te pokretanje testova | `scripts/build.ps1` | `scripts/build.sh` |
| Pokretanje API-ja i ugrađenog React frontenda | `scripts/run-api.ps1` | `scripts/run-api.sh` |
| Pokretanje Vite razvojnog poslužitelja | `scripts/run-web.ps1` | `scripts/run-web.sh` |
| Pokretanje demo aplikacije | `scripts/run-demo.ps1` | `scripts/run-demo.sh` |
| Pokretanje protokolne konzole | `scripts/run-protocol-console.ps1` | `scripts/run-protocol-console.sh` |

Skripte same određuju korijenski direktorij repozitorija, pa ih nije potrebno pokretati iz određenog radnog direktorija.

## Buildanje i testiranje

### Windows PowerShell

```powershell
.\scripts\build.ps1
```

### Bash, Git Bash, Linux ili macOS

```bash
./scripts/build.sh
```

Build skripta izvršava:

1. `npm ci` u `Onx100.Web`
2. izradu produkcijske React verzije
3. kopiranje `Onx100.Web/dist` u `Onx100.Api/wwwroot`
4. `dotnet restore Onx100.sln`
5. Release build cijelog rješenja
6. pokretanje projekta `Onx100.Driver.Tests`

Očekivani rezultat testova:

```text
Passed: 103
Failed: 0
Skipped: 0
```

Build skriptu treba pokrenuti nakon povlačenja repozitorija i nakon promjena frontenda koje trebaju biti ugrađene u API.

## Pokretanje integrirane aplikacije

Prije pokretanja:

1. pokrenuti dostavljeni ONX-100 simulator
2. provjeriti da sluša na `127.0.0.1:4999`
3. provjeriti da nijedan drugi klijent nije spojen
4. prethodno izvršiti build skriptu

Simulator dopušta samo jednu aktivnu TCP vezu. Dodatni klijent primit će `*BUSY` i veza će biti odbijena.

### Windows PowerShell

```powershell
.\scripts\run-api.ps1
```

### Bash, Git Bash, Linux ili macOS

```bash
./scripts/run-api.sh
```

Skripta pokreće `Onx100.Api` u konfiguraciji Release bez ponovnog buildanja. Otvoriti URL koji ASP.NET Core ispiše uz poruku `Now listening on`.

Na tom URL-u dostupni su:

- React frontend na `/`
- REST API na `/api/device/...`
- SignalR hub na `/hubs/device`

## Razvojni način rada frontenda

`run-web` skripte nisu potrebne za normalno korištenje aplikacije. Koriste se samo tijekom razvoja React frontenda radi Vite automatskog ponovnog učitavanja.

Potrebna su dva terminala.

### Terminal 1 — API

```powershell
.\scripts\run-api.ps1
```

ili:

```bash
./scripts/run-api.sh
```

### Terminal 2 — Vite frontend

```powershell
.\scripts\run-web.ps1
```

ili:

```bash
./scripts/run-web.sh
```

Otvoriti URL koji Vite ispiše. Razvojni proxy prosljeđuje zahtjeve za `/api` i `/hubs` prema `Onx100.Api`.

## Pokretanje demo aplikacije

Prije pokretanja potrebno je pokrenuti simulator, osigurati da drugi klijent nije spojen i izvršiti build skriptu.

### Windows PowerShell

```powershell
.\scripts\run-demo.ps1
```

### Bash, Git Bash, Linux ili macOS

```bash
./scripts/run-demo.sh
```

Demo:

1. uspostavlja vezu i dovršava `*HELLO` handshake
2. uključuje uređaj
3. odabire ulaz `2`
4. postavlja glasnoću na `50`
5. isključuje mute
6. dohvaća završno stanje
7. uredno prekida vezu

Demo sadrži logiku oporavka za namjerno odbačene odgovore simulatora.

## Pokretanje protokolne konzole

### Windows PowerShell

```powershell
.\scripts\run-protocol-console.ps1
```

### Bash, Git Bash, Linux ili macOS

```bash
./scripts/run-protocol-console.sh
```

Protokolna konzola omogućuje:

- ručno slanje sirovih ONX-100 naredbi
- prikaz primljenih odgovora
- asinkrono primanje neželjenih protokolnih događaja
- dijagnostiku uokvirivanja poruka, isteka vremena i ponašanja simulatora

`Onx100.ProtocolConsole` nije produkcijsko integracijsko sučelje. Za integraciju se koristi `Onx100.Driver`, odnosno `Onx100.Api` za klijente u pregledniku.

## Konfiguracija simulatora

Zadane postavke nalaze se u `Onx100.Api/appsettings.json`:

```text
Host: 127.0.0.1
Port: 4999
ConnectionTimeout: 5 s
CommandTimeout: 3 s
PowerTransitionTimeout: 20 s
```

## Česti problemi

### `*BUSY`

Drugi klijent već koristi jedinu TCP sesiju simulatora. Zaustaviti API, Demo, ProtocolConsole ili drugi spojeni alat i pokušati ponovno.

### Frontend ne prikazuje najnovije promjene

Ponovno izvršiti `build.ps1` ili `build.sh`. Skripta ponovno builda React i osvježava `Onx100.Api/wwwroot`.

### `npm` nije pronađen

Instalirati Node.js, zatvoriti i ponovno otvoriti terminal te provjeriti `node --version` i `npm --version`.

### `dotnet` nije pronađen

Instalirati .NET 9 SDK i provjeriti `dotnet --version`.

### Bash skripta nema dozvolu za izvršavanje

```bash
chmod +x scripts/*.sh
```

Skriptu je moguće pokrenuti i izravno:

```bash
bash scripts/build.sh
```

## Napomena o publish paketu

Zaseban publish paket nije potreban za predaju repozitorija. Evaluator može povući repozitorij, izvršiti build skriptu i zatim pokrenuti `run-api` skriptu.

Detaljna opažanja o protokolu dostupna su u [PROTOCOL.md](PROTOCOL.md).
