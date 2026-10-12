# Szerviznaptár – első béta

A Szerviznaptár menüpont helyi adatbekérő. A „Havi naptár megnyitása” gomb külön ablakot nyit. Hónapfülek, előző/következő hónap és Ma gomb; hétfővel kezdődő hathetes rács, az öt munkatárs külön színsávban. Egy napra és munkatársra tetszőleges számú feladat rögzíthető. A cella az első feladatot és a továbbiak számát mutatja; kattintással a teljes adatok és az összes feladat lapozható. Hosszú részletek külön lapozhatók görgetősáv nélkül.

## Excel-import a Beállítások / Szerviz lapon

1. Beállítások → Szerviz. Ügyfél-Excel tallózása, majd Gép-Excel tallózása. Mindkét teljes útvonal külön mezőben látszik, és megmarad a következő indításkor is.
2. Adatbázisok beolvasása. Mindkét fájlt ellenőrzi, és csak siker esetén cseréli le a törzset.
3. A Szerviznaptár lapon az Ügyfél keresése gombbal válasszon ügyfelet. Utána csak a hozzá rendelt gépek jelennek meg a géplistában.

Excel (.xls és .xlsx), első munkalap, első nem üres sor a fejléc. Excel telepítése nem szükséges. Képletek helyett értékeket tartalmazó export szükséges. Többletoszlopok megengedettek. Az azonosítókat szövegként exportálja, így a kezdő nullák megmaradnak. A tényleges ügyviteli export eredeti oszlopneveit automatikusan felismeri a program; nem szükséges átnevezni őket.

| Ügyfélfájl oszlop | Tartalom |
| --- | --- |
| ugyfelazonosito | Egyedi ügyfélkód |
| nev | Ügyfél neve |

| Gépfájl oszlop | Tartalom |
| --- | --- |
| gepazonosito | Egyedi gépkód |
| ugyfelazonosito | A gép tulajdonosának ügyfélkódja |
| megnevezes | Gép neve/típusa |
| gyariszam | Opcionális gyári szám |

A fejléc kis/nagybetűi, ékezetei és szóközei nem számítanak. „Ügyfélkód”, „Partnerkód”, „Partnerazonosító”, „Ügyfél neve”, „Partnernév”, „Gépkód”, „Gép megnevezése” és „Sorozatszám” is elfogadott megfelelő névként. Üres kötelező mező, duplikált azonosító és ismeretlen ügyfélhez rendelt gép esetén az import elutasított, a régi adatbázis megmarad.

Az Importmappa megnyitása gomb létrehozza a `Dokumentumok\LinserHungary\Szerviznaptar\Import` mappát, benne két üres fejléces mintával, kitöltött `ugyfelek-proba.xlsx` / `gepek-proba.xlsx` próbaadatbázissal és útmutatóval. A fájlokat a külön importbeállítások lapon kell kiválasztani. Nincs automatikus vagy rejtett fájlválasztás. Nem írja felül az ott meglévő exportokat.

## Feladat mentése

Ügyfél, hozzá tartozó gép, munkatárs, dátum (éééé.hh.nn), kapcsolattartó neve és telefonja, gép lokációja, hibaleírás. A Szalai Zoltán naptár a szervizvezetőé. Szalai Zoltán elsőként szerepel, külön aranyszínű szervizvezetői jelöléssel. A többi munkatárs semleges, váltakozó sorfelületeket kap. Sikeres mentés után visszajelzés jelenik meg, és minden beviteli mező, ügyfél-, gép- és munkatársválasztás, valamint a dátum is kiürül. Sikertelen mentésnél a kitöltött adatok megmaradnak. A nyitott naptár azonnal frissül.

A feladatok ügyfél- és gépnevet is mentenek: egy későbbi törzsimport nem módosítja a már rögzített feladatok adatait. Nem törli a régi feladatokat akkor sem, ha egy ügyfél vagy gép az új exportban már nincs benne.

## Helyi tárolás

`%LOCALAPPDATA%\CAT-Price-Calculator\ServiceCalendar\catalog.json` és `jobs.json`. Újraindítás után megmaradnak; más géppel nincs szinkronizálás. Biztonsági másolat: bezárt alkalmazás mellett a teljes ServiceCalendar mappa másolata. Hibás mentett naptár esetén a mentés letiltott, az eredeti adatfájl érintetlen marad. Egyidejű helyi feladatmentés fájlzárral védett.

Ez az alapverzió rögzítést és megtekintést tartalmaz. Meglévő feladat szerkesztése/törlése, időpontok, többnapos feladatok, megosztás és emlékeztetők későbbi fejlesztési lépések.

A WPF-felület tényleges futtatása Linux alatt nem ellenőrizhető; a build, import, hozzárendelés, hónaprács és mentés automatizáltan ellenőrizhető.

A kitöltött próbaadatbázis 4 fiktív ügyfelet és 8 gépet tartalmaz. A fájlválasztókkal válassza ki a két `*-proba.xlsx` fájlt, majd olvassa be őket. Ez az import lecseréli a helyi ügyfél/gép törzset, de a meglévő naptárfeladatokat megőrzi. A végleges adatbázisra a valódi fájlok újraimportálásával lehet visszatérni. A naptár munkatársjelölése feliratos sorokkal, kék széljelöléssel és semleges váltakozó felületekkel történik.

## Importfrissesség és ügyfélkeresés

A Szerviznaptár lapon csak az utolsó sikeres, mindkét adatbázist frissítő import időpontja jelenik meg magyar időben. Ha hét napnál régebbi, sárgásan villogó „Frissítés szükséges” jelzés látszik. A fájlok tallózása és a sikertelen import nem módosítja az időpontot. A korábbi, időbélyeg nélküli törzs importidejét nem találjuk ki: frissítést kér. A bejegyzések továbbra is rögzíthetők a korábbi törzzsel.

Az ügyfélkereső a név elején vagy bárhol a névben keres. Kis-/nagybetűk és magyar ékezetek, köztük ő/ű, nem különböznek. A normalizált névindex a kereső megnyitásakor egyszer készül el. A találatok 12-es lapokban jelennek meg; ügyfél választása után a géplista a megfelelő ügyfélhez szűrődik.

A naptár, ügyfélkereső és feladatrészletek normál, nem mindig előtérben tartott, nem tulajdonolt és nem modális ablakok. Egymástól és a főablaktól függetlenül rétegezhetők. A főablak bezárása a szervizablakokat is bezárja. A főablak meglévő Mindig előtérben kapcsolója továbbra is csak a főablakot szabályozza.

## Munka hozzáadása a naptárból és rögzítő

Egy nap/munkatárs celláját megnyitva a Munka hozzáadása gomb a Szerviznaptár adatbekérő lapját hozza elő, a kattintott dátummal és szerelővel. Az esetleg már beírt többi mező megmarad. A rögzítő kötelező külön választás: Szalai Zoltán, Hották Enikő, Sebesi Sándor vagy Helembai Gergely. A név a mentett feladat része, a napi részletablakban is látszik; a régi feladatoknál „korábban nem rögzített”. Sikeres mentés után ez a választó is kiürül.

Szalai Zoltán neve mellett nincs külön vezető felirat; az első hely és az arany jelölés megmarad. Az app normál gombjai és a funkcióválasztó menügombok egy közös sablont használnak: lekerekített felület, azonos hover/fókusz/aktív állapotok és oldalsó jelölés. A napi részletek gombjai két sorban, a közös stílusban helyezkednek el.

## Ügyviteli törzsexport (v1.2.0-beta.6)

A feltöltött valódi bináris XLS-formátum Excel telepítése nélkül beolvasható. Ugyanez a fejléc .xlsx fájlban is használható. A többi oszlopot figyelmen kívül hagyjuk.

| Export | Oszlop | Felhasználás |
|---|---|---|
| Partnertörzs | A (`kod`) | Partnerazonosító |
| Partnertörzs | B (`nev`) | Partner megnevezése |
| Géptörzs | A (`gep`) | Gépazonosító |
| Géptörzs | B (`megnevezes`) | Gép megnevezése |
| Géptörzs | J (`gep_azonosito`) | Alvázszám, megjelenik a gépválasztóban |
| Géptörzs | K (`partner`) | Partnerazonosító, összekapcsolás a partnertörzs A oszlopával |

A hiányzó gépnév helyére `- hiányzó adat -` kerül. Üres partnerazonosító esetén a gép a kereshető `- hiányzó adat -` gyűjtőpartnerhez tartozik. Ezek a sorok is bekerülnek; az import visszajelzése jelzi a darabszámokat. Az összes többi partnerhez csak a saját gépei választhatók. Nem üres, de a partnertörzsben nem található azonosító esetén a program hibát jelez, és megőrzi a korábbi adatbázist.

A tényleges export ellenőrzése: 5144 partner, 6323 gép, 27 hiányzó partnerazonosító, 5 hiányzó gépnév. Minden gép megmaradt. A valódi exportfájlok és ügyféladatok nem kerülnek a nyilvános forrásba vagy a kiadásba; a regressziós XLS-minták fiktívek.

## Automatikus rögzítő (v1.2.0-beta.8)

A korábbi rögzítőválasztót a bejelentkezett felhasználó megjelenített neve váltja fel. Nem szerkeszthető, és az űrlap ürítése után is látható marad. Új felhasználó neve is menthető, a korábbi naptáradatok rögzítőneve nem változik.

A bal oldali szervizáttekintés a mai és a következő 7 napi munkák számát, a következő tervezett munkát és munkatársat, a törzs méretét és az utolsó import magyar idejét mutatja. Mentéskor, importkor és látható oldalon percenként frissül, helyi adatokból.
