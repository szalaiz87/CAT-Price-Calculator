# Felhasználói felület – tartós követelmények

- A felhasználó semelyik nézetben nem szeretne görgetősávot (2026-10-07).
- A teljes felület férjen el az ablakban. A teljes tartalom arányos méretezése megengedett, tartalom levágása vagy a sáv egyszerű elrejtése nem.
- A v0.7.0-hoz képest körülbelül 25%-kal tömörebb megjelenés, kisebb térközök és panelek.
- Sötét, modern neonkékre/cianra épülő arculat; aktív gomb és beviteli fókusz hangsúlyos.
- Árfolyamnap és a tényleges sikeres lekérés dátuma/ideje külön szerepeljen. A lekérési idő mentés után is maradjon meg; régi mentéshez ne találjunk ki időpontot.
- CAT és EUR kalkulátor egységes komponensekkel. A csomagkövető felület v1.1.0-beta.1-től külön lapon, v1.1.0-beta.2-től DHL API-kapcsolattal érhető el; v1.1.0-beta.5-től UPS-integráció is elérhető; FedEx/GLS még API nélküli.

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

- v1.1.0-beta.1: harmadik funkcionális menüpont Robo Sanyi, vektoros robotikonnal. Felül csomagszám, logós futárlista és megjegyzés, helyi hozzáadás; alatta úton lévő és kézbesített csomagok két fix táblázata. Négy sor/táblázat, külön lapozás, üres állapotok. Nincs görgetősáv vagy új ablakméret. A hosszú azonosító/megjegyzés teljes tartalma a sor eszköztippjében olvasható.
- Eredeti internetes DHL/FedEx/UPS/GLS SVG-kből fagyasztott WPF-vektorok, fehér logójelvényeken. Mindkét témában közös színek, 10-es kártyasarkok, éles hover/fókusz; kézbesítettnél kontrasztos zöld státusz. MINTA jelölés és API nélküli előnézet felirat; valósnak tűnő kitalált követési adat nem adható saját csomaghoz.
- A bal menüsáv logo-/felirattérközei összesen 52 referenciapixellel tömörebbek, ellensúlyozva az új 50 pixeles menüsor igényét. Külső keret és kalkulátorméretek változatlanok. A Robo Sanyi saját kompakt oldalsó összegzést kap. Kézbesítéskor 48 órás megőrzés, hátralévő idő kijelzése; percenkénti és induláskori helyi tisztítás.

- v1.1.0-beta.2: Beállítások fejlécében két alfül, Általános és DHL API; így az API-kulcs, opcionális irányítószám/szolgáltatás, 24 órás keret, mentés/törlés, teszt és hozzáférési útmutató a változatlan ablakban fér el. Maszkolt kulcsmező közös 10-es sarkokkal és éles fókuszkerettel, sötét/világos dinamikus színekkel; a futárlista stílusa közös erőforrás.
- Robo Sanyi: DHL frissítés, megszakítás és DHL API gyorsgomb a fix alsó sorban, mintagombok a fejlécben. A két négysoros táblázat és lapozás változatlan. Hibajelzés a soron, teljes DHL-státusz és lekérési idő az eszköztippben; időzóna nélküli időpont *, észleléstől számított megőrzés † jelöléssel. DHL-attribúció az alsó sorban. Hiányzó követési adatok nem helyettesíthetők feltételezéssel.

- v1.1.0-beta.3: csak kézi csomaglekérés, hozzáadáskor sincs API-hívás. Összes frissítése a Robo Sanyi alsó sorában, úton lévő sorok végén vektoros frissítésikon, minden sor végén kuka. 28×28-as, lekerekített gombok közös éles hover/fókusszal és akadálymentes nevekkel; minták/API nélküli futárok frissítése inaktív, magyarázó eszköztippel. Új 68 pixeles műveletoszlop, tömörebb adatoszlopok, hosszú helynév eszköztippben. Változatlan ablak és táblázatmagasság, négy sor és lapozás, nincs görgetősáv. Frissítés közbeni törlés nem hozhatja vissza a csomagot a válasz megérkezésekor.

- v1.1.0-beta.4: az API-kulcs mezőjének szöveg-, font- és kurzorbeállításai explicit módon követik a témát. Csökkentett függőleges belső tér és középre igazított tartalom, állandó mezőmagasság. Jobb szélen 30×30-as, lekerekített vektoros szemgomb, szöveg számára fenntartott hely. Karakterszám a címkesorban; a gomb nem módosítja a kulcsot és a piszkos/mentett állapotot. A megjelenítés elrejtődik mentéskor és lapelhagyáskor. Hosszú kulcs vízszintesen belül követi a kurzort látható görgetősáv nélkül; normál kalkulátormezők görgetési beállítása változatlan.
- Beállítások harmadik alfüle API-napló, vektoros dokumentumikonnal. Hat fix magasságú sor és külön lapozás, kulcsot nem tartalmazó részletes eszköztipp; helyi újraolvasás, mappa megnyitása és naplótörlés. Hiba kiemelt, éles kontrasztos szöveggel. Az ablak, kalkulátorok és Robo Sanyi méretei változatlanok; nincs görgetősáv vagy automatikus hálózati lekérés.

- v1.1.0-beta.5: a Beállítások fejlécén négy fix alfül (Általános, DHL API, UPS API, API-napló). UPS vektorlogó, a DHL-éhez hasonló kártyák; két közös SecretEntry szemgombbal és karakterszámmal, explicit dinamikus színek/font/kurzor és rejtett alapállapot. Mentéskor és lapelhagyáskor a megjelenített másolat ürül. Opcionális ügyfélszám és egyértelműen helyi keret, mentés/törlés, kézi teszt és hozzáférési útmutató a fix 816-as területen. Robo Sanyi soronkénti/összes frissítés DHL/UPS-hoz; API gyorsgomb a kiválasztott futár fülére visz. Közös napló futárjelöléssel. Nincs új ablakméret, görgetősáv, automatikus csomaglekérés vagy kitalált adat.

- v1.1.0-beta.6: a DHL kulcsmező is a UPS-szel közös SecretEntry-t használja, ugyanaz a 66 pixel (18+6+42), karakterszám/szemikon/dinamikus téma és rejtett alapállapot. Továbbra is négy beállításfül, azonos ablak- és kártyaméretek, nincs görgetősáv. Percenkénti helyi tisztítás megmarad; rejtett Robo Sanyi esetén nincs táblázat-újraépítés, látható oldalon csak a megérkezett táblázat időfüggő kijelzése frissül. A csomaglapozás közös, stabil rendezési holtversennyel és négy látható sorral.

- v1.1.0-beta.7: hat beállításfül változatlan 32 pixeles sorban: Általános / DHL / UPS / FedEx / GLS / API-napló; futárnév mellett egyforma 28×18 fehér, lekerekített vektorlogó-terület. Egy közös futárszerkesztő; 392/180/190 pixeles kapcsolat/teszt/útmutató kártyák, 12 pixeles hézagok minden futárnál. Titkos mezők azonos 66 pixeles sorban; DHL második kulcs helyén tájékoztatás, az alsó mezők/gombok nem mozdulnak. Hozzáférés mentése / Hozzáférés törlése / Kapcsolat tesztelése / Helyi 24 órás keret elnevezések egységesek; a valós hitelesítési címkék futárspecifikusak. Megmarad a két téma, draftok, szemgombok és védett mentés. Robo Sanyi 100 pixeles fejlécén ugyanaz a munkagépes fotó, lekerekített keret, témához illeszkedő színátmenet, HeroTitle és Linser Hungary felirat van, mint a kalkulátoroknál. Jobb oldalt mintagombok és kompakt négyfutáros béta jelzés. Nincs új grafikai fájl, ablak-/táblaméret-változás vagy görgetősáv. MyGLS ETA híján Még nincs adat; ismeretlen adatok továbbra sem találhatók ki.
