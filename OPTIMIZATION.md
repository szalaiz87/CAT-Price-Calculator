# Optimalizáció – v1.1.0-beta.7

A FedEx és magyar MyGLS integráció beépítése után az ismétlődő felületet, hitelesítést és válaszfeldolgozást összevontuk. A számítások, árfolyamok, frissítési csatorna, titkos mentések, két téma, kézi csomagműveletek és 48 órás helyi tisztítás megmarad.

| Mért adat | beta.6 / korábbi módszer | beta.7 / közös módszer |
| --- | ---: | ---: |
| Önálló Windows x64 EXE | 64,30 MB | 64,32 MB; kb. +18,5 kB (+0,03%) |
| Windows letöltési ZIP | 58,19 MB | 58,20 MB; kb. +16,1 kB (+0,03%) |
| Allokált .NET-memória, 20 × kb. 1 MiB JSON-válasz, meleg pufferrel | 62 946 024 bájt | 10 024 bájt (−99,98%) |
| Ugyanezen helyi JSON-feldolgozás medián ideje | 30,710 ms | 3,609 ms |

MB = 1 000 000 bájt, kB = 1000 bájt. Az új két integráció mellett a csomagméret gyakorlatilag változatlan; kisebb EXE-t ebben a kiadásban nem állítunk. A build metaadata a pontos méretet pár bájttal változtathatja. Nincs új külső csomag, fotó, logóletöltés, runtime trimming vagy AOT.

A JSON-mérés az előző DHL/UPS `ReadAsStringAsync + JsonDocument.Parse(string)` megoldását hasonlítja az új `ReadAsStreamAsync + JsonDocument.ParseAsync` úthoz, ugyanazon előre elkészített 1 048 661 bájtos UTF-8 adaton. 10 bemelegítő pár, öt mérési pár váltott sorrenddel, páronként 20 válasz, mérés előtt GC, `GC.GetTotalAllocatedBytes(true)` különbség. Mindkét út azonos mezőt ellenőriz a létrehozott dokumentumban. A bemelegítés után az UTF-8 út visszaadott pool-puffereket használ; a szöveges úton a teljes UTF-16 másolat és új UTF-8 dokumentum létrejön. A poolban megtartott memória nem tűnik el; a szám **műveletenkénti új allokáció**, nem RSS vagy teljes app RAM. Nincs hálózati átvitel, WPF, céges API-kulcs vagy logfájl a mérésben. A helyi parser időeredménye nem jelent gyorsabb internetet vagy százalékos Windows-app sebességígéretet.

Nyers adatok: [OPTIMIZATION-BETA7-MEASUREMENTS.json](OPTIMIZATION-BETA7-MEASUREMENTS.json). Ismétlés:

```text
dotnet run --project tests/CatPriceCalculator.Checks -c Release -- --benchmark-tracking-json
```

Megvalósítás:

- **Egy futárbeállítás-szerkesztő** a korábbi két külön nézet helyén, most mind a négy szolgáltatóhoz. A mentés, törlés, kézi teszt, maszkolás és visszajelzés közös. Futáronként külön megmaradó szerkesztési draft és tesztállapot; a háttérben befejeződő kézi teszt nem jelenhet meg másik futár alatt. A DHL szolgáltatáslista egyszer készül el, nem minden fülváltáskor.
- **UPS/FedEx közös OAuthTrackingClient**, egységes tokencache/lejárat/egyszeri 401-utáni újítás. UPS HTTP Basic és FedEx form-kulcsok továbbra is saját, kérésenkénti adapterben. Nincs automatikus tokenfrissítés vagy háttérhívás.
- **ManualTrackingSession** az UPS/FedEx/GLS közös kézi sorosításához, naplóhoz, HTTP-429 várakozáshoz és biztonságos hibakezeléshez. Futáronkénti kvóta és memóriatoken, egy közös 20 s / 2 MB / redirect nélküli HttpClient; egy futár hibája nem állítja le a többit. GLS csomagszintű jogosultságmegtagadás sem állítja le a többi GLS-sort.
- **TrackingJson** közös UTF-8 stream olvasás és mezőkezelés a négy futárhoz, teljes köztes válaszszöveg nélkül. A HttpClient választest-pufferelése megmarad az időkorlát és 2 MB-os határ érvényesítéséhez. Nyers választ/titkot nem naplózunk.
- **Egy meglévő fotó és két közös fejlécstílus**: Robo Sanyi és a két kalkulátor ugyanazt a képerőforrást használja. Nincs új raster fájl vagy fülváltáskori képgenerálás.
- A beta.6 atomi UTF-8 tárolása, legfeljebb 200-as naplócache, lapozása és rejtett csomagoldali táblázatmunka-csökkentése megmarad.

Ellenőrzés: **503 automatizált eset** és hibamentes Windows x64 self-contained publish. Új FedEx/GLS hivatalos kérések, hitelesítés, token/cooldown, SHA-512 numerikus bájttömb, védett mentés, hibák, státuszok, hely-idő párosítás, WCF/ISO idő, ETA hiányának kezelése, megőrzés, naplótitkok és négyfutáros közös HTTP-fejlécek ellenőrizve. XAML fordítás és fix méretek/megőrzött keret ellenőrzése. A tényleges Windows WPF/DPAPI-futtatás és céges kulcsos élő próba Linuxon nem történt; teljes Windows-folyamat RAM/CPU/indulási idő nem mérhető itt.

---

## Korábbi beta.6 eredmények

# Optimalizáció – v1.1.0-beta.6

Összehasonlítás: az utolsó kiadott v1.1.0-beta.5 és a beta.6 Release build. Minden felhasználói funkció, kalkuláció, téma, mentett beállítás, kézi csomagkövetés, titkosítás, 48 órás megőrzés és frissítési csatorna megmarad.

## Mérhető eredmény

| Mért adat | beta.5 | beta.6 | Csökkenés |
| --- | ---: | ---: | ---: |
| Windows x64, önálló, tömörített EXE | 67,73 MB | 64,30 MB | 5,1% / kb. 3,42 MB |
| EXE-t tartalmazó Windows ZIP | 61,60 MB | 58,19 MB | 5,5% / kb. 3,41 MB |
| Összes memóriaallokáció 1000 naplóbejegyzés írásakor | 425,71 MB | 3,41 MB | 99,2% |

MB = 1 000 000 bájt. A fájlméretek ugyanazzal a self-contained win-x64 / PublishSingleFile / IncludeNativeLibrariesForSelfExtract beállítással készültek; a single-file tömörítés már a beta.5-ben is aktív volt. A pontos méret a build metaadataival pár bájtot változhat.

A naplómérés 200 meglévő bejegyzés mellé 1000 új bejegyzést ír, végig 200-as korláttal és azonnali fájlmentéssel. Nyers adatok: [OPTIMIZATION-BETA6-MEASUREMENTS.json](OPTIMIZATION-BETA6-MEASUREMENTS.json). Öt ismétlés mediánja: beta.5 **425 709 704 bájt**, beta.6 **3 408 264 bájt**. Release / .NET 10, azonos Linux környezet, helyi ideiglenes fájlok, API-hívás nélkül. Ez az összes létrehozott, később felszabaduló memóriát méri, **nem a teljes Windows-alkalmazás RAM-használatát**. Folyamat-RAM és tényleges WPF futási/indulási sebesség Linuxon nem mérhető; ezekre nem adunk százalékos ígéretet. A futásidő függ a fájlrendszertől és annak gyorsítótárától, ezért azt nem használjuk általános sebességígéretként.

## Mi változott?

- **Hat JSON-tároló egy közös fájlkezelővel:** árfolyam, téma, indulási beállítások, csomaglista, lekérési keret és API-napló. Közvetlen UTF-8 stream olvasás/írás, teljes UTF-16 JSON-szöveg köztes létrehozása nélkül. Az atomi ideiglenes fájl + átnevezés, a régi adatformátumok, a hibakezelés és UTF-8 BOM kompatibilitás megmarad. A titkos hozzáférések továbbra is kizárólag DPAPI-védett bináris mentést kapnak.
- **200 bejegyzéses naplócache:** minden diagnosztika azonnal mentődik, de a saját, változatlan fájlt nem olvassuk/feldolgozzuk újra minden bejegyzésnél. Fájlméret/időbélyeg változásakor újraolvasás; külső törlés és hibás fájl felismerve. A visszaadott lista önálló másolat; sikertelen írás nem módosítja a cache-t vagy a korábbi fájlt. Csak kifejezett naplótörlés javít hibás fájlt.
- **Egy SecretEntry a DHL/UPS mezőknek:** közös szemgomb, karakterszám, téma, betű, kurzor és alapból rejtett tartalom. Mentéskor/lapelhagyáskor a látható másolat ürül. A DHL mező az eredeti 18+6+42 pixeles területet foglalja el.
- **Négy helyett három HttpClient:** a DHL és UPS közös, 20 másodperces, 2 MB-os, átirányítás nélküli követési klienst használ. Kulcs / Basic / Bearer kizárólag az adott kérés fejlécébe kerül. A szolgáltatói hitelesítés, token, keret, öt másodperces ütemezés és megszakítás továbbra is külön kezelt. Árfolyam és frissítő megtartja saját időkorlátját.
- **Kevesebb táblázatmunka:** a két csomagtábla közös lapozása csak a látható négy sort adja vissza, azonos rendezéssel. A percenkénti helyi tisztítás megmarad, de rejtett Robo Sanyi esetén nincs táblázat-újraépítés. Látható oldalon csak az időfüggő, megérkezett táblázat frissül az időzítőből.
- **Kisebb Windows-csomag:** az app magyar felületéhez nem szükséges runtime nyelvi erőforrások kimaradnak (`SatelliteResourceLanguages=hu;en`); az angol semleges fallback és a magyar kultúra/időkezelés megmarad. A teljes önálló .NET/WPF futtatókörnyezet, natív komponensek és grafika megmarad; nincs WPF-funkciókat kockáztató trimming vagy AOT.

## Ellenőrzés és ismételhető próba

**398 automatizált ellenőrzés**, beleértve az eddigi kalkulátor/mentés/frissítés/DHL/UPS eseteket, cache-változás/törlés/hibás fájl/sikertelen atomi írás, párhuzamos naplózás, BOM/szöveg kompatibilitás, lapozás/rendezési holtversenyek és közös kliens fejléc-elkülönítés. Hibamentes Windows x64 publish; XAML és fix mezőmagasság ellenőrzése. A tényleges WPF-felületet és Windows DPAPI futását Linux alatt nem tudjuk elindítani, élő céges futárkulcsos próba nem történt.

Ellenőrzés:

```text
dotnet run --project tests/CatPriceCalculator.Checks -c Release
```

A naplózási mérés kézzel, ideiglenes fájlokkal és hálózat nélkül indítható:

```text
dotnet run --project tests/CatPriceCalculator.Checks -c Release -- --benchmark
```

Öt mérés JSON-eredményét adja az ellenőrzések után. A beta.5 alapmérés ugyanezzel a naplózási forgatókönyvvel, a módosítások előtt készült. Az eltérő ideiglenes útvonal hossza néhány bájttal módosíthatja az allokációs értéket.

---

## Korábbi optimalizáció mérési eredményei

# v0.11.2 optimalizálás – változatlan funkciók és arculat

Mérések a v0.11.1 forrásával összevetve, Release .NET 10.0 Linux alatt. Nyers adatok: OPTIMIZATION-MEASUREMENTS.json. A közvetlenül érintett műveleteket mértük; ez nem a teljes Windows-folyamat RAM-, CPU- vagy indulásiidő-mérése.

| Művelet / mért mennyiség | Előtte | Utána | Eredmény |
| --- | ---: | ---: | --- |
| Számfeldolgozás, 400 000 vegyes bevitel, medián | 40.984 ms | 23.542 ms | 42.56% rövidebb idő |
| Számfeldolgozás, allokált bájt / 400 000 hívás | 16,000,096 | 96 | Nincs hívásonkénti szövegallokáció a mért mintán; 96 bájt fix mérési rezsi |
| Frissítő, 8 MiB-os ZIP, allokált .NET-bájt / futás | 16,819,432 | 42,256 | 99.75% csökkenés |
| Három átfedő árfolyamlekérés, sikeres EKB HTTP-kérések | 3 | 1 | 66,67%-kal kevesebb EKB-kérés ebben az esetben |
| Első téma használatakor épített paletta-ecsetek | 84 | 2 | Csak a szükséges paletta készül el; szemantikus UI-ecsetek változatlanok |
| EXE bájt | 67,593,315 | 67,593,853 | +538 bájt, +0,0008%; gyakorlatilag változatlan |

A számteszt 8, felváltva használt érvényes/hibás, magyar/angol és Unicode szóközös bevitelt mér. 20 000 hívás bemelegítés mindkét változaton, utána 7 forduló, váltott mérési sorrenddel. A frissítőteszt 8 MiB véletlen adattartalmú EXE-t tartalmazó, tömörítés nélküli ZIP-et használ, két 4 MiB körüli és egy kis záró részben. Ugyanazok az adatok, StreamContent HTTP-fixture, egy bemelegítő és négy mért forduló. A fixture létrehozása kívül esik a mért allokáción. A párhuzamos EKB-kérések száma késleltetett, számlált tesztforráson ellenőrizve. Hálózati sebességre nem következtetünk a helyi frissítőteszt idejéből.

Megvalósítás:
- A számmezők normalizálása rövid bevitelnél stackalloc/Span segítségével, hosszúnál visszaadott ArrayPool-pufferrel. Ugyanazok a szeparátorok és ellenőrzések. A számítási képletek, kerekítés és szorzók változatlanok.
- A frissítő HTTP-fejlécek után közvetlenül a fájlba másol, legfeljebb 64 KiB-os újrafelhasználható átviteli pufferrel. Részenkénti SHA-256 folyamatosan, teljes ZIP SHA-256 továbbra is külön, a lemezről ellenőrizve. Hostellenőrzés, 8 MiB-os eszközméret-korlát, sorrend, pontos letöltési hossz, megszakítás, timeout és csak sikeres ellenőrzés utáni kicsomagolás megmaradnak.
- Az átfedő, azonos megszakítási körbe tartozó EKB-lekérések egy HTTP-letöltést használnak. Nincs tartós adatcache; befejezés után új kérés új letöltést indít. Külön megszakítási körök és tartalék devizapár-források önállóak maradnak.
- Online árfolyam-alkalmazáskor a közbenső TextChanged nem számol újra; az árfolyam és eredet beállítása után a meglévő explicit újraszámítás megmarad. Manuális bevitel és a revision-védelem működése változatlan.
- A kijelölések csak állapotváltozáskor írják a WPF-propertyt, új bool-objektumok nélkül. A frissítési szolgáltatás újrafelhasználható.
- 35 használatlan szín/ecset-erőforrás, egy használatlan ikon és a régi, nem beágyazott raster logó eltávolítva. A használt fotó, vektorlogó és natív ikon bájtról bájtra változatlan. Vektorok/fotóforrás fagyasztva; témaváltó ecsetek nem, mert dinamikusak.
- A paletta 42 helyett 32 használt színt tartalmaz. Csak az elsőként választott paletta készül el induláskor, a másik első használatkor. Témák színei és mentése változatlanok.

Ellenőrzés: 122 automatikus ellenőrzés, köztük 10 012 bemenet összevetése a korábbi parserrel, csonka/túlméretes/hibás letöltés, streaming, választest-timeout, frissítés integritása, párhuzamos EKB és független megszakítások. Három élő árfolyamlekérés sikeres. Hibamentes Windows x64 self-contained publish. Mind a 43 névvel hivatkozott UI-elem, méret, kötés, X gomb, 3×5-ös rács és a használt két téma színpárjai megőrizve. A Windows felület tényleges futtatása itt nem lehetséges.

---

# v0.8.1 optimalizálás

Mért EXE méret: 174,125,807 → 76,194,127 bájt (56.24% csökkenés).

- Minden gombot érintő elmosó árnyékszűrők megszüntetve; éles keretkiemelés.
- Szabályos nyolcfogú fogaskerék központi körrel, vektoros geometria.
- Fülváltásonként 15 új gomb létrehozása helyett a meglévő 15 gomb marad.
- Szorzóválasztáskor több mint 30 új ecset és egy árnyékeffekt helyett újrafelhasználható ecsetek, árnyékszűrő nélkül.
- Két előre elkészített, fagyasztott témapaletta: ismételt színfeldolgozás és ecsetgyártás nélkül. Ez kis induláskori többlet az ismételt váltások olcsóbb kezeléséért.
- Új ExchangeRateService példány lekérésenként helyett egy közös példány; az időzóna objektuma is újrafelhasználható.
- Single-file tömörítés csökkenti a lemezen lévő EXE méretét. Induláskor kicsomagolási munkát igényel, ezért nem állítjuk, hogy gyorsabb indulást eredményez.

A funkciók változatlanok. 103 automatikus ellenőrzés és 3 élő árfolyamlekérés. Windows CPU/GPU/RAM és indulási idő itt nem mérhető, ezek javulására nem adunk százalékos állítást. A fenti objektumszámok a kód vizsgálatából származnak, nem futásidejű profilozásból.

Letöltési ZIP: 69,922,900 → 70,333,417 bájt (+0.59%). A kisebb EXE nem eredményez kisebb letöltést, mert a korábbi ZIP már erősen tömörítette az alkalmazást.
