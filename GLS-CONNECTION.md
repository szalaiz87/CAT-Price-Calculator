# GLS – magyar MyGLS kézi csomagkövetés

A v1.1.0-beta.7 a **magyar MyGLS API / GetParcelStatuses** éles szolgáltatást integrálja. Ez a `mygls.hu` céges rendszerhez tartozik; a német GLS és más GLS-termékek ettől eltérő API-t használhatnak. A program jelenleg a magyar végpontot használja.

## Szükséges adatok és beszerzésük

1. Legyen céges GLS-szerződésed és magyar [MyGLS](https://mygls.hu/) fiókod.
2. A GLS kapcsolattartótól kérj **MyGLS API-jogosultságot** a használni kívánt felhasználóhoz. A webes belépés és ügyfélszám önmagában nem bizonyítja az API-jogosultságot.
3. Kérj **API-felhasználónevet (rendszerint e-mail)** és hozzá tartozó **jelszót**, vagy a meglévő, GLS által engedélyezett MyGLS-felhasználót használd. Nem OAuth Client ID/Secret és nem DHL-féle API-kulcs kell.
4. A **Beállítások → GLS** fülön add meg az **API-felhasználó** és **API-jelszó** értékét. Az engedélyezett, pozitív egész **GLS ügyfélszám** opcionális; a felhasználóhoz társított szám legyen. Nyomd meg a **Hozzáférés mentése** gombot.
5. Egy saját csomagszámmal indítsd a **Kapcsolat tesztelése** műveletet. A hibák részletei a **Beállítások → API-napló** alatt láthatók.

**Beérkező csomagok:** külön kérdezd meg a GLS-től, hogy az API-felhasználód követheti-e azokat a csomagokat is, amelyeket egy másik GLS-ügyfél adott fel nektek. Az API csomagszintű hozzáférést ellenőriz: 5/15-ös hibával megtagadhatja egy ilyen csomag követését. Ez nem javítható pusztán a programban; GLS-jogosultság kell. A megtagadott sor nem állítja le a többi GLS-csomag kézi frissítését.

## Működés és korlátok

- Éles végpont: `POST https://api.mygls.hu/ParcelService.svc/json/GetParcelStatuses`. Nem az `api.test.mygls.hu` környezet.
- Hivatalos JSON-mezők: `Username`, `Password`, `ClientNumberList`, `WebshopEngine`, `ParcelNumber`, `ReturnPOD=false`, `LanguageIsoCode=HU`. A program a jelszó UTF-8 bájtjaiból SHA-512-t képez, a 64 bájtot **JSON számok tömbjeként** küldi, nem base64-ként. A jelszó Unicode karakterei és kezdeti/végi szóközei megmaradnak. Az eredeti jelszó nem kerül a kérésbe; a hash is hitelesítési titok, nem naplózható.
- Az API `ParcelStatusList` állapotait és depóhelyét használjuk. `/Date(unix-ms+offset)/` WCF-időnél a milliszekundum már UTC-pillanat, ezért az offsetet nem adjuk hozzá újra. Ismert időzóna magyar időre alakul, ismeretlen ISO-idő eredetiben, * jelöléssel marad.
- **A GetParcelStatuses nem ad strukturált várható kézbesítési dátumot (ETA).** Ezért a tervezett érkezés mezőben „Még nincs adat” marad. Nem következtetünk másnapi dátumra vagy a státusz szabad szövegéből.
- Az 5/54/55/58 kódok megerősített kézbesítésnek számítanak; a 23/40 (feladónak visszaküldés) nem teszi át a csomagot a megérkezett táblába. Az ismeretlen jövőbeli kódot nem találgatjuk.
- Nincs automatikus csomaglekérés vagy háttérhitelesítés. Csak a két csomagfrissítő művelet és a beállítások kézi tesztje kér API-adatot.
- A **Helyi 24 órás keret** alapból 250 HTTP-kérés, **nem hivatalos GLS-kvóta**. Futáronként mentett keret és legalább 5 másodperces ütemezés. HTTP 429 esetén Retry-After, MyGLS 31-es „túl gyakori kérés” hibánál legalább öt perc várakozás. A GLS üzleti korlátait külön ellenőrizd.
- Windows DPAPI CurrentUser védett mentés. Felhasználó/jelszó/hash/ügyfélszám, fejlécek, URL, teljes csomagszám és nyers válasz nincs az API-naplóban. Titkosítási hiba esetén nincs egyszerű szöveges tartalék.
- A megérkezett csomagok 48 eltelt óráig maradnak a biztos kézbesítési időtől, ennek hiányában az első megerősített észleléstől (†). Újabb frissítés nem hosszabbítja meg és nem állítja vissza az állapotot.

## Hivatalos források

Ellenőrizve: 2026-10-08. A publikus PDF 25.12.11-es dokumentáció, az API oldala 26.09.30.01 futó verziót jelez.

- [Magyar MyGLS API dokumentáció és példák](https://api.mygls.hu/)
- [MyGLS API PDF: request base, GetParcelStatuses, Appendix A/G](https://api.mygls.hu/docs/MyGLS_API.pdf)
- [Éles szolgáltatás WSDL/séma](https://api.mygls.hu/ParcelService.svc?singleWsdl)

Élő MyGLS-felhasználóval próba nem történt. A kérés alakja, SHA-512 bájttömbje, státuszok, WCF-idők és hibák hálózat nélküli HTTP-fixture-ökkel ellenőrzöttek. A beérkező csomagok tényleges jogosultságát a GLS-nek kell megerősítenie.
