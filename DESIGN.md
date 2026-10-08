# Felhasználói felület – tartós követelmények

- A felhasználó semelyik nézetben nem szeretne görgetősávot (2026-10-07).
- A teljes felület férjen el az ablakban. A teljes tartalom arányos méretezése megengedett, tartalom levágása vagy a sáv egyszerű elrejtése nem.
- A v0.7.0-hoz képest körülbelül 25%-kal tömörebb megjelenés, kisebb térközök és panelek.
- Sötét, modern neonkékre/cianra épülő arculat; aktív gomb és beviteli fókusz hangsúlyos.
- Árfolyamnap és a tényleges sikeres lekérés dátuma/ideje külön szerepeljen. A lekérési idő mentés után is maradjon meg; régi mentéshez ne találjunk ki időpontot.
- CAT és EUR kalkulátor egységes komponensekkel. Csomagkövetés nincs ebben a kiadásban.

- v0.7.2: fülváltáskor a külső referenciakeret, panelmagasságok és betűméretek állandók. Méretezés csak az ablak méretétől függhet. Külső margó 3 (korábban 6).
- Mindkét kalkulátor ugyanazt a ×1,30–×2,00 szorzólistát használja.
- A natív fejléc sötét arculatot kap, pontos színezés Windows 11 támogatással.

- v0.8.0: Beállítások külön lap, vektoros fogaskerékikon, bal alul elkülönítve. Sötét/világos téma az egész appra és natív fejlécre, helyben mentett választással. Fülváltás és témaváltás nem változtathatja a rögzített keret méretét.

- v0.8.1: gombok/menük kiemelése éles kerettel, a feliratot elmosó árnyékeffekt nélkül. Beállítások szabályos nyolcfogú vektoros fogaskerékkel.

- v0.8.2: Mindig előtérben kapcsoló globálisan a bal oldali menüben, csúszókapcsolóként. A raster LIS logó helyett DrawingImage vektor. Egységes 10-es kártya/mező/gomb saroksugár; a kapcsoló teljesen kerekített.

- v0.8.3: minimalizálás csak normál Windows-tálcára; értesítési ikon és Hide tiltva. >3% figyelmeztetés rövid látható szöveggel, teljes metaadat eszköztippben. Jobb kártyák rögzített Grid-sorokban, alsó lekerekítéssel az ablakon belül.

- v0.9.0: Beállításokban legújabb verziólog legfeljebb négy ponttal. Mentett induláskori Topmost és automatikus árfolyamfrissítés. Aktuális munkamenet Topmost és következő indítás preferenciája külön kezelve.

- v0.10.0: árfolyamok a bal menüsávban, menüpontok és Beállítások között; nincs jobb oszlop vagy Hasznos információk doboz. 3 sor × 5 oszlopos gombok. Generált fotóhatású CAT-háttér. 890 × 940 rögzített referenciakeret, képernyőhöz igazodó kisebb ablak.

- v0.11.0: kizárólag vizuális redesign. Navy/acélkék panelek, közös szemantikus szín- és gradient-erőforrások, Segoe tipográfia, konzisztens 10-es sarkok. Keskenyebb (224) menüsáv; Beállítások és Frissítések együtt az alsó utility zónában. Aktív menü vékony cian széljelöléssel; kiválasztott szorzó kék gradienttel; hover/fókusz nem változtat komponensméretet és nem használ feliratot elmosó effektet.
- A fő nézet rögzített soros Grid: fotós hero, mezők, összegzés, 3×5-ös szorzórács, eredménykártya, műveletek és státusz. Az árfolyam-metaadat két teljes sorban, a figyelmeztetés külön fenntartott sorban. CAT/EUR váltáskor minden szakasz mérete azonos; világos téma és mentés változatlan.
- Közös erőforrások: Styles/Colors.xaml, Typography.xaml, Controls.xaml, Branding.xaml. Effektmentes rétegezés és finom peremek helyettesítik a költséges elmosást/árnyékot. A meglévő fotó és vektoros logó változatlan, egyszer töltenek be.

- v0.11.1: a két kalkulátor közös beszerzési ármezőjén belül, jobb oldalon vektoros X törlőgomb. Csak az árat törli, visszaadja a mezőfókuszt; árfolyam, szorzó és másik fül mentett értéke nem változik. Üres mezőnél inaktív. 30×30 gomb, közös 10-es saroksugár, éles hover-/fókuszkeret, nincs elmosás vagy geometriai ugrás. Szöveg számára fenntartott jobb oldali padding.
- Teljes világos paletta: világos menüsáv és panelek, fehér inputok, kék aktív jelölések; világos fotóátfedés a fejlécen, világoskék eredménykártya sötét szöveggel. Közös dinamikus brush-ek a fejlécben, aktív menün és eredményen. Témaváltáskor a figyelmeztetések visszafogott színe is frissül. A választás mentése, sötét téma és rögzített referenciakeret megmarad.

- v0.11.2: optimalizálás során az aktív arculat, színek, méretek és kötések változatlanok. Csak használatlan erőforrások törölhetők; dinamikus témaszínek nem fagyaszthatók. Vektorok/fotóforrás változatlan tartalommal fagyasztható. Online árfolyamváltásnál a végső újraszámítás megmarad; ár törlése, kézi beírás és revision-védelem változatlan.

- v1.0.0: Beállításokban külön Frissítési csatorna kártya, a közös csúszókapcsolóval és lekerekített gombokkal. A béta-opt-in alapból kikapcsolt és mentett. A stabilra visszatérési gomb csak béta buildben látszik; helye a kártyában fenntartott. A futó verzió és csatorna látható. Tömörebb Beállítások-kártyák a meglévő fix referenciakereten belül; nincs görgetősáv vagy nézetváltási méretváltozás.
