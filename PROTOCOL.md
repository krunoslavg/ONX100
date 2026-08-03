# Bilješke o ONX-100 protokolu

Protokol je analiziran metodom obrnutog inženjeringa nad dostavljenim ONX-100 simulatorom i uspoređen s isječkom protokola proizvođača.

## 1. Transport

| Svojstvo            | Opaženo ponašanje                      |
|---------------------|----------------------------------------|
| Transport           | TCP                                    |
| Port                | `4999`                                 |
| Kodiranje           | ASCII                                  |
| Završetak naredbe   | `CR` (`\r`, bajt `0x0D`)               |
| Završetak odgovora  | `CRLF` (`\r\n`)                        |
| Istodobni klijenti  | Samo jedan aktivni klijent             |
| Idle timeout        | Približno 60 sekundi                   |
| Reset idle timeouta | Svaka primljena naredba resetira timer |

### 1.1 Uokvirivanje naredbi

Naredbe moraju završavati znakom `CR`.

```text
PWR ?\r
```

Simulator sprema djelomično primljene TCP podatke dok ne primi `CR`. Naredba zato može biti podijeljena kroz više TCP upisa:

```text
"PW"
"R ?"
"\r"
```

Simulator ih rekonstruira kao:

```text
PWR ?
```

Više naredbi završenih znakom `CR` također se može poslati u jednom TCP upisu. Obrađuju se redom, a odgovori se vraćaju istim redoslijedom.

### 1.2 `LF` i `CRLF`

Samostalni `LF` (`\n`) nije delimiter naredbe. Podaci ostaju u međuspremniku dok naknadno ne stigne `CR`.

Naredba završena s `CRLF` obrađuje se čim je primljen `CR`, ali preostali `LF` može ostati u ulaznom međuspremniku simulatora i onečistiti sljedeću naredbu.

Za pouzdanu komunikaciju naredbe treba slati isključivo s `CR` završetkom.

## 2. Životni ciklus veze

### 2.1 Pozdravna poruka veze

Odmah nakon svake uspješne TCP veze simulator šalje:

```text
*HELLO ONX-100 FW:2.13
```

To je neželjena poruka na razini veze, a ne odgovor na naredbu.

Driver ovu poruku treba tretirati kao protokolni handshake. Sama uspješna TCP veza ne dokazuje da je ONX-100 sesija prihvaćena.

### 2.2 Drugi klijent

Simulator dopušta samo jedan aktivni TCP klijent.

Ako se drugi klijent spoji dok je sesija već aktivna, prima:

```text
*BUSY
```

Simulator zatim zatvara vezu drugog klijenta. Izvorna veza ostaje aktivna.

Driver treba tretirati `*BUSY` kao odbijeni pokušaj povezivanja, zatvoriti vlastiti transport i prepustiti retry/backoff politiku pozivatelju.

### 2.3 Prekid zbog neaktivnosti

Nakon približno 60 sekundi bez prometa sa strane klijenta simulator zatvara TCP sesiju. Slanje bilo koje naredbe resetira idle timer.

Kod urednog prekida zbog neaktivnosti opaženo je slanje poruke:

```text
BYE
```

prije zatvaranja veze.

### 2.4 Prisilno gašenje simulatora

Kada se simulator prekine s `Ctrl+C`, ne šalje `BYE` ni drugu protokolnu poruku. Klijent detektira prekid na razini transporta, primjerice:

```text
Unable to read data from the transport connection:
An existing connection was forcibly closed by the remote host.
```

## 3. Sintaksa naredbi

Parser je strog:

- nazivi naredbi razlikuju velika i mala slova
- parametri razlikuju velika i mala slova
- očekuje se točno jedan razmak između naredbe i parametra
- početni razmaci se odbijaju
- završni razmaci se odbijaju
- naredbe se ne trimaju niti normaliziraju

Primjeri:

| Ulaz     | Rezultat |
|----------|----------|
| `PWR ?`  | Valjano  |
| `pwr ?`  | `ERR 01` |
| `PWR on` | `ERR 02` |
| `PWR?`   | `ERR 01` |
| `PWR  ?` | `ERR 02` |
| ` PWR ?` | `ERR 01` |
| `PWR ? ` | `ERR 02` |

## 4. Napajanje

### 4.1 Naredbe

```text
PWR ON
PWR OFF
PWR ?
```

### 4.2 Odgovori na query

Opažena stanja napajanja:

```text
PWR OFF
PWR WARM
PWR ON
PWR COOL
```

### 4.3 Stroj stanja

```text
OFF -> WARM -> ON
ON  -> COOL -> OFF
```

Opažena trajanja:

| Prijelaz                   | Približno trajanje |
|----------------------------|--------------------|
| `PWR ON` do `EVT PWR ON`   | 11–12 sekundi      |
| `PWR OFF` do `EVT PWR OFF` | 7–8 sekundi        |

Setter odmah vraća `OK`, ali stvarni prijelaz završava tek kada stigne odgovarajući event:

```text
PWR ON
OK
...
EVT PWR ON
```

```text
PWR OFF
OK
...
EVT PWR OFF
```

Tijekom prijelaza:

- `PWR ?` vraća `PWR WARM` ili `PWR COOL`
- `IN ?` i input setteri vraćaju `ERR 03`
- `VOL` naredbe ostaju dostupne
- `MUTE` naredbe ostaju dostupne

Slanje istog power settera dok je uređaj već u traženom stanju ili se već kreće prema tom stanju vraća `OK`. Novi power event možda se neće poslati ako nije došlo do stvarne promjene stanja.

## 5. Odabir ulaza

### 5.1 Naredbe

```text
IN 1
IN 2
IN 3
IN 4
IN ?
```

### 5.2 Ponašanje

Kada je uređaj potpuno uključen:

- `IN <1-4>` vraća `OK`
- `IN ?` vraća `IN <1-4>`

Primjer:

```text
IN 3
OK
IN ?
IN 3
```

Input funkcije nisu dostupne dok je uređaj:

- isključen
- u zagrijavanju
- u hlađenju

U tim stanjima input naredbe vraćaju:

```text
ERR 03
```

Nije opažen zaseban `EVT IN ...` event.

## 6. Glasnoća

### 6.1 Naredbe

```text
VOL <0-100>
VOL ?
```

### 6.2 Decimalni setter, heksadecimalni query

Setter prihvaća decimalnu vrijednost:

```text
VOL 60
OK
```

Query vraća trenutačnu vrijednost kao heksadecimalni tekst:

```text
VOL ?
VOL 3C
```

Primjeri:

| Decimalna glasnoća | Odgovor na query |
|--------------------|------------------|
| `1`                | `VOL 01`         |
| `33`               | `VOL 21`         |
| `40`               | `VOL 28`         |
| `60`               | `VOL 3C`         |

Driver mora parsirati payload odgovora kao heksadecimalnu vrijednost.

Naredbe za glasnoću rade dok je uređaj:

- isključen
- u zagrijavanju
- uključen
- u hlađenju

Nije opažen zaseban event za glasnoću.

## 7. Isključivanje zvuka

### 7.1 Naredbe

```text
MUTE ON
MUTE OFF
MUTE ?
```

### 7.2 Ponašanje

```text
MUTE ON
OK

MUTE ?
MUTE ON
```

```text
MUTE OFF
OK

MUTE ?
MUTE OFF
```

Ponovno slanje istog stanja i dalje vraća `OK`.

Nevaljani oblici poput sljedećih vraćaju `ERR 02`:

```text
MUTE
MUTE MAYBE
MUTE 1
MUTE on
```

Mute naredbe rade dok je uređaj:

- isključen
- u zagrijavanju
- uključen
- u hlađenju

Nije opažen zaseban `EVT MUTE ...` event.

## 8. Odgovori s greškom

| Greška   | Opaženo značenje                                   |
|----------|----------------------------------------------------|
| `ERR 01` | Nepoznata naredba ili nevaljan oblik naredbe       |
| `ERR 02` | Nevaljan parametar ili nevaljano formatiranje      |
| `ERR 03` | Naredba nije dostupna u trenutačnom stanju uređaja |

Primjeri:

```text
pwr ?
ERR 01
```

```text
MUTE on
ERR 02
```

```text
PWR OFF
IN ?
ERR 03
```

## 9. Neželjeni eventi

### 9.1 Power eventi

```text
EVT PWR ON
EVT PWR OFF
```

Oni označavaju završetak odgovarajućeg prijelaza stanja napajanja.

### 9.2 Signal eventi

Simulator šalje signal evente neovisno o naredbama klijenta:

```text
EVT SIGNAL 1 OK
EVT SIGNAL 1 LOST
EVT SIGNAL 2 OK
EVT SIGNAL 2 LOST
EVT SIGNAL 3 OK
EVT SIGNAL 3 LOST
EVT SIGNAL 4 OK
EVT SIGNAL 4 LOST
```

Signal event može stići između naredbe i njezina odgovora. Driver ih mora klasificirati kao neželjene evente i ne smije ih potrošiti kao odgovor na aktivnu naredbu.

## 10. Pouzdanost odgovora

Simulator može namjerno odbaciti odgovore.

Opaženi primjeri uključuju:

```text
response dropped: PWR OFF
```

i izgubljeni `OK` odgovor nakon valjanog settera.

Odbačeni odgovor ne zatvara nužno TCP vezu. Uređaj je mogao obraditi setter čak i ako klijent nije primio potvrdu.

U jednom testu odbačen je 1 od 15 valjanih odgovora na `PWR ?`. Ostala pokretanja završila su bez dropova, pa je ponašanje povremeno.

### 10.1 Posljedice na razini uređaja

- svaka naredba mora imati timeout
- timeout ne dokazuje da je uređaj prekinuo vezu
- timeout ne dokazuje da je setter odbijen
- slijepo ponavljanje settera nije sigurno jer je prva naredba možda već primijenjena
- stanje uređaja preživljava TCP reconnect i može se provjeriti u novoj sesiji

### 10.2 Politika sesije drivera

Protokol nema identifikator zahtjeva. Nakon timeouta odgovora zakašnjeli odgovor prethodne naredbe mogao bi stići dok novija naredba čeka isti tip odgovora. Postojeća TCP sesija zato više nije sigurna za korelaciju odgovora.

Driver stoga:

- označava trenutačnu sesiju nevaljanom nakon command timeouta
- zatvara TCP sesiju prije dopuštanja nove naredbe
- zahtijeva eksplicitni reconnect prije daljnjih naredbi
- dopušta ponavljanje queryja tek u novoj sesiji
- ne ponavlja settere automatski
- očekuje da pozivatelj nakon izgubljene potvrde settera napravi reconnect i queryjem provjeri stvarno stanje

Cancellation slijedi isto pravilo kada nastupi nakon početka slanja naredbe. Cancellation dok poziv samo čeka command execution lock ne invalidira aktivnu sesiju.

## 11. Redoslijed odgovora

Kada se više naredbi pošalje zajedno u jednom TCP upisu, simulator ih obrađuje redom.

Primjer burst slanja:

```text
VOL 10\r
VOL 20\r
VOL ?\r
MUTE ON\r
MUTE ?\r
IN 2\r
IN ?\r
```

Opaženi redoslijed odgovora dok je uređaj uključen:

```text
OK
OK
VOL 14
OK
MUTE ON
OK
IN 2
```

Neželjeni eventi i dalje se mogu umetnuti između tih odgovora.

## 12. Trajnost stanja

### 12.1 TCP reconnect

Stanje uređaja preživljava TCP reconnect.

Potvrđene očuvane vrijednosti:

```text
PWR ON
IN 3
VOL 3C
MUTE ON
```

TCP sesija i stanje uređaja zato su međusobno neovisni.

### 12.2 Restart simulatora

Potpuni restart simulatora vraća uređaj na:

```text
PWR OFF
IN 1
VOL 28
MUTE OFF
```

`IN 1` može se dohvatiti tek nakon dovršetka uključivanja uređaja jer input naredbe vraćaju `ERR 03` dok je uređaj isključen.

## 13. Implikacije za implementaciju drivera

Driver treba:

1. Koristiti jednu dugotrajnu receive petlju.
2. Spremati dolazne bajtove dok nisu dostupne potpune poruke završene s `CRLF`.
3. Podržavati fragmentirane poruke i više poruka u jednom čitanju.
4. Slati naredbe isključivo s `CR` završetkom.
5. Serijalizirati javne naredbe jer protokol nema identifikatore zahtjeva.
6. Održavati najviše jedan aktivni pending odgovor na naredbu.
7. Usmjeravati `EVT ...`, `*HELLO`, `*BUSY` i `BYE` odvojeno od odgovora na naredbe.
8. Tretirati uspostavu TCP veze i uspostavu protokolne veze kao dva odvojena koraka.
9. Završiti `ConnectAsync` tek nakon primitka poruke `*HELLO`.
10. Tretirati `*BUSY`, prekid prije `*HELLO` ili handshake timeout kao neuspješan pokušaj povezivanja.
11. Tretirati `*BUSY` kao odbijenu sesiju i prepustiti retry/backoff politiku pozivatelju.
12. Primijeniti timeout na svaku naredbu.
13. Invalidirati i zatvoriti trenutačnu sesiju nakon command timeouta.
14. Invalidirati i zatvoriti sesiju kada cancellation nastupi nakon početka slanja naredbe.
15. Ostaviti sesiju aktivnom ako cancellation nastupi dok poziv samo čeka ulazak u serijalizirani command path.
16. Ponavljati queryje tek nakon reconnecta u novu sesiju.
17. Nikada slijepo ne ponavljati setter čija je potvrda izgubljena; napraviti reconnect i queryjem provjeriti stanje.
18. Modelirati napajanje stanjima `Unknown`, `Off`, `Warming`, `On` i `Cooling`.
19. Završavati power operacije na `EVT PWR ON/OFF`, a ne samo na `OK`.
20. Tretirati završni power event kao autoritativan čak i ako stigne prije potvrde settera.
21. Koristiti dulje timeoutove za power operacije nego za obične naredbe.
22. Parsirati vrijednosti volume queryja kao heksadecimalne.
23. Tretirati `ERR 03` kao grešku stanja ili dostupnosti funkcionalnosti.
24. Odmah završiti pending naredbe i power waitere greškom nakon remote disconnecta, poruke `BYE` ili odbijene sesije.
25. Resetirati framing i command-correlation stanje na granici reconnecta.
26. Izolirati iznimke koje bace korisnički event handleri kako ne bi prekinule receive petlju ili životni ciklus veze.
27. Izbjegavati povezivanje više neovisnih instanci drivera na isti uređaj.
