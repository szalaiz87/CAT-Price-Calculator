# UPS kapcsolat – Linser Hungary v1.1.0-beta.5

## Mit kell beszerezni, és honnan?

| Adat / jogosultság | Hol találod? | Az alkalmazásban |
| --- | --- | --- |
| Céges UPS.com fiók | A meglévő céges UPS belépés; szükség esetén a cég UPS kapcsolattartója | Nem kell jelszót megadni az appban. |
| Céges UPS ügyfélszám és hozzáférés | UPS ügyfélfiók / számla / szerződés; legyen hozzáadva a portálon használt fiókhoz és az alkalmazáshoz | Opcionális, 6 betűből/számból álló UPS ügyfélszám; a hitelesítés x-merchant-id fejlécéhez. |
| Saját fejlesztői alkalmazás, Tracking API hozzáféréssel | [UPS Developer Portal](https://developer.ups.com/) → Apps / My Apps → Create App; saját céges használat és a megfelelő ügyfélszám társítása, Tracking kiválasztása | A Tracking hozzáférésnek engedélyezettnek kell lennie. |
| **Client ID** | A létrehozott UPS alkalmazás adatlapja, a hitelesítési adatoknál | Beállítások → **UPS API** → Client ID. |
| **Client Secret** | Ugyanennek az alkalmazásnak a hitelesítési adatai | Beállítások → **UPS API** → Client Secret. |
| Valódi UPS csomagszám | A feladótól kapott egyedi tracking number, gyakran 1Z kezdetű | Kézi teszt és Robo Sanyi; 7–34 betű/szám. |
| Engedélyezett API-korlátok | Az alkalmazás / Tracking termék portálon látható feltételei, szükség esetén UPS fejlesztői támogatás | A helyi 24 órás keret alapból 250 **HTTP-kérés**, a tokenkérésekkel együtt. Ez helyi védelem, **nem a UPS hivatalos napi kvótája**. |

Az ügyfélszám appbeli megadása önmagában nem ad API-jogosultságot: a portálon is társítani kell a megfelelő UPS-fiókot. Ha nem szükséges x-merchant-id, az opcionális mező üresen hagyható. A portál elnevezései nyelvtől/felületváltozattól függően eltérhetnek. Az UPS hivatalos példája az Apps alatt, a saját alkalmazás adatlapján található Client ID és Secret használatát írja elő.

Nem a DHL Consumer Key-t, nem a régi UPS Access Key-t, és nem kézzel bemásolt OAuth tokent kérünk. A UPS-hozzáféréshez **mindkét Client érték kell**. A Client Secretet ne küldd el beszélgetésben vagy képernyőképen.

## Beállítás és első ellenőrzés

1. Telepítsd a beta.5 verziót. Stabilból a Beállításokban engedélyezd a béta frissítéseket, majd Frissítések. A stabil v1.0.0 változatlan.
2. Nyisd meg **Beállítások → UPS API**. Másold be a saját alkalmazásod Client ID / Client Secret értékeit, opcionálisan az ügyfélszámot. A karakterszám jelzi a beillesztést, a szemgombbal olvashatóvá teheted az értéket. Mindkét téma támogatott.
3. Kattints **UPS beállítások mentése**. Windows DPAPI CurrentUser titkosítva menti az adatokat; mentéskor és lapelhagyáskor újra rejtettek. Más Windows-felhasználó vagy másik gép nem feltétlenül tudja feloldani a fájlt; ott újra meg kell adni őket. A Client Secret/token nem kerül a naplóba.
4. Adj meg egy valódi UPS csomagszámot és kattints **Tesztlekérés**. Ez éles UPS-lekérés; a teszt nem hoz létre vagy módosít csomagsort.
5. A **Robo Sanyi** lapon add meg a csomagszámot, válaszd a UPS futárt és írd be a megjegyzést. Hozzáadás csak helyi mentés. A sor frissítésikonja csak azt a csomagot, az **Összes frissítése** minden oldalon a támogatott, hozzáféréssel rendelkező úton lévő saját DHL/UPS-csomagokat kéri le.
6. Hibánál **Beállítások → API-napló**: UPS jelölés, idő, OAuth / követés / feldolgozás szakasz, HTTP-kód és biztonságos hiba. A napló újraolvasása nem indít hálózati lekérést.

**Nincs automatikus csomaglekérés**, induláskor, hozzáadáskor, lapváltáskor és időzítve sem. A tokent csak egy ilyen kézzel indított lekérés során kérjük/újítjuk meg; lejáratig csak memóriában őrizzük. A kérések között legalább 5 másodperc telik el. A Stop a sorozatot megszakítja; a már mentett adatok megmaradnak. A minták, kézbesített csomagok és API nélküli FedEx/GLS kimaradnak. Egy futár hitelesítési/korlátozási hibája után a másik futár frissítése folytatódhat.

## Mit látunk, ha van API-adat?

Státusz, utolsó ismert szkennelési hely és annak dátuma/ideje, valamint a tervezett kiszállítás napja. A UPS szolgáltatói leírása a sor eszköztippjében olvasható; a kérés alapértelmezett nyelve en_US. Nem kérünk kézbesítési aláírást vagy POD-képet. Csak a hozzáférésedhez elérhető követési adatokra támaszkodunk; részletes ügyféladatokhoz az UPS külön jogosultságot kérhet.

A legfrissebb helyet a hozzá tartozó esemény idejével párosítjuk; a címzett címe nem lesz „utolsó ismert hely”. Hiányzó adat helyén **Még nincs adat** szerepel. Ismert időzónával magyar időt mutatunk; időzóna nélkül az eredeti helyi idő * jelölést kap. A kiszállítási ablakból nem találunk ki pontos időpontot.

A kézbesített csomag 48 eltelt óra után helyben törlődik. Ha az API pontos, időzónás kézbesítési ideje nem ismert, az első igazolt észlelés időpontjától számolunk, † jelöléssel. Újabb frissítés nem tolja ki a megőrzést. Ez a helyi tisztítás induláskor és percenként fut, API-hívás nélkül.

## Gyakoribb hibák

- **OAuth 400/401/403:** rossz Client ID / Secret, nem megfelelő vagy letiltott alkalmazás; ellenőrizd a portál adatlapját. Titokcsere után az appban is újra kell menteni.
- **Követés 401/403:** a Tracking API nincs engedélyezve, vagy a hozzáférésed nem jogosult. Érvénytelen memóriatokenre ugyanabban a kézi műveletben legfeljebb egy újrahitelesítés történik, végtelen próbálkozás nélkül.
- **400/404 / nincs csomag:** ellenőrizd az egyedi tracking numbert. Frissen feladott csomagnál később próbáld újra. Több csomagra mutató azonosítónál egyedi csomagszám szükséges.
- **429 vagy helyi keret:** várj a jelzett korlát lejártáig; a keretet csak az engedélyezett hozzáférés ismeretében módosítsd. Újraindítás nem nullázza a helyi 24 órás keretet.
- **Hálózati/időkorlát/feldolgozási hiba:** a korábbi csomagadat megmarad. A napló megmutatja, melyik szakaszban történt a hiba.

## Hivatalos technikai források

- [UPS Developer Portal](https://developer.ups.com/)
- [UPS hivatalos OAuth Client Credentials OpenAPI](https://github.com/UPS-API/api-documentation/blob/main/OAuthClientCredentials.yaml): POST `https://onlinetools.ups.com/security/v1/oauth/token`, HTTP Basic Client ID / Secret, `grant_type=client_credentials`; opcionális x-merchant-id.
- [UPS hivatalos Tracking OpenAPI](https://github.com/UPS-API/api-documentation/blob/main/Tracking.yaml): GET `https://onlinetools.ups.com/api/track/v1/details/{inquiryNumber}`, OAuth Bearer, transId és transactionSrc fejlécek; activity, currentStatus, deliveryDate és deliveryTime.
- [UPS hivatalos követési példa és hitelesítési adatok beszerzése](https://github.com/UPS-API/java-api-examples/tree/main/track)

A béta éles végpontot használ, nem CIE/sandbox mintaválaszt. Saját céges kulcsok hiányában a hitelesítési/követési eseteket szimulált HTTP-válaszokkal ellenőriztük; valódi UPS-csomaggal a fenti kézi teszt még szükséges. Linuxon a WPF-felület és a Windows DPAPI tényleges futása nem tesztelhető.
