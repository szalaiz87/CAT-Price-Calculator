API-kulcs megjelenítése és API-napló – v1.1.0-beta.4. A v1.0.0 marad a hivatalos stabil.

- Javított DHL-kulcsmező: explicit témaszínek/font/kurzor és középre igazítás; vektoros szemikon a kulcs megjelenítéséhez/elrejtéséhez. Karakterszám jelzi a beillesztett értéket. Mentéskor/lapelhagyáskor újra rejtett, az érték és titkosított mentése megmarad.
- Beállítások / API-napló: kézi lekérés szakasza, magyar idő, HTTP-kód, biztonságos hiba, kitakart csomagszám-vég és eltelt idő. Kulcs, kérésfejléc, URL/query és nyers DHL-válasz nem kerül a naplóba.
- Legutóbbi 200 helyi bejegyzés, hat soros lapozás görgetősáv nélkül; újraolvasás, mappa megnyitása és naplótörlés. Naplóírási hiba nem akadályozza a követést. Törléskor a kulcs, csomagok és keret megmarad.
- Változatlan ablakméret, kalkulátorok, Robo Sanyi kézi frissítés/törlés és két téma. Nincs automatikus csomaglekérés; a napló frissítése csak helyi olvasás.

Windows: CAT-Letoltes-Windows-v1.1.0-beta.4.cmd, PowerShell nélkül. Telepítés előtt zárd be a programot. Appon belül béta csatorna engedélyezése, majd Frissítések. A teljes forrás és a DHL-CONNECTION.md útmutató a kiadásban található.

Ellenőrzés: 273 automatizált ellenőrzés, naplómezők/HTTP-hibák/titokmentesség/korlát/tárolás ellenőrzésével, statikus XAML/kontraszt ellenőrzés és hibamentes Windows x64 publish. Linux alatt a tényleges WPF-felület nem futtatható; saját jóváhagyott DHL-kulcs nélkül élő céges csomagteszt nem történt.
