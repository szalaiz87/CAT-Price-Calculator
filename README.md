# Linser Hungary v1.1.0-beta.1 – Robo Sanyi előnézet

- Harmadik funkcionális menüpont: **Robo Sanyi**, külön vektoros robotikonnal.
- Felül csomagszám, logós DHL/FedEx/UPS/GLS futárválasztó, megjegyzés és helyi hozzáadás. API nincs; a saját csomag „Adatra vár” állapotban marad, kitalált követési adatok nélkül.
- Két egymás alatti táblázat: úton lévő és megérkezett csomagok. Logó/csomagszám/megjegyzés, státusz, utolsó hely és eseményidő, valamint tervezett érkezés vagy kézbesítés/törlés. Mindkét táblázat négy sort mutat, külön lapozással; nincs görgetősáv.
- Hat külön jelölt mintacsomag látható, köztük két kézbesített. A minták elrejthetők és visszaállíthatók a saját bejegyzések megtartásával.
- Helyi, atomikus mentés a külön `robo-sanyi-preview.json` fájlba. Kézbesítéstől számított 48 óra után automatikus törlés; ellenőrzés percenként és minden programindításkor. Bezárt appnál a következő indítás tisztít. Üres mentést nem töltünk újra mintákkal; hibás fájlt nem írunk felül.
- Változatlan 780×840 alapablak és 890×940 referenciakeret; a harmadik menühöz a logó körüli térközök tömörebbek. A kalkulátorok méretei és funkciói, a két téma, stabil/béta csatorna és visszatérés megmaradnak.

[Windows béta letöltő, PowerShell nélkül](https://github.com/szalaiz87/CAT-Price-Calculator/releases/download/v1.1.0-beta.1/CAT-Letoltes-Windows-v1.1.0-beta.1.cmd). A stabil appban a béta-frissítési kapcsoló engedélyezésével is elérhető. Legfrissebb stabil: **v1.0.0**.

A logók az internetről letöltött SVG-kből fagyasztott WPF-vektorok. Források: [SOURCES.md](src/CatPriceCalculator/Assets/Carriers/SOURCES.md). Nincs futás közbeni logóletöltés vagy külső SVG-renderelő.

Ellenőrzés: **172 automatizált ellenőrzés**, benne pontos 48 órás határ, magyar óraátállítás, újraindítás utáni mentés/törlés, minták, duplikáció és hibás/üres fájl kezelése. Windows x64 publish és statikus XAML-ellenőrzés. A tényleges WPF-felület Linux alatt nem futtatható.

További kiadások alapból béta státuszúak; stabil kiadás kizárólag kifejezett felhasználói kérésre.

---

# Linser Hungary v1.0.0 – első stabil főverzió

- A Frissítések gomb alapból csak stabil kiadást ajánl fel.
- A Beállításokban menthető „Béta frissítések engedélyezése” kapcsolóval a legújabb stabil vagy béta kiadás is elérhető.
- Béta programban külön gomb tér vissza a legfrissebb stabilra, akár alacsonyabb verziószámra is. A visszatérés kikapcsolja a béta csatornát; a többi beállítás és árfolyam megmarad.
- A verziószám közös build-metaadatból származik; a CAT/EUR funkciók, arculat és görgetősáv nélküli rögzített elrendezés megmaradnak.

**Kiadási szabály:** minden további fejlesztés béta, stabil kiadás csak a felhasználó kifejezett kérésére. Teljes forrás: `stable` és `beta` ág; a történeti `main` megmarad. Részletes működés és kiadási útmutató: [RELEASES.md](RELEASES.md).

[Windows 1.0 letöltő, PowerShell nélkül](https://github.com/szalaiz87/CAT-Price-Calculator/releases/download/v1.0.0/CAT-Letoltes-Windows-v1.0.0.cmd). Telepítés előtt zárd be a futó programot.

Ellenőrzés: 152 automatizált ellenőrzés sikeres stabil és béta builddel; Windows x64 self-contained publish hibamentes. A tényleges WPF-felület Linux alatt nem futtatható.

---

# Linser Hungary v0.11.2 – optimalizálás

- Kevesebb átmeneti memória a számfeldolgozás és a frissítés során.
- Átfedő árfolyamlekérések közös EKB-letöltéssel, későbbi lekéréskor friss adatokkal.
- Csak szükséges témapaletták, fagyasztott vektorok, használatlan erőforrások eltávolítása és fölösleges UI-frissítések csökkentése.
- CAT/EUR számítás, kerekítés, árfolyamok, haszon/árrés, szorzók, X törlőgomb, sötét/világos téma, mentés, indulási beállítások, normál tálca és frissítés változatlan. Görgetősáv nincs, elrendezés és arculat megmarad.

Részletes eredmények és mérési korlátok: OPTIMIZATION.md. Nyers mérési adatok: OPTIMIZATION-MEASUREMENTS.json. 122 automatizált ellenőrzés és három élő árfolyamlekérés sikeres, Windows x64 publish hibamentes. A Windows UI tényleges futtatása Linux alatt nem lehetséges.

---

# Linser Hungary v0.11.1 – ár törlése és teljes világos arculat

- Vektoros X gomb a CAT (DKK) és EUR beszerzési ármező végén. Csak az árat törli; az árfolyamok, szorzó és a másik fül értéke megmarad. A kurzor az ármezőbe kerül, üres mezőnél a törlőgomb inaktív.
- A világos téma a menüsávra, panelekre, mezőkre, aktív állapotokra, fotós fejlécre és eredménykártyára is kiterjed. Világos panelek, kék hangsúlyok, sötét, kontrasztos szöveg; ugyanaz a komponensrendszer és 10-es lekerekítés.
- Dinamikus figyelmeztetésszínek témaváltáskor; a Beállításokban kiválasztott téma megmarad a következő indításra.
- A sötét arculat, méretek, számítások, mentések, szorzók, haszon/árrés, árfolyamok, billentyűparancsok és frissítés működése változatlan. Görgetősáv nincs.

Ellenőrzés: hibamentes Windows x64 publish; az X gomb kötése, ár törlése/fókusz visszaadása, erőforrások és a rögzített 3×5 rács statikusan ellenőrizve. A vizsgált világos téma szövegpárjai elérik a 4,5:1 kontrasztot (input, gomb, aktív menü és eredmény). A tényleges WPF-megjelenés és kattintás Linux alatt nem futtatható, Windows alatt ellenőrizendő.

---

# Linser Hungary v0.11.0 – egységes navy UI redesign

- Keskenyebb bal menü, cian aktív jelölés, alsó Beállítások / Frissítések zóna, megújult csúszókapcsolók.
- Fotós hero, egységes 10-es sarkú mezők és panelek, közös hover/pressed/fókusz állapotok. Méretet nem változtató fókuszkeret; nincs elmosó effekt a menüfeliratokon.
- Erősebb összegzés, jobb szám- és felirathierarchia, 3×5-ös szorzórács, nagyobb eladási ár kék gradientkártyán.
- Az árfolyammezők metaadata két sorban, az eltérési figyelmeztetés külön sorban, fenntartott hellyel.
- Sötét és világos témák, 890×940 állandó referenciakeret, 780×840 képernyőhöz igazított alapablak, görgetősáv nélkül.

**Funkciók változatlanok:** CAT/EUR számítás, +10% CAT dealer felár, ×1,30–×2,00 szorzók, haszon/árrés, árfolyamlekérés és időbélyeg, másolás, új számítás, billentyűparancsok, témák és indítási beállítások mentése, normál tálcára minimalizálás, frissítés.

**Megvalósítás:** a Styles/Colors.xaml, Typography.xaml, Controls.xaml és Branding.xaml erőforrásszótárakat az App.xaml tölti be. A MainWindow.xaml strukturált kártyákat és stabil Grid-sorokat használ. A code-behind csak a vizuális kiválasztás/stílus hozzárendelésében és a verziószámban változott. ThemePalette.cs és WindowTheme.cs a palettát és a natív fejlécet egységesítik. A Core fájlokban kizárólag a HTTP user-agent verziója frissült; a számítási és hálózati működés változatlan.

**Ellenőrzés:** 111 meglévő automatizált ellenőrzés sikeres; 42 névvel hivatkozott elem, események, beviteli kötések és funkcionális kód megőrzése összevetve a v0.10.0 forrással; XAML-erőforrások, rácsok és sorok ellenőrizve. Hibamentes Windows x64 self-contained publish. A tényleges WPF megjelenés Linux alatt nem futtatható; Windows alatt ellenőrizendő.

A teljes forrás letölthető a release Source.zip fájljában; a main ág továbbra is változatlan.

---

# Linser Hungary v0.10.0 – kisebb, kétoszlopos megjelenés

- Árfolyam-információk a bal oldali menüsávban, a kalkulátorok és a Beállítások között; a jobb oszlop és Hasznos információk doboz megszüntetve.
- 3 sor × 5 oszlop, mind a 15 szorzógomb ugyanúgy működik.
- Generált, fotóhatású CAT lánctalpas kotrógép hátteret kapott a program a kis vektoros munkagépikon helyett. A fejléc képe és a háttér ugyanazt az 1280 pixel szélesre dekódolt BitmapImage erőforrást használja.
- Referenciakeret: 890 × 940; alapablak 780 × 840, képernyőhöz igazított induló szélességgel. Minden nézet rögzített méretű és görgetősáv nélküli.
- A kalkulátorok, témák, indítási beállítások, verziólog, figyelmeztetések, normál minimalizálás és frissítés megmaradtak.

A háttér AI által generált, nem egy konkrét gép eredeti fényképe. Windows UI vizuális futtatása Linux alatt nem lehetséges.

---

# Linser Hungary v0.9.1 – a csatolt mintát követő vektoros logó

Az alkalmazás főlogója a megadott LIS Germany logó alapján újrarajzolva. L/I/S geometria, világos betűkitöltés, sötétkék kontúr, Germany felirat és ® jel mind vektoros, külső betűkészlet nélkül.

Újrafelhasználható SVG: src/CatPriceCalculator/Assets/LIS-Germany.svg. WPF beágyazott változat: App.xaml / LisVectorLogo. Fehér, lekerekített jelvénypanelen mindkét téma mellett olvasható. A tálca/EXE ikonja változatlan.

A feliratot görbékké alakított, hasonló geometrikus betűformák alkotják; nem az eredeti grafikai forrásból exportált logó. A többi funkció változatlan.

---

# Linser Hungary v0.9.0 – indítási beállítások és verziólog

- Beállításokban v0.9.0 verziószám és négy rövid változásleírás.
- Mentett indítási mód: mindig előtérben vagy normál ablak. A bal oldali kapcsoló továbbra is az aktuális munkamenetet szabályozza; a Beállítások kapcsoló a következő indítást.
- Mentett választás: mindhárom árfolyam automatikus frissítése induláskor, vagy legutóbbi beállított értékek használata.
- Alapérték: normál ablak, mentett árfolyamok. Kikapcsolt frissítésnél az eddigi csak olvasó eltérésellenőrzés megmarad, értéket nem ír felül.
- Sikertelen lekéréskor a korábbi érték megmarad; a közben átírt kézi CAT árfolyamot a revision védi.
- Beállítások külön startup-preferences.json fájlban, a korábbi beállításmappában, régi mentések változtatása nélkül.
- A rögzített, görgetősáv nélküli keret és minden korábbi funkció megmaradt.

Ellenőrzés: 111 automatikus ellenőrzés, 3 élő árfolyamlekérés és hibamentes Windows x64 self-contained single-file publish. A Windows felület tényleges működése Linux alatt nem ellenőrizhető.

---

# Linser Hungary v0.8.3 – látható árfolyamfigyelmeztetések és normál tálca

- A >3% eltérési figyelmeztetés számára nagyobb rögzített hely és rövidebb látható szöveg; teljes forrás/dátum eszköztippben.
- A jobb oldali kártyák közös, rögzített soros Gridbe kerültek. Az alsó kártya és a Haszon és árrés szöveg nem lóg túl az ablakon; alsó lekerekítés is látszik.
- Minimalizálás normál Windows-tálcára. Nincs rejtett értesítési ikon és nincs Hide() hívás. Bezárás a normál X gombbal.
- A fölösleges NotifyIcon/Windows Forms függőség eltávolítva.
- Görgetősáv nélküli megjelenés, rögzített keret, kalkulátorok, témák és frissítés megmaradtak.

Windows UI tényleges futtatása Linux alatt nem lehetséges. Ellenőrzés: hibamentes Windows x64 self-contained single-file Release publish és elrendezési invariánsok.

---

# Linser Hungary v0.8.2 – globális csúszókapcsoló és vektoros LIS

- Mindig előtérben csúszókapcsoló a bal oldali menüben, mindhárom lapon látható. Kattintás, húzás és billentyűzet támogatott; közvetlen kétirányú Topmost binding.
- A raster főlogó helyett WPF DrawingImage, külön L/I/S geometriákkal. A tálca és EXE ikonja megmaradt.
- Egységes 10-es lekerekítés a kártyákon, mezőkön, gombokon és háttereken; a csúszókapcsoló kapszula alakú.
- A rögzített keret és görgetősáv nélküli elrendezés megmaradt, valamint a tömörített EXE és az előző optimalizálás.

Ellenőrzés: Windows x64 self-contained single-file publish, 103 meglévő automatikus ellenőrzés és 3 élő árfolyamlekérés. A Windows UI, valódi húzás és Topmost működés ebben a Linux-környezetben nem futtatható, Windows alatt ellenőrizendő.

---

# Linser Hungary v0.8.1 – éles kiemelés és optimalizálás

Új fogaskerékikon, árnyékszűrő nélküli gombkiemelés. Funkciók változatlanok. A részletes mérések és korlátok az OPTIMIZATION.md fájlban találhatók.

# Linser Hungary v0.8.0 – Beállítások, sötét és világos téma

- Beállítások külön lap, bal alul elkülönítve, vektoros fogaskerékikonnal.
- Egy beállítás: sötét vagy világos megjelenés. Azonnal életbe lép, újraindításkor a választás megmarad.
- A téma kártyákra, mezőkre, szövegekre, ikonokra és a natív Windows-fejlécre is érvényes. A kék eladási árkártya mindkét témában világos szöveges.
- Külön appearance.json tárolja a témát, a CAT-Price-Calculator helyi beállításmappájában; az árfolyamok változatlanok.
- Hibás/hiányzó beállításnál sötét alapérték. Mentési hiba esetén magyar visszajelzés, a választott téma a munkamenetben használható.
- A rögzített 1100 × 900 referenciakeret, görgetősáv nélküli felület és kalkulátorok megmaradtak.

Ellenőrzés: 103 automatikus ellenőrzés és három élő árfolyamlekérés; Windows x64 self-contained single-file Release publish. A Windows UI és DWM színek tényleges megjelenése Linux alatt nem ellenőrizhető.

---

# Linser Hungary v0.7.2 – stabil panelek és egységes szorzók

- Külső margó 6 helyett 3, az alapablak a rögzített tartalom arányához igazítva, csökkentett üres külső kerettel.
- Fülváltáskor rögzített 1100 × 900 referenciakeret és panelméretek. A betűméret és a méretezési arány csak az ablak méretétől függ, a fül tartalmától nem.
- Mindkét kalkulátor egyetlen közös ×1,30–×2,00 szorzólistát használ, 0,05-ös lépésekben.
- A CAT dealer felára továbbra is +10%, az EUR számítás továbbra is felár nélküli.
- Sötét natív Windows-fejléc; Windows 11-en az app háttérszínével egyező címmező és világos szöveg. A régebbi Windows figyelmen kívül hagyhatja a pontos színbeállítást.
- Görgetősáv nincs. A teljes fix felület arányosan igazodik a kisebb ablakhoz.
- Teljes árfolyam-metaadatok az oldalsó információpanelen és a mező alatti sor eszköztippjében. A lekérési dátum/idő továbbra is mentett.

Ellenőrzés: 97 automatikus ellenőrzés és 3 élő árfolyamlekérés. A Windows UI és DWM-fejléc tényleges megjelenése ebben a Linux-környezetben nem tesztelhető.

---

# Linser Hungary v0.7.1 – kompakt neon felület

- Körülbelül 25%-kal kisebb/tömörebb elrendezés (alapablak 1010 × 720 a korábbi 1220 × 920 helyett), kisebb térközök.
- A kalkulátorokban nincs görgethető panel. Egyetlen Viewbox a teljes felületet arányosan igazítja az ablakhoz, minden tartalommal együtt.
- Ciánkék fókusz, neon aktív szorzó, finom gradiens kártyák és kék eredménypanel.
- A forrás árfolyamnapja és az alkalmazásban sikeresen lekért árfolyam időpontja külön adat. A lekérés dátuma és ideje mentett, magyar időzónában (nyári/téli időszámítással) látszik.
- A korábbi mentések továbbra is betölthetők; nem ismert régi lekérési idő esetén „korábban nem rögzített” szerepel.
- A tartós felületi követelmények a DESIGN.md fájlban találhatók: a felhasználó soha nem szeretne görgetősávot.

Windows telepítés: CAT-Letoltes-Windows-v0.7.1.cmd, PowerShell nélkül. Előbb zárd be a kalkulátort a tálcaikon **Kilépés** menüpontjával.

Új build: build-windows.cmd (.NET SDK 10.0.401). Kimenet: artifacts/win-x64/CAT-Price-Calculator.exe.

Ellenőrzés: 87 automatikus ellenőrzés és 3 élő árfolyamlekérés; Windows x64 single-file self-contained Release publish. A Windows UI tényleges megjelenése Linux alatt nem ellenőrizhető.

---

# Linser Hungary - segédprogram v0.7.0

Két kalkulátor közös decimal számítási motorral, sötétkék arculattal és vektoros ikonokkal.

- CAT árkalkulátor: DKK alapár, változatlan +10% dealer felár, DKK/HUF és DKK/EUR.
- EUR árkalkulátor: EUR alapár × EUR/HUF, dealer felár nélkül; ×1,30–×2,00, 0,05-ös lépésekben.
- Külön mentett EUR/HUF árfolyam, fülváltáskor megmaradó számítások, haszon és árrés mindkét oldalon.
- Másolás, új számítás, billentyűparancsok, mindig felül, tálcaikon és frissítés megmaradt.
- EKB elsődleges és Frankfurter tartalék árfolyamforrás; offline használható kézi értékek.
- Az eredeti LS logó és alkalmazásikon megmaradt; a feliratok Linser Hungary nevet használnak.
- A képen szereplő funkcióikonokhoz hasonló vektoros ikonok, valamint visszafogott munkagép-sziluett. Csomagkövetés és beállításmenü nincs.

## Windows telepítés

Zárd be a régi appot, futtasd a `CAT-Letoltes-Windows-v0.7.0.cmd` fájlt. PowerShell és külön .NET telepítés nem szükséges.
Az EXE: `%LOCALAPPDATA%\Programs\LS-Germany-CAT\CAT-Price-Calculator.exe`. A korábbi árfolyamok megmaradnak.

## Új build

.NET SDK 10.0.401: futtasd a `build-windows.cmd` fájlt a projekt gyökeréből. A kész program az `artifacts\win-x64` mappába kerül.
Az EUR/HUF beállítás külön `eur-huf-settings.json` fájl, a korábbi CAT-Price-Calculator beállításmappában.

## Ellenőrzés

81 sikeres automatikus ellenőrzés, 3 sikeres élő árfolyamlekérés; hibamentes Windows x64 self-contained single-file publish.
A Windows UI, tálca és valódi frissítés/újraindítás Linux alatt nem futtatható; Windows alatt ellenőrizendő.

---

# CAT alkatrész árkalkulátor

Kompakt, magyar nyelvű C# / .NET 10 WPF alkalmazás Windows 10/11 x64 rendszerhez. Nincs adatbázis, API-kulcs vagy külső alkalmazáscsomag.

## Indítás

A self-contained kiadás: `artifacts/win-x64/CAT-Price-Calculator.exe`.
Ezt a fájlt másolja Windowsra és indítsa el; külön .NET telepítés nem szükséges. Az első indításkor a csomagolt natív könyvtárak ideiglenes mappába bontódnak ki.

Adja meg a CAT árat és a DKK/HUF és DKK/EUR árfolyamot, az alapértelmezett szorzó ×1,40. A dealer +10% mindig automatikus. A számítás decimal értékekkel történik, köztes kerekítés nélkül; a végső ár egész forintra, felezéskor felfelé kerekül. A bekerülési ár két tizedesjeggyel látható. Az alapértelmezett, manuális árfolyam 53,50; ezt szükség szerint módosítsa.

Elfogadott számok: `1250`, `1250,50`, `1 250,50`, `53.5`. Nulla, negatív, hibás vagy túl nagy összeg esetén nincs eladási ár.

- Ctrl+1–7: szorzó kiválasztása 1,30–1,60 között.
- Enter: továbblépés a beviteli mezőből; Tab/Shift+Tab: navigáció.
- Escape vagy Ctrl+N: új számítás, megőrzött árfolyammal.
- Másolás: a kijelzett forintos árat másolja a vágólapra.

## v0.6.1 színek

Az eredeti sötét, palaszürke felület narancssárga (`#FF9D2E`) kiemelésekkel. A logó és a saját ikon megmaradt. A narancssárga a végső ár kártyáján, a kiválasztott szorzón és a figyelmeztetéseken jelenik meg. v0.6.0-ról a program Frissítések/Frissítés gombjával telepíthető.

## v0.6.0 napi használat és frissítés

- Alapértelmezett szorzó **×1,40**, induláskor és új számításkor automatikusan kijelölve.
- Haszon = kijelzett, egész forintra kerekített eladási ár − pontos bekerülési ár. Az árrés a haszon / eladási ár ×100; két tizedesre megjelenítve. Nulla eladási árnál az árrés nem értelmezhető (—). Ezek nem adó- vagy egyéb költségelszámolások.
- Az ablak minimalizáló gombja az értesítési területre rejti az appot. Az óra melletti logós ikon dupla kattintása vagy **Megnyitás** menüje visszahozza. **Kilépés** vagy az ablak × gombja bezárja a programot.
- Induláskor ellenőrzi a legújabb nyilvános GitHub-kiadást. Új verzió esetén a **Frissítés** gomb jelzi. Gombnyomás és megerősítés után token nélkül letölt, SHA-256 alapján ellenőriz, újraindul és frissít. A beállítások megmaradnak. A korábbi EXE `.previous` biztonsági másolata megmarad az app mellett.
- Nincs PowerShell-hívás az alkalmazás frissítőjében vagy a Windows-letöltőben. A publikus letöltés/frissítés független a GitHub feltöltési token lejáratától. Frissítésellenőrzéshez `api.github.com`, letöltéshez GitHub release/CDN hozzáférés szükséges. A nyilvános API lekérési korlátja érvényes.
- A frissítő ideiglenesen a Windows TEMP mappába tölt. Az app a saját könyvtárához írható jogosultságot igényel; írási/indítási hiba esetén a kézi letöltő használható.
- A fejlesztői updater-teszt: `dotnet run --project tests/CatPriceCalculator.Checks -c Release -- --live-update`.

## v0.5.0 arculat és parancsikon

- LS Germany logó a fejlécben, világos mezőben. A logó áttetsző háttérrel előkészített PNG-je: `src/CatPriceCalculator/Assets/LS-Germany.png`.
- Sötétkék/világoskék színpaletta, a logó színeivel összhangban. A végső ár továbbra is a legfeltűnőbb elem.
- A `.exe`, az ablak és az asztali parancsikon saját LS Germany ikont használ. A többméretű ICO (16–256 px): `src/CatPriceCalculator/Assets/LS-Germany.ico`; beágyazás: `ApplicationIcon` a csproj-ban és `Window.Icon` a XAML-ben.
- A v0.5.0 letöltő az Asztalra **LS Germany - CAT arkalkulator** parancsikont készít, a kicsomagolt EXE-re mutatva.
- Windows alatt az ikon/parancsikon tényleges megjelenése itt nem tesztelhető.

## v0.4.0 változások

- Aszinkron, kizárólag ellenőrző induláskori HUF és EUR lekérés. Nincs automatikus árfolyamcsere vagy beállításmentés.
- Több mint 3%-os eltérésnél sárga figyelmeztetés az adott árfolyam alatt, az aktuális értékkel, forrással és dátummal. Nem tűnik el számítás vagy új tétel indítása miatt.
- **Mindig felül** kapcsoló a fejlécben. Bekapcsolva a kalkulátor más ablakok fölött marad; kikapcsolva visszaáll a szokásos ablakviselkedés. Induláskor kikapcsolt állapotú.
- Nincs új árkerekítési funkció.

## v0.3.0 javítások

- Görgetősáv nélküli fix ablak: az összes elem mindig az ablakon belül marad.
- Elsődleges árfolyamforrás a közvetlen EKB XML; másodlagos forrás Frankfurter. Forrásonként 4 másodperces timeout; sikertelen lekérés megtartja a korábbi értéket.
- EKB irányok: HUF/DKK = (HUF/EUR) / (DKK/EUR), EUR/DKK = 1 / (DKK/EUR). Mindkettő decimal alapú.
- Árfolyamhiba naplója: `%LOCALAPPDATA%\CAT-Price-Calculator\arfolyam-hiba.log`. A felület magyar üzenetet ad; technikai részletek a helyi naplóban.

## v0.2.0 változások

- Dealer felár: 10% (`1.10m`).
- A dealer sor felett a nyers CAT DKK ár EUR-értéke látható, dealer felár nélkül.
- Külön DKK/EUR mező: **1 DKK = X EUR**. Manuális és online mód, mentett forrás/dátum, 8 másodperces timeout, korábbi érték megtartása hibánál.
- Kompaktabb felület, fix 460 × 780 méret, nincs átméretezés vagy maximalizálás. A tartalom egységesen kicsinyedik, ha szükséges, ezért minden gomb látható, görgetősáv nélkül. Kisebb képernyőn az indulási ablakméret a munkaterülethez igazodik, utána nem méretezhető.
- Példa: 1250 DKK, 53,50 HUF/DKK, ×1,40 → dealer 1375 DKK, bekerülési ár 73 562,50 Ft, eladási ár **102 988 Ft**.
- Ugyanaz a nyers 1250 DKK, 0,134 EUR/DKK → **167,50 EUR**, a dealer szorzótól és a HUF számítástól függetlenül.

## Árfolyam és helyi beállítások

Az elsődleges online forrás az **Európai Központi Bank**: `https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml`. Másodlagos forrás **Frankfurter / ECB**: `https://api.frankfurter.dev/v1/latest?base=DKK&symbols=HUF`.
EUR: `https://api.frankfurter.dev/v1/latest?base=DKK&symbols=EUR`.

Ez a legutóbbi rendelkezésre álló referenciaárfolyam, nem valós idejű kereskedési ár. A felület az árfolyam dátumát mutatja; hétvégén/ünnepnapon korábbi lehet.

A lekérés aszinkron, forrásonként 4 másodperces timeouttal. Hiba esetén a korábbi érték megmarad. A lekérés közben szerkesztett manuális árfolyamot a válasz nem írja felül. Induláskor automatikusan ellenőrzi az aktuális HUF és EUR árfolyamot, de nem írja felül a beviteli mezőket, a mentett értékeket vagy az online/manuális állapotot. Több mint 3%-os eltérésnél külön figyelmeztetés látható. A viszonyítás alapja az aktuális online árfolyam: `abs(beállított - aktuális) / aktuális > 0.03`. Pontosan 3% nem jelez. A figyelmeztetés kézi szerkesztés és explicit online frissítés után újraszámolódik. Sikertelen induláskori ellenőrzés mellett a manuális számítás zavartalanul működik.

A HUF árfolyam és online forrásadatai: `%LOCALAPPDATA%\CAT-Price-Calculator\settings.json`. A CAT ár és a választott szorzó nem kerül mentésre; induláskor és új számításkor ×1,40 a szorzó. Az EUR árfolyam külön fájlba kerül: `%LOCALAPPDATA%\CAT-Price-Calculator\euro-settings.json`. A korábbi HUF beállítás változatlanul megmarad. Az EUR alapértéke manuális `0,134` (1 DKK = 0,134 EUR).

Sérült/hiányzó beállítás esetén a program a manuális alapértékre tér vissza.

## Fejlesztés és új build

Telepítsen .NET 10 SDK-t (a projekt 10.0.401-et vagy újabb 10.0.4xx javítóverziót használ).
A repository gyökerében:

```powershell
dotnet build src/CatPriceCalculator/CatPriceCalculator.csproj -c Release
dotnet run --project tests/CatPriceCalculator.Checks -c Release
.\publish-win-x64.ps1
```

Windows alatt a felület indítása:

```powershell
dotnet run --project src/CatPriceCalculator
```

Linux alatt a WPF projekt fordítható és Windowsra publikálható, de a grafikus alkalmazás nem futtatható. A számítási/service tesztek platformfüggetlenek.

Élő API-teszt:

```powershell
dotnet run --project tests/CatPriceCalculator.Checks -c Release -- --live
```

A live teszt valódi pozitív DKK/HUF választ igényel; hálózati hiba esetén sikertelen. Az alap tesztcsomag külön ellenőrzi a számítást, a magyar számokat, a beállításmentést, az API válaszát, a hibás választ, az offline/503 eseteket és a timeoutot.

## Módosítási pontok

- Dealer 1,10 és eladási szorzók: `src/CatPriceCalculator.Core/PriceCalculator.cs` (`DealerMultiplier`, `SellingMultipliers`).
- API lecserélése: `src/CatPriceCalculator.Core/ExchangeRateService.cs` (`Endpoint`, `FetchAsync`).
- Helyi beállításkezelés: `src/CatPriceCalculator.Core/SettingsStore.cs`.
- Felület: `src/CatPriceCalculator/MainWindow.xaml`; események: `MainWindow.xaml.cs`.

## Ellenőrzési eredmények és korlátok

v0.6.1: 58 sikeres automatikus ellenőrzés, továbbá sikeres valódi DKK/HUF és DKK/EUR Frankfurter API-lekérés. A Release build és Windows x64 self-contained publish Linux alatt készül. A Windows-felület, az értesítési terület és a Windows alatti EXE-csere/újraindítás ebben a Linux környezetben nem futtatható. A frissítő hálózati és csomagellenőrzési műveletei platformfüggetlenül tesztelhetők.
