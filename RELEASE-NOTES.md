Első béta kiadás: Robo Sanyi csomaglap, egyelőre futár-API integráció nélkül.

- Új Robo Sanyi menüpont. Felül csomagszám, logós futárválasztó (DHL, FedEx, UPS, GLS), megjegyzés és helyi hozzáadás.
- Két egymás alatti táblázat: úton lévő, majd megérkezett csomagok. Külön lapozás, görgetősáv nélkül, változatlan ablakmérettel; sötét és világos megjelenés.
- Vektoros internetes futárlogók és külön jelölt mintacsomagok. A saját csomag követési adatokra vár; a prototípus nem kérdez le futár-API-t.
- Helyi mentés és kézbesítéstől számított 48 órás automatikus törlés. Ellenőrzés percenként és programindításkor; bezárt appnál a következő indításkor. Hátralévő idő kijelzése, minták ki-/bekapcsolása a saját bejegyzések megtartásával.

Windows telepítés: CAT-Letoltes-Windows-v1.1.0-beta.1.cmd, PowerShell nélkül. Futtatás előtt zárd be a programot. A stabil app Beállítások lapján kapcsold be a béta frissítéseket; a béta appból ugyanitt visszatérhetsz v1.0.0-ra. A v1.0.0 marad a legfrissebb hivatalos stabil.

Ellenőrzés: 172 automatizált ellenőrzés; hibamentes Windows x64 publish. A tényleges WPF-felület Linux alatt nem futtatható. A futárlogók forrásait és a teljes kódot a Source.zip tartalmazza.
