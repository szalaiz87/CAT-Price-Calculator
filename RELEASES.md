# Stabil és béta kiadások

- **v1.0.0**: a jelenlegi funkcionalitás első hivatalos stabil főverziója, a mentett frissítési csatornával és stabilra visszatéréssel.
- `stable`: a legutóbb jóváhagyott stabil kiadás teljes forrása.
- `beta`: az új fejlesztések forrása; induló verzió `1.1.0-beta.1`, még nem publikált fejlesztési alap.
- A történeti `main` ág és 0.x kiadások megmaradnak.

Új fejlesztés alapból béta. Stabil kiadás csak kifejezett felhasználói kérésre. Béta `v1.1.0-beta.1`, `beta.2`, stb.; stabilra emeléskor `v1.1.0`. Mindkét projekt verzióját a Directory.Build.props határozza meg; a felület és a frissítő a tényleges assembly-metaadatból kapja a verziót.

Alapbeállításon a program GitHub `/releases/latest` végpontról csak stabil kiadást fogad el. Béta engedélyezve a release-listát végiglapozza, kihagyja a draftot, ismeretlen preview formátumokat és hibásan megjelölt kiadásokat, és a legújabb stabil/béta verziót választja. Az azonos számozású stabil kiadás újabb a bétánál. Béta kikapcsolása nem telepít automatikusan alacsonyabb stabil verziót.

Béta programban a Beállításokban látható a **Visszatérés a legfrissebb stabil verzióra** gomb. Az újraszámolástól függetlenül ellenőrzi a stabil kiadást, és kifejezett visszatéréskor a stabil verziót telepíti akkor is, ha annak száma kisebb. A letöltést és ellenőrzőösszeget ugyanaz az optimalizált frissítő kezeli; béta kikapcsolása csak sikeres letöltés után, az újraindítás előtt mentődik. Árfolyamok, témák és indítási beállítások megmaradnak.

Kiadás menete:

1. Dolgozz a megfelelő forráságon, állítsd a Directory.Build.props verzióját, frissítsd a legfeljebb négy pontból álló UI-verziólogot.
2. Futtasd az ellenőrzéseket és a `build-windows.cmd` fájlt. A Windows EXE az `artifacts/win-x64` mappába kerül.
3. `python scripts/release.py package --exe artifacts/win-x64/CAT-Price-Calculator.exe --output /egy/ures/kiadasmappa`
4. A forráságat pushold a megfelelő `stable`/`beta` ágra. A publisher ellenőrzi a távoli verziót.
5. Béta: `python scripts/release.py publish --output /kiadasmappa --notes-file RELEASE-NOTES.md --target beta`. Automatikusan prerelease, nem váltja le a GitHub latest stabil kiadását.
6. Stabil: ugyanez `--target stable --stable-approved` kapcsolóval, kizárólag a felhasználó már meglévő explicit stabil-kiadási engedélyével.

A publisher a `GH_RELEASE_TOKEN` környezeti hitelesítést használja, nem kerül token a forrásba vagy a programba. Először draftot hoz létre, minden eszközt ellenőriz, majd publikál. A forrás-ZIP a teljes appot és build/release eszközöket tartalmazza. Windows telepítéshez továbbra sem kell PowerShell.
