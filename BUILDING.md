# Buildanje, testiranje i pokretanje

## Preduvjeti

Za buildanje i pokretanje projekta potrebno je:

- instaliran **.NET 9 SDK**
- dostavljeni ONX-100 simulator
- slobodan TCP port `4999`
- terminal otvoren u root direktoriju repozitorija

Instaliranu verziju .NET SDK-a moguće je provjeriti naredbom:

dotnet --version

Projekti ciljaju framework: net9.0

## Skripte

Sve pomoćne skripte nalaze se u direktoriju `scripts` u rootu repozitorija.

Dostupne su PowerShell i Bash varijante:

| Namjena                           | PowerShell                         | Bash                              |
|---                                |---                                 |---                                |
| Restore, Release build i testovi  | `scripts/build.ps1`                | `scripts/build.sh`                |
| Pokretanje demo aplikacije        | `scripts/run-demo.ps1`             | `scripts/run-demo.sh`             |
| Pokretanje protocol console alata | `scripts/run-protocol-console.ps1` | `scripts/run-protocol-console.sh` |

Skripte same određuju root direktorij repozitorija, pa ih nije nužno pokretati iz određenog radnog direktorija.

## Buildanje i testiranje

### Windows PowerShell

```powershell
.\scripts\build.ps1
```

### Bash, Git Bash ili Linux/macOS

```bash
./scripts/build.sh
```

Build skripta izvršava:

1. `dotnet restore`
2. Release build cijelog solutiona
3. pokretanje testnog projekta bez ponovnog buildanja

Očekivani rezultat:

```text
Passed: 103
Failed: 0
Skipped: 0
```

## Pokretanje demo aplikacije

Prije pokretanja:

1. pokrenuti dostavljeni ONX-100 simulator
2. provjeriti da simulator sluša na `127.0.0.1:4999`
3. provjeriti da nijedan drugi klijent nije spojen
4. prethodno izvršiti build skriptu

Simulator dopušta samo jednu aktivnu TCP vezu. Dodatni klijent primit će poruku `*BUSY`, nakon čega će veza biti odbijena.

### Windows PowerShell

```powershell
.\scripts\run-demo.ps1
```

### Bash, Git Bash ili Linux/macOS

```bash
./scripts/run-demo.sh
```

Demo izvršava sljedeći tijek:

1. povezuje se sa simulatorom
2. dovršava `*HELLO` handshake
3. uključuje uređaj
4. odabire ulaz `2`
5. postavlja glasnoću na `50`
6. isključuje mute
7. dohvaća završno stanje uređaja
8. uredno prekida vezu

Uspješno izvršavanje završava s exit codeom `0`.

## Pokretanje protocol console aplikacije

Prije pokretanja potrebno je pokrenuti simulator i izvršiti build skriptu.

### Windows PowerShell

```powershell
.\scripts\run-protocol-console.ps1
```

### Bash, Git Bash ili Linux/macOS

```bash
./scripts/run-protocol-console.sh
```

Protocol console omogućuje:

- ručno slanje sirovih ONX-100 naredbi
- prikaz primljenih odgovora
- asinkrono primanje neželjenih protokolnih događaja
- testiranje framinga, timeouta i ponašanja simulatora

Produkcijsko integracijsko sučelje je projekt `Onx100.Driver`. `Onx100.ProtocolConsole` služi isključivo kao dijagnostički i razvojni alat.

## Ručno pokretanje

Skripte su samo praktični omotači oko standardnih .NET CLI naredbi. Ekvivalentne naredbe moguće je izvršiti i ručno:

```bash
dotnet restore Onx100.sln
dotnet build Onx100.sln -c Release --no-restore
dotnet test Onx100.Driver.Tests/Onx100.Driver.Tests.csproj -c Release --no-build
```

```bash
dotnet run --project Onx100.Demo -c Release --no-build
dotnet run --project Onx100.ProtocolConsole -c Release --no-build
```

## Napomena o simulatoru

ONX-100 simulator nije dio driver biblioteke i mora se pokrenuti zasebno.

Zadane postavke korištene u projektu:

```text
Host: 127.0.0.1
Port: 4999
Transport: TCP
```

Detaljnija opažanja o protokolu dostupna su u dokumentu [PROTOCOL.md](PROTOCOL.md).
