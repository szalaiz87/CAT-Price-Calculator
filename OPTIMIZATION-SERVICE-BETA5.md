# Szerviznaptár optimalizáció – v1.2.0-beta.5

Az előtte mérés a teljes XML-fát használó importot, soronkénti szótárakat és a teljes naptárállomány csoportosítását vizsgálta. Az utána mérés az új, soronkénti XML-feldolgozást, egyetlen fájlonkénti fejlécindexet és a látható 42 nap csoportosítását mérte. Ugyanazon szintetikus adatkészletek, ugyanazon Linux/.NET 10.0.12 környezet, Release build. Műveletenként 3 bemelegítő és 7 mért futás, a medián szerepel a táblában. A fájlok létrehozása és a mérés előtti GC nem része az időnek.

## Mért eredmények

Excel: 5000 ügyfél és 10000 gép két XLSX-fájlban, meleg helyi fájlolvasással. Naptár: 100000 tesztfeladat tíz évre szétosztva, a 2026. októberi 42 napos nézetben 1134 feladat. Az eredmények Linuxon mért adatfeldolgozási értékek; nem a teljes Windows-alkalmazás indulási ideje, RAM-ja vagy kirajzolási ideje.

| Mérés | Előtte | Utána | Változás |
| --- | ---: | ---: | ---: |
| Excel-import – idő | 99.96 ms | 74.35 ms | −25.6% |
| Excel-import – allokáció | 59.89 MB | 9.73 MB | −83.8% |
| Naptári adatcsoportosítás – idő | 11.42 ms | 1.39 ms | −87.8% |
| Naptári adatcsoportosítás – allokáció | 9.39 MB | 0.11 MB | −98.9% |

Az allokáció a vizsgált szinkron művelet futó szálán létrehozott kezelt objektumok összes bájtja (`GC.GetAllocatedBytesForCurrentThread`), nem csúcsmemória vagy RAM. A futásidő terheléstől és háttérfolyamatoktól változhat; az allokáció mért értékei azonos végső adatkészlettel ismételt mérésben is megegyeztek.

Az önálló Windows EXE mérete a felületi módosítások után, optimalizáció előtt **64 391 047 bájt**, utána **64 391 205 bájt**: +158 bájt, gyakorlatilag változatlan. Az optimalizáció itt az adatfeldolgozás allokációját és idejét javította.

## Mit és miért javítottunk?

1. **Excel-import:** a teljes munkalap XML-fája helyett közvetlen `XmlReader`-feldolgozás, soronként. Cellánként sem készül külön XML-olvasó, soronként sem készül külön szótár; az oszlopindex egyszer jön létre fájlonként. Kevesebb rövid életű objektum és kisebb XML-megőrzés. Ékezetek, shared strings, rich text, üres opcionális cellák és idézőjeles CSV továbbra is működnek.
2. **Naptárindex:** csak a megjelenített 42 nap feladatait csoportosítjuk, nem a teljes történetet. A régi feladatok a mentett állományban megmaradnak; más hónapra lépve annak indexe készül el. A csoporton belüli feladatsorrend és az egy napra írt több munka változatlan.
3. **Hónapfülek:** a 12 gomb egyszer készül el, utána csak az aktív állapot frissül. Új hónapgomb létrehozása nézetfrissítésenként 12-ről 0-ra csökkent; ez kódból ellenőrzött darabszám, nem Windows-kirajzolási időmérés.
4. **Hibajavítás:** a nulla, előjeles és az Excel határán túli sorazonosítók importját elutasítjuk. Az Excel hibacellái (például #N/A) nem kerülnek normál ügyfél-/gépnévként a törzsbe. Ezeknél a sikertelen import nem írja felül a korábbi adatbázist. A képletek tiltása megmarad.

## Ellenőrzés és reprodukció

572 funkcionális ellenőrzés; benne régi naptáradatok, rögzítőnevek, importidő, ékezetfüggetlen ügyfélkeresés, 42 napos szűrés/sorrend, hibás koordináták, rich text, opcionális cellák és formulák. További statikus ellenőrzések a fix WPF-keretre, eseményekre, közös gombsablonra és mindkét téma szövegkontrasztjára. Windows x64 self-contained publish készül; a tényleges WPF/DPAPI-futtatás és teljes Windows-folyamatprofil itt nem végezhető el.

A végső mérés újrafuttatása a forrás gyökerében:

```sh
dotnet run --project tests/CatPriceCalculator.Checks -c Release -- --service-benchmark service-measurement.json
```

Nyers eredmények és százalékok: OPTIMIZATION-SERVICE-BETA5-MEASUREMENTS.json. Az előtte mérés változatlan korábbi algoritmusokkal készült; a szintetikus adatok nem valós ügyféladatok.
