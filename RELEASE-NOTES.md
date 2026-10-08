Robo Sanyi kézi csomagkezelés – v1.1.0-beta.3. A v1.0.0 marad a legfrissebb hivatalos stabil.

- Csomagkövetés kizárólag kézi indítással: hozzáadáskor, induláskor, lapváltáskor és időzítve sincs automatikus API-kérés. A Beállítások kapcsolatpróbája továbbra is csak a Tesztlekérés gombbal indul.
- Összes frissítése gomb a Robo Sanyi lapon, az összes oldal úton lévő, támogatott saját csomagjához. Soronkénti frissítésikon az úton lévő csomagok végén. Jelenleg DHL; minták és API nélküli futárok frissítése inaktív, a kézbesített csomagok kimaradnak.
- Kukaikon minden sor végén, mindkét táblázatban. Törlés a mentett listából, frissítés közben is; a később érkező API-válasz nem hozza vissza a törölt csomagot.
- Változatlan ablak- és táblázatméret, négy sor és külön lapozás, görgetősáv nélkül. Lekerekített, vektoros műveletgombok sötét és világos témában. DHL-kulcs, keret, öt másodperces ütemezés, 48 órás helyi törlés és kalkulátorok megmaradnak.

Windows letöltő: CAT-Letoltes-Windows-v1.1.0-beta.3.cmd, PowerShell nélkül. Telepítés előtt zárd be a programot. Az alkalmazásban a béta csatorna engedélyezése után Frissítések. A legfrissebb stabilra visszatérés megmarad.

Ellenőrzés: 252 automatizált ellenőrzés, statikus XAML- és eseménykötés-ellenőrzés, hibamentes Windows x64 publish. A tényleges Windows WPF-felület Linux alatt nem futtatható; saját jóváhagyott DHL-kulcs nélkül élő céges csomagteszt nem történt. A teljes forrás és a frissített DHL-CONNECTION.md útmutató a kiadásban található.
