# FedEx – beérkező csomagok kézi követése

A v1.1.0-beta.7 a **Basic Integrated Visibility / Tracking API** éles, csomagszám alapú követését használja. A DHL/UPS/FedEx/GLS fülek azonos szerkesztőt, mentési/törlési és kézi tesztgombokat használnak. A céges FedEx-belépés önmagában nem API-hozzáférés.

## Szükséges adatok és beszerzésük

1. Nyisd meg a [FedEx Developer Portal](https://developer.fedex.com/) oldalt. A saját céges szervezethez hozz létre vagy válassz ki egy projektet; társítsd a céges FedEx ügyfélszámot.
2. A projektben engedélyezd a **Basic Integrated Visibility / Tracking API** szolgáltatást, és intézd el az éles hozzáférést. A fizetős Advanced Integrated Visibility külön termék; a program a szokásos csomagszámos Tracking API-t használja.
3. A **Project Overview / Production keys** területén keresd az **API Key (Client ID)** és **Secret Key (Client Secret)** értékét. A titkos kulcsot létrehozáskor/regeneráláskor mutathatja a portál. Sandbox-kulcsokkal az éles kapcsolat nem működik.
4. Az alkalmazás **Beállítások → FedEx** fülén add meg a két kulcsot és nyomd meg a **Hozzáférés mentése** gombot. Az ügyfélszámot a fejlesztői projektben kell társítani; a csomagszámos kéréshez itt nem kell külön megadni.
5. Egy saját FedEx csomagszámmal indítsd a **Kapcsolat tesztelése** műveletet. Eredmény és biztonságos diagnosztika: **Beállítások → API-napló**.

Saját céges projekthez `client_credentials` hitelesítés kell. FedEx-integrátor/CSP vagy parent-child projekt más hitelesítési modellt (`csp_credentials`, `client_pc_credentials`, további child key/secret) igényelhet; ezeket a program nem támogatja. Ilyen projekt esetén kérj a FedEx-től saját céges, standard Client Credentials hozzáférést.

## Működés és korlátok

- OAuth: `POST https://apis.fedex.com/oauth/token`, form-urlencoded `grant_type=client_credentials`, `client_id`, `client_secret`. FedEx-nél nincs UPS-féle HTTP Basic fejléc.
- Követés: `POST https://apis.fedex.com/track/v1/trackingnumbers`, kérésenként Bearer-fejléc, `includeDetailedScans=true`, a megadott csomagszám. A token csak memóriában marad; a következő kézi műveletkor újul meg, lejárat vagy egyetlen 401-utáni újrapróbálás esetén.
- Kizárólag **Összes frissítése**, aktív csomagsor **Frissítés**, illetve a beállítások **Kapcsolat tesztelése** gombja hív API-t. Hozzáadás, indulás, lapváltás és időzítő nem kér adatot és nem hitelesít a háttérben.
- A legutóbbi ismert scan-helyet a saját időpontjával mutatjuk; a címzett címe nem helyettesíti. ETA csak a FedEx strukturált dátummezőjéből; kézbesítési idő a tényleges delivery adatból. Több feladás/történeti csomagszám esetén nem találgatunk.
- A **Helyi 24 órás keret** alapból 250 HTTP-kérés, hitelesítéssel együtt; ez **nem hivatalos FedEx-kvóta**. Futáronként mentett keret és legalább 5 másodperces HTTP-ütemezés; az éles projekt tényleges korlátját a portálon ellenőrizd. 429 esetén a szolgáltató Retry-After ideje érvényes.
- Windows DPAPI CurrentUser védett mentés; kulcs, token, HTTP-fejléc, URL, nyers válasz és teljes csomagszám nem kerül a naplóba. Titkosítási hiba esetén nincs egyszerű szöveges mentési tartalék.
- Megérkezett csomagok 48 eltelt óráig maradnak. Ha a FedEx nem ad biztos, időzónás kézbesítési időt, az első megerősített észleléstől számolunk (†). A megőrzést újabb frissítés nem hosszabbítja meg.

## Hivatalos források

Ellenőrizve: 2026-10-08.

- [Tracking API leírás és hozzáférés](https://developer.fedex.com/api/en-us/catalog/track/v1/docs.html)
- [Tracking Numbers hivatalos OpenAPI séma](https://developer.fedex.com/wirc/json/api_groups/Track/TrackingNumbers-Resource.json)
- [Authorization API, projekttípusok és kulcsok](https://developer.fedex.com/api/en-us/catalog/authorization/v1/docs.html)

Élő céges API-kulccsal próba nem történt; a kérés/válasz, OAuth és hibakezelés hivatalos sémára épülő, hálózat nélküli HTTP-fixture-ökkel ellenőrzött.
