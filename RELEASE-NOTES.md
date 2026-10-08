Optimalizáció – v1.1.0-beta.6. A v1.0.0 marad a hivatalos stabil.

- Kisebb Windows-csomag: EXE 67,73 → 64,30 MB (−5,1%), ZIP 61,60 → 58,19 MB (−5,5%). A nem használt runtime nyelvi erőforrások kimaradnak; magyar kultúra, angol fallback, teljes .NET/WPF és grafika megmarad.
- Hat tároló közös, atomi UTF-8 JSON-fájlkezelője, köztes teljes szöveg nélkül; 200-as naplócache azonnali mentéssel és külső változás felismerésével. 1000 naplóírás összes memóriaallokációja 425,71 → 3,41 MB (−99,2%) az izolált Linux-mérésben; nem a teljes Windows-app RAM-használata.
- DHL/UPS közös titkos adatmező és követési HttpClient, kérésenként elkülönített hitelesítési fejlécekkel. Közös négy soros csomaglapozás; rejtett csomagoldalon nincs percenkénti táblázat-újraépítés, látható oldalon csak a megérkezett tábla időfüggő része frissül.
- Minden funkció, két téma, ablakméret, görgetősáv nélküli megjelenés, kézi követés, DPAPI, mentett beállítás és 48 órás helyi tisztítás megmarad. Nincs automatikus csomaglekérés; FedEx/GLS továbbra is API nélküli.

Windows: CAT-Letoltes-Windows-v1.1.0-beta.6.cmd, PowerShell nélkül. Telepítés előtt zárd be a programot. Béta csatorna engedélyezése, majd Frissítések. Részletes eredmény és mérési módszer: OPTIMIZATION.md; a forrás és futárhozzáférési útmutatók a kiadásban.

398 automatizált ellenőrzés és hibamentes Windows x64 publish. Naplócache, külső módosítás, hibás fájl, atomi mentési hiba, párhuzamos naplózás, kompatibilitás, lapozás és közös HTTP-fejlécek ellenőrizve. A tényleges WPF/DPAPI-futtatás és teljes Windows-folyamat RAM-ja Linuxon nem mérhető; élő céges DHL/UPS-kulcsos próba nem történt.
