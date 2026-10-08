# Projektmunkára vonatkozó tartós utasítások

- A felhasználó 2026-10-08-i kérése: a v1.0.0 az első hivatalos stabil főverzió. Ez a stabil kiadás a jelen beszélgetésben kifejezetten engedélyezett.
- Innentől minden új fejlesztés és kiadás **béta**, amíg a felhasználó kifejezetten nem kéri stabil főverzió kiadását. Új stabil kiadást automatikusan vagy pusztán a tesztek sikeréből következtetve kiadni tilos.
- Fejlesztés alapértelmezett forrásága `beta`; hivatalos forráság `stable`. A történeti `main` ág változatlanul megőrizhető.
- Verzió egyetlen forrása a Directory.Build.props. Béta: pl. `1.1.0-beta.1`, majd `.2`, `.3`; stabil az azonos alapverzió béta-utótag nélkül. Az első béta kiadás `1.1.0-beta.1` (Robo Sanyi előnézet); következő béta `.2`, `.3` stb.
- Béta GitHub release: `prerelease=true`, `make_latest=false`, forráság `beta`. Stabil: `prerelease=false`, `make_latest=true`, forráság `stable`, és kifejezett felhasználói stabil-kiadási kérés szükséges. A scripts/release.py stabil kiadáshoz `--stable-approved` kapcsolót kér; ez csak már meglévő explicit felhasználói engedéllyel használható.
- A frissítési csatorna alapból stabil; béta csak mentett felhasználói opt-in után. A visszatérés a legfrissebb stabilra lehet downgrade, és kikapcsolja a béta csatornát. Megőriz minden más beállítást és árfolyamot.
- Tartsd meg a DESIGN.md tartós követelményeit: nincs görgetősáv, fix keret és fülméretek, közös kalkuláció, 3×5 szorzók, éles menüjelölés, lekerekítések, vektoros logó/ikonok, világos/sötét téma, normál Windows-tálca.
- Robo Sanyi: jelenleg API nélküli előnézet, saját csomaghoz ne találj ki követési adatot. Internetes logók forrásai Assets/Carriers/SOURCES.md. Két négy soros, lapozható táblázat; kézbesítéstől 48 órás helyi megőrzés, induláskori és percenkénti tisztítás.
- Funkcionális frissítési változásoknál futtasd a tests/CatPriceCalculator.Checks projektet, és készíts Windows x64 publish-t. Linux alatt a tényleges WPF-felület nem futtatható; ezt ne állítsd ellenőrzöttnek.
