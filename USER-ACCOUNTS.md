# Helyi felhasználói bejelentkezés – v1.2.0-beta.7

Induláskor csak a munkagép hátterű bejelentkező ablak jelenik meg. Az utolsó sikeres belépés felhasználóneve előre kitöltött, a jelszómező üres; az alkalmazás felülete csak sikeres belépés után nyílik meg. Kijelentkezéskor a szerviznaptár és ügyfélkereső ablakok bezáródnak, és ismét a megjegyzett felhasználónévvel és üres jelszómezővel jelenik meg a bejelentkezés.

Az első indításkor létrehozott adminisztrátor felhasználóneve és megjelenített neve **Szalai Zoltán**, kezdeti jelszava **12345**. A Beállítások / Fiókok lapon szerkeszthető, így a kezdeti jelszó lecserélhető. A felhasználónév kis- és nagybetűtől, valamint a körülötte lévő szóközöktől független; a jelszó pontosan, az ékezetekkel, szóközökkel és betűmérettel együtt értendő.

## Adminisztrátori kezelés

- Háromsoros, lapozható lista az összes felhasználóval; név, felhasználónév, szerepkör, rejtett jelszó, vektoros megjelenítés/szerkesztés/törlés gombok.
- Új felhasználó létrehozása vagy meglévő szerkesztése: név, felhasználónév, jelszó és Adminisztrátor / Felhasználó szerepkör.
- A szemikon külön kattintásra mutatja a jelszót. Lapelhagyáskor, lapozáskor, ablakváltáskor, mentéskor és kijelentkezéskor a megjelenített jelszavak ismét rejtettek.
- A felhasználónevek egyediek; a saját aktív adminfiók nem törölhető és nem fokozható le. Mindig marad adminisztrátor. Másik adminisztrátor felvehető és korlátlan jogosultságot kap.
- Sikeres mentés után a beviteli mezők kiürülnek. Hibás vagy sikertelen mentés nem változtatja meg a korábbi fiókot.

## Jogosultságok

A normál felhasználónál külön jelölőnégyzet tartozik a CAT, EUR, Szerviznaptár, Robo Sanyi és Frissítések menühöz, valamint a DHL, UPS, FedEx, GLS, API-napló és Szerviznaptár-import beállításfülekhez. Az Általános fül mindig elérhető, ezért nem kapcsolható ki. A Fiókok kezelés kizárólag adminisztrátornak érhető el. Az adminisztrátor minden menüt és beállítást elér; a jelölőnégyzetek ilyenkor bekapcsoltak és inaktívak.

Új normál felhasználónál kezdetben az öt főmenü engedélyezett, a külön beállításfülek nem; ezek mentés előtt szabadon átállíthatók. A tiltott menük és fülek nem jelennek meg. A futárbeállítások gyorsgombja, a frissítési műveletek és a kalkulátor gyorsbillentyűi is ellenőrzik a hozzáférést. Ha egyetlen fő funkció sem engedélyezett, belépés után az Általános beállítások nyílnak meg. Egy fiók módosított jogosultságai a következő bejelentkezéskor kerülnek a munkamenetébe.

## Tárolás és hatókör

Ez a verzió helyi fiókkezelést tartalmaz. Az adatok az aktuális Windows-profil `%LOCALAPPDATA%\CAT-Price-Calculator\users.bin` fájljában vannak, Windows DPAPI CurrentUser titkosítással. A fájl más Windows-fiókban nem olvasható. A bejelentkezés sózott PBKDF2-SHA256 ellenőrzést használ; az adminisztrátori visszanézhetőséghez a jelszó szintén a titkosított adatbázisban található. Nincs jelszónaplózás, automatikus belépés vagy jelszó a munkamenetobjektumban.

A fiókok, jogosultságok és naptáradatok még nem szinkronizálódnak számítógépek között. Az alkalmazáson belüli jogosultságok nem helyettesítik a Windows fájlhozzáférési védelmét. Központi többgépes használathoz a későbbi szerveroldali hitelesítés és adatbázis szükséges.

Meglévő, hibás vagy nem visszafejthető fiókfájl esetén a bejelentkezés leáll, és az eredeti fájl megmarad; a program nem állítja vissza automatikusan a kezdeti adminjelszót.

## Ellenőrzés

Automatizált ellenőrzések: megfelelő/hibás belépés, titkosított tárolás, jogosultságok, adminisztráció tiltása normál felhasználónak, duplikált név, pontos jelszókezelés, módosítás/törlés, újraindítás, sikertelen titkosítás és sérült adatfájl. A titkosítási tesztek Linuxon tesztprotektort használnak; az éles DPAPI-t Windows biztosítja.

Windows kézi átvételhez: első belépés; normál fiók felvétele; eltérő menü- és fülengedélyek kipróbálása; szemikonok és témák; kijelentkezés nyitott naptár mellett; újraindítás és új jelszó; ablakméretezés. Linuxon a WPF-felület vizuális futtatása nem végezhető el.

## Felhasználónév megjegyzése (v1.2.0-beta.8)

Csak sikeres hitelesítés után mentjük a tényleges fiók felhasználónevét a helyi `login-preference.json` fájlba. Sikertelen belépés nem cseréli le a korábbi nevet. Első indításkor vagy hibás preferenciafájlnál a név üres. A név szerkeszthető, másik felhasználó is beléphet. Nincs mentett jelszó ebben a fájlban és nincs automatikus belépés. Kitöltött névnél a fókusz a jelszómezőre kerül.

A szerviznaptár rögzítőmezője automatikusan a bejelentkezett felhasználó megjelenített nevét mutatja, nem szerkeszthető. A munka ezzel a névvel kerül a naptárba; a mező az adatbekérő ürítése után is megmarad. A régi bejegyzések rögzítőnevei változatlanok.
