# DHL bekötés – v1.1.0-beta.2

A Robo Sanyi a **DHL Shipment Tracking – Unified** API-t használja. Ez csomagkövetés; nem címkenyomtatási vagy fuvarrendelési API. Egyetlen saját, jóváhagyott **Consumer Key / API-kulcs** szükséges a hitelesítéshez.

## Beszerzendő adatok

| Adat / jogosultság | Kötelező? | Hol szerezhető be? |
|---|---|---|
| DHL Developer Portal fiók | Igen | [Regisztráció és API-oldal](https://developer.dhl.com/tracking). A regisztráció valódi cégnévvel történjen; céges e-mail használata ajánlott. A szokásos DHL ügyfélportál belépése önmagában nem API-jogosultság. |
| Shipment Tracking – Unified hozzáférés | Igen | Az API oldalán **Get Access → Create App**; add meg az alkalmazást, a céget és a felhasználási célt (saját cégnek érkező csomagok követése). Várd meg a DHL jóváhagyását. |
| Consumer Key / API-kulcs | Igen | Jóváhagyás után **My Apps → saját alkalmazás → Credentials → Consumer Key → Show**. Ezt másold az app Beállítások / DHL API lapjára. |
| Saját DHL csomagszám | A követéshez és teszthez igen | A beszállító feladási értesítéséből, DHL értesítésből vagy ügyfélportálból. Valós, friss csomaggal tesztelj. |
| Éles használati jogosultság és jóváhagyott keret | Üzemi használathoz igen | **My Apps → saját alkalmazás → API-lista → Request Upgrade**. Írd le a csomagszámok mennyiségét, várható napi lekéréseket és a céges felhasználást. A jóváhagyást, feltételeket és keretet a DHL határozza meg. |
| Címzett irányítószáma | Nem kötelező | A csomag célcíméből. Egyes DHL szolgáltatásoknál, különösen Parcel DE/NL esetében részletesebb adatokhoz szükséges lehet. A beállított érték minden DHL lekérésre vonatkozik. |
| DHL szolgáltatás | Nem kötelező | A feladási értesítésből (például DHL Express). Hagyd **Automatikus** értéken, kivéve ha több találat miatt vagy a konkrét szolgáltatáshoz szűkítés kell. |

**Consumer Secret, OAuth client secret és DHL ügyfélszám nem szükséges a program követési kéréséhez.** A DHL ügyfélszámát az igénylés során kérhetik a céges kapcsolat igazolására. Nem a MyDHL felhasználónevet/jelszót kell az alkalmazásba írni.

## Aktiválás az alkalmazásban

1. Telepítsd a béta kiadást, vagy engedélyezd a béta frissítést a Beállítások / Általános alatt, majd kattints a Frissítésekre.
2. **Beállítások → DHL API**: másold be a Consumer Key értékét. Az irányítószám maradhat üres, a szolgáltatás Automatikus, a keret kezdetben 250.
3. Kattints a **DHL beállítások mentése** gombra. Írj be egy saját DHL csomagszámot a tesztmezőbe, majd **Tesztlekérés**.
4. A **Robo Sanyi** lapon válaszd a DHL-t és add hozzá a csomagot; az új DHL-csomagot a program rögtön lekéri. A **DHL frissítés** a saját, még nem kézbesített DHL-csomagokat frissíti. A **Stop** megszakítja a hátralévő lekéréseket; a már mentett válaszok megmaradnak.

Az API-kulcs maszkolt mezőben látható, és Windows DPAPI-titkosítással, az aktuális Windows-felhasználóhoz kötve mentődik a `%LOCALAPPDATA%\CAT-Price-Calculator\dhl-connection.bin` fájlba. Másik gépen vagy Windows-felhasználóval újra meg kell adni. A program nem menti sima szövegként és nem írja naplóba; a HTTP-kérésben kizárólag a `DHL-API-Key` fejlécben használja. A **Mentett API-kulcs törlése** kikapcsolja a kapcsolatot. A kulcsot az alkalmazásba írd, ne nyilvános GitHub-bejegyzésbe vagy chatbe.

## Keret és frissítési működés

- A DHL hivatalos kezdő hozzáférése **250 lekérés/nap és legfeljebb egy lekérés öt másodpercenként**; ezt a DHL fejlesztési célú keretként írja le. Az éles céges használat feltételeit a Request Upgrade jóváhagyása rendezi. A regisztráció nem jelent automatikus éles hozzáférést.
- A program minden DHL kérés között legalább öt másodpercet tart, a tesztlekéréssel együtt. A 24 órás számláló mentett, újraindítással nem nullázódik; ez konzervatív, gördülő 24 órás helyi korlát. A DHL szerverének napi számlálója eltérhet, és más gépek / programok lekérései is beleszámíthatnak. Magasabb helyi keretet csak DHL által jóváhagyott limit alapján állíts be.
- Nincs időzített háttérlekérés. Hozzáadáskor és kézi frissítéskor indul API-kérés. A mintacsomagok nem kérdezhetők le. FedEx/UPS/GLS továbbra is csak helyi bejegyzést tárol, API-integrációjuk későbbi fejlesztés.
- Jogosultsági vagy szolgáltatáshiba és kerettúllépés leállítja a sorozatot; a sor hibajelzést kap, részletei az egérrel rámutatva olvashatók. A már ismert adatok megmaradnak. A nem található / többértelmű csomag nem akadályozza a többi csomag lekérését. A DHL 429 válaszánál az app figyelembe veszi a Retry-After várakozást.
- A `demo-key` mintaadatot ad, ezért ezt a program nem fogadja el saját kapcsolatként.

## Adatok és megőrzés

A státusz, utolsó hely, eseményidő és tervezett érkezés a DHL válaszából származik. A szállítási becslés nem minden csomagnál elérhető. A program nem helyettesíti az utolsó ismert helyet a célcímmel, és nem talál ki hiányzó dátumot.

Időzónával érkező esemény magyar időben látható. Időzóna nélküli eseménynél az eredeti helyi időt mutatja `*` jelöléssel; puszta dátumhoz nem talál ki órát. Az aktuális DHL-lekérés időpontja és a teljes státuszszöveg a sor eszköztippjében olvasható.

Az igazoltan kézbesített csomag az alsó táblázatba kerül, és a kézbesítéstől számított **48 eltelt óra** után törlődik. Ha a DHL nem ad pontos, időzónával ellátott kézbesítési időt, az első API-visszaigazolástól indul a megőrzés, `†` jelöléssel. Egy későbbi lekérés ezt nem hosszabbítja meg. Bezárt programnál a következő indítás végzi a törlést. A csomagadatok helyben a meglévő `robo-sanyi-preview.json` fájlba kerülnek.

## Hivatalos források és ellenőrzés

- [Shipment Tracking – Unified: hozzáférés, kvóták, API, használati feltételek](https://developer.dhl.com/tracking)
- [Hivatalos 1.5.8 OpenAPI-séma](https://developer.dhl.com/sites/default/files/2026-08/track_v1.5.8.yaml)
- Használt végpont: `GET https://api-eu.dhl.com/track/shipments?trackingNumber=…`; hitelesítés `DHL-API-Key` fejléccel.

Ellenőrizve 2026-10-08-án. A tesztek a hivatalos válaszsémát, státuszokat, időket, kereteket, tárolást és szimulált hibákat ellenőrzik. Saját, jóváhagyott API-kulcs hiányában valós céges DHL-csomaggal még nem történt élő teszt. A Windows WPF-megjelenés és DPAPI-futtatás ebben a Linux környezetben nem ellenőrizhető; a Windows build/publish fordítható.
