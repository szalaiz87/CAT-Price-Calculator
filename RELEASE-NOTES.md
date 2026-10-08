DHL csomagkövetés – v1.1.0-beta.2. A v1.0.0 marad a legfrissebb hivatalos stabil.

- DHL Shipment Tracking – Unified a Robo Sanyi lapon: valós státusz, utolsó hely/esemény, tervezett érkezés. Saját DHL-csomag hozzáadáskor automatikus, egyébként kézi frissítés; megszakítható sorozat. Minták, FedEx, UPS és GLS nem indítanak DHL-kérést.
- Beállítások / DHL API: Consumer Key védett Windows DPAPI-mentéssel, kulcstörlés, opcionális irányítószám/szolgáltatás, jóváhagyott keret és saját csomagszámos kapcsolatpróba. Consumer Secret és OAuth nem kell. Hozzáférési lista: DHL-CONNECTION.md.
- Legalább öt másodperc két kérés között, mentett gördülő 24 órás keret (alapból 250), DHL Retry-After kezelés. Hibák esetén megmaradó korábbi adatok, sorjelzés és részletes eszköztipp. A kezdő DHL-keret fejlesztési célú; éles felhasználáshoz Request Upgrade és DHL-jóváhagyás szükséges.
- Változatlan ablakméret és kalkulátorok, két téma, görgetősáv nélküli lapozás és 48 órás törlés. Ismeretlen időzónánál eredeti idő *, pontos kézbesítési idő hiányában első igazolt észleléstől megőrzés †. Korábbi béta csomagadatok átvehetők.

Windows telepítés: CAT-Letoltes-Windows-v1.1.0-beta.2.cmd, PowerShell nélkül. Futtatás előtt zárd be a programot. Az alkalmazáson belül a Beállítások / Általános alatt engedélyezd a béta frissítéseket, majd Frissítések. A stabilra visszatérés megmarad.

Hozzáférés: https://developer.dhl.com/tracking → Get Access → Create App → jóváhagyás → My Apps → saját alkalmazás → Credentials → Consumer Key → Show. A kulcsot kizárólag az appban add meg. Üzemi jogosultság/keret: a saját alkalmazás API-listájában Request Upgrade.

Ellenőrzés: 246 automatizált ellenőrzés, hivatalos DHL-válaszséma és szimulált jogosultsági/kvóta/hálózati hibák; hibamentes Windows x64 publish. Saját jóváhagyott DHL-kulcs hiányában élő céges DHL-csomaggal még nem volt teszt. Linux alatt a WPF-megjelenés és a Windows DPAPI-futtatás nem ellenőrizhető.
