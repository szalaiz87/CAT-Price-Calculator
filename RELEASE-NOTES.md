UPS csomagkövetés – v1.1.0-beta.5. A v1.0.0 marad a hivatalos stabil.

- Beállítások / UPS API: külön fül, UPS vektorlogó, Client ID és Client Secret szemgombbal/karakterszámmal, védett mentés/törlés, opcionális ügyfélszám, helyi 24 órás keret és kézi kapcsolatpróba. Hozzáférési útmutató: UPS-CONNECTION.md.
- Robo Sanyi: DHL és UPS soronkénti / összes kézi frissítés; státusz, utolsó ismert hely és idő, tervezett kiszállítás. OAuth csak a kézi művelet részeként; lejáratig memóriatoken, korlátozott újítás. Nincs automatikus csomaglekérés.
- Közös API-napló futárjelöléssel és OAuth/követés/feldolgozás szakaszokkal. Kulcs, token, ügyfélszám, teljes csomagszám, fejléc, URL/query és nyers válasz nem naplózható. Egy futár hozzáférési/kvótahibája nem állítja le a másik frissítését.
- Közös titkos tárolás és csomagfrissítési motor; régi DHL-mentések/napló megmaradnak. Változatlan ablakméret, két téma, lekerekítés, lapozás, 48 órás csomagmegőrzés és görgetősáv nélküli elrendezés. FedEx/GLS még API nélküli.

Windows: CAT-Letoltes-Windows-v1.1.0-beta.5.cmd, PowerShell nélkül. Telepítés előtt zárd be a programot. Appon belül béta csatorna engedélyezése, majd Frissítések. A forrás, DHL-CONNECTION.md és UPS-CONNECTION.md a kiadásban található.

Ellenőrzés: 372 automatizált ellenőrzés, hivatalos UPS-séma szerinti szimulált OAuth/Tracking-válaszokkal és a korábbi funkciók ellenőrzésével; hibamentes Windows x64 publish. Saját céges API-adatok nélkül élő UPS-csomagteszt még nem történt. Linuxon a tényleges WPF-felület és Windows DPAPI-futtatás nem ellenőrizhető. A beállított UPS 250/24 órás keret helyi védelem, nem szolgáltatói kvóta.
