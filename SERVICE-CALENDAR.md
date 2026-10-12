# Szerviznaptár – első béta

A Szerviznaptár menüpont helyi adatbekérő. A „Havi naptár megnyitása” gomb külön ablakot nyit. Hónapfülek, előző/következő hónap és Ma gomb; hétfővel kezdődő hathetes rács, az öt munkatárs külön színsávban. Egy napra és munkatársra tetszőleges számú feladat rögzíthető. A cella az első feladatot és a továbbiak számát mutatja; kattintással a teljes adatok és az összes feladat lapozható. Hosszú részletek külön lapozhatók görgetősáv nélkül.

## Excel-import a felületen

1. Ügyfél-Excel kiválasztása, majd Gép-Excel kiválasztása.
2. Adatbázisok beolvasása. Mindkét fájlt ellenőrzi, és csak siker esetén cseréli le a törzset.
3. Ügyfél kiválasztása után csak az ügyfélazonosítóhoz rendelt gépek jelennek meg.

Excel (.xlsx), első munkalap, első nem üres sor a fejléc. Excel telepítése nem szükséges. Képletek helyett értékeket tartalmazó export szükséges. Többletoszlopok megengedettek. Az azonosítókat szövegként exportálja, így a kezdő nullák megmaradnak. A tényleges ügyviteli exporthoz az oszlopneveket egy mintafájl alapján tudjuk tovább igazítani.

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

Az Importmappa megnyitása gomb létrehozza a `Dokumentumok\LinserHungary\Szerviznaptar\Import` mappát, benne két üres fejléces mintával, kitöltött `ugyfelek-proba.xlsx` / `gepek-proba.xlsx` próbaadatbázissal és útmutatóval. Fájlválasztás nélkül innen olvassa az `ugyfelek.xlsx` és `gepek.xlsx` fájlokat. Nem írja felül az ott meglévő exportokat.

## Feladat mentése

Ügyfél, hozzá tartozó gép, munkatárs, dátum (éééé.hh.nn), kapcsolattartó neve és telefonja, gép lokációja, hibaleírás. A Szalai Zoltán naptár a szervizvezetőé. A munkatársak színei a kapott táblázat mintáját követik. Mentés után a hibaleírás kiürül, a többi választás megmarad a következő feladathoz. A nyitott naptár azonnal frissül.

A feladatok ügyfél- és gépnevet is mentenek: egy későbbi törzsimport nem módosítja a már rögzített feladatok adatait. Nem törli a régi feladatokat akkor sem, ha egy ügyfél vagy gép az új exportban már nincs benne.

## Helyi tárolás

`%LOCALAPPDATA%\CAT-Price-Calculator\ServiceCalendar\catalog.json` és `jobs.json`. Újraindítás után megmaradnak; más géppel nincs szinkronizálás. Biztonsági másolat: bezárt alkalmazás mellett a teljes ServiceCalendar mappa másolata. Hibás mentett naptár esetén a mentés letiltott, az eredeti adatfájl érintetlen marad. Egyidejű helyi feladatmentés fájlzárral védett.

Ez az alapverzió rögzítést és megtekintést tartalmaz. Meglévő feladat szerkesztése/törlése, időpontok, többnapos feladatok, megosztás és emlékeztetők későbbi fejlesztési lépések.

A WPF-felület tényleges futtatása Linux alatt nem ellenőrizhető; a build, import, hozzárendelés, hónaprács és mentés automatizáltan ellenőrizhető.

A kitöltött próbaadatbázis 4 fiktív ügyfelet és 8 gépet tartalmaz. A fájlválasztókkal válassza ki a két `*-proba.xlsx` fájlt, majd olvassa be őket. Ez az import lecseréli a helyi ügyfél/gép törzset, de a meglévő naptárfeladatokat megőrzi. A végleges adatbázisra a valódi fájlok újraimportálásával lehet visszatérni. A naptár munkatársjelölése feliratos sorokkal, kék széljelöléssel és semleges váltakozó felületekkel történik.
