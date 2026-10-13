# Google Táblázatok szerviznaptár – v1.2.0-beta.10

Az app a naptár forrása. A Google-táblázat a szerelők olvasási nézete: egy közös feladatlista és öt szerelőnkénti lap. A szerelői lapokon a mai és későbbi munkák dátum szerint szerepelnek; telefonon a saját névvel jelölt lapot érdemes megnyitni. A közös lap minden helyi munkát tartalmaz. Ez listanézet, nem a WPF havi rács másolata.

## Google-oldali beállítás, ingyenes személyes fiókkal is

1. Lépjen be a saját Google-fiókjába, és hozzon létre egy külön Google-táblázatot: https://sheets.new . Például: Linser szerviznaptár. Másolja ki a teljes hivatkozását.
2. Nyissa meg a https://console.cloud.google.com/ oldalt. A projektválasztónál hozzon létre új projektet, például Linser szerviznaptár. Ez nem igényel Google Workspace-előfizetést. A Sheets API-hoz általában nem szükséges fizetési fiók; ez a fejlesztés nem használ fizetős felhőszolgáltatást.
3. A kiválasztott projektben: APIs & Services / Library (API-k és szolgáltatások / Könyvtár). Keressen rá: Google Sheets API. Nyomja meg az Enable (Engedélyezés) gombot. Drive API nem szükséges.
4. IAM & Admin / Service Accounts (IAM és adminisztrálás / Szolgáltatásfiókok) / Create service account. Név: linser-szerviznaptar. A projekt szintű szerepkör és felhasználói hozzáférés mezőket hagyja üresen; ehhez a megoldáshoz nem kell Owner/Editor projektjog.
5. Nyissa meg a létrehozott szolgáltatásfiókot. Keys / Add key / Create new key / JSON. A böngésző letölti a JSON-kulcsot. Ha a szervezeti szabály tiltja a kulcslétrehozást, a Google-adminisztrátor segítsége szükséges.
6. Az app Beállítások / Google Táblázatok lapján adja meg a táblázat hivatkozását, majd tallózza be a JSON-fájlt. A megjelenő szolgáltatásfiók e-mail-címét másolja ki.
7. A Google-táblázat Megosztás gombjánál adja hozzá ezt a szolgáltatásfiók-címet **Szerkesztő** joggal. Ez a gépi feltöltéshez kell; nem a szerelők jogosultsága.
8. Az appban Beállítások mentése, Kapcsolat tesztje, majd Naptár szinkronizálása. A teszt olvasási hozzáférést ellenőriz; az első sikeres szinkron igazolja az írásjogot, és létrehozza a LINSER lapokat.
9. A Google-táblázatot ossza meg a szerelők konkrét Google-fiókjaival **Megtekintő** joggal. A link birtokában bárki típusú megosztásra nincs szükség. A szerelők Google Táblázatok appban vagy böngészőben megnyithatják, és a saját nevükkel jelölt lapra válthatnak.
10. Ha a kézi próba rendben van, az appban bekapcsolható az Automatikus feltöltés mentés után és induláskor opció. Mentse ismét a beállítást.

A Google-fiók jelszavát nem kell az appba írni. A JSON-kulcs titkos belépési adat: ne tegye közös ügyfélimport-mappába, ne ossza meg a szerelőkkel, és ne küldje el ebben a beszélgetésben. Az app Windows DPAPI CurrentUser titkosítással menti a kulcs tartalmát. Az eredeti letöltött fájlt a mentés nem módosítja.

## Jogosultság az appban

Az adminisztrátor automatikusan hozzáfér. Normál felhasználónál Beállítások / Fiókok / Google Táblázatok külön engedélyezhető. Ez a jogosultság védi a kulcsimportot, konfigurálást, tesztet és kézi szinkront. Az Általános fül továbbra is mindenkinek elérhető.

Az admin által bekapcsolt automatikus feltöltés a szerviznaptár használatára jogosult felhasználók munkarögzítéseit is átviszi, akkor is, ha a Google-beállításokat nem kezelhetik. A Google-táblázat megosztási jogait továbbra is a Google Megosztás ablakában kell kiosztani.

## Szinkron működése

- A külön `LINSER · Munkák` és öt `LINSER · [munkatárs neve]` lapot az app kezeli. Szinkronkor ezek adatai az aktuális helyi naptárra cserélődnek. Ezekbe ne rögzítsen kézzel adatot; a Google-módosítások nem kerülnek vissza az appba.
- Más munkalapokat nem módosít. Azonos nevű, de appjelölés nélküli lap esetén leáll, és nem írja felül.
- A munkák állandó azonosítója is feltöltődik; ismételt szinkron nem hoz létre másolatokat. Helyi törlés/módosítás a következő teljes szinkronban átvezetődik.
- A cellákat szövegként írjuk: például a +36-os telefonszám és a kezdő nullák megmaradnak, a hibaleírás nem lesz Google-képlet.
- A teljes frissítés egy Google batchUpdate művelet. Hálózati hiba esetén a helyi naptár és a legutóbbi sikeres szinkron időpontja megmarad. Ha a Google már feldolgozta a kérést, de a válasz elveszett, a következő szinkron biztonságosan ismételhető.
- Automatikus módban induláskor és munkamentés után feltölt. Sikertelen vagy közben újabb mentés miatt függőben maradt feltöltést futó app mellett ötpercenként újrapróbál, szerviznaptárhoz jogosult munkamenetben. Nincs Google-kérés a beállításlap megnyitásából.
- A mobilnézet frissítéséhez az appnak futnia kell. Bezárt app mellett a szerelők a legutóbb feltöltött adatokat látják. A lapon szereplő magyar frissítési idő alapján ellenőrizhető a frissesség.
- A helyi szervizadatok továbbra is számítógépenként külön tárolódnak. Egy kijelölt PC szinkronizáljon ehhez a táblázathoz; több külön helyi naptár ugyanabba a táblázatba szinkronizálása felülírná egymást.
- Az első változat egy szinkroncsomagra 2 MB-os korlátot alkalmaz. E fölött nem küld módosítást, hanem érthető hibát ad; később archiválás vagy kötegelt feltöltés szükséges.

Hivatalos dokumentáció: https://developers.google.com/identity/protocols/oauth2/service-account és https://developers.google.com/workspace/sheets/api/reference/rest/v4/spreadsheets/batchUpdate .
