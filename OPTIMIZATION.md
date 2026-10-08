# v0.11.2 optimalizálás – változatlan funkciók és arculat

Mérések a v0.11.1 forrásával összevetve, Release .NET 10.0 Linux alatt. Nyers adatok: OPTIMIZATION-MEASUREMENTS.json. A közvetlenül érintett műveleteket mértük; ez nem a teljes Windows-folyamat RAM-, CPU- vagy indulásiidő-mérése.

| Művelet / mért mennyiség | Előtte | Utána | Eredmény |
| --- | ---: | ---: | --- |
| Számfeldolgozás, 400 000 vegyes bevitel, medián | 40.984 ms | 23.542 ms | 42.56% rövidebb idő |
| Számfeldolgozás, allokált bájt / 400 000 hívás | 16,000,096 | 96 | Nincs hívásonkénti szövegallokáció a mért mintán; 96 bájt fix mérési rezsi |
| Frissítő, 8 MiB-os ZIP, allokált .NET-bájt / futás | 16,819,432 | 42,256 | 99.75% csökkenés |
| Három átfedő árfolyamlekérés, sikeres EKB HTTP-kérések | 3 | 1 | 66,67%-kal kevesebb EKB-kérés ebben az esetben |
| Első téma használatakor épített paletta-ecsetek | 84 | 2 | Csak a szükséges paletta készül el; szemantikus UI-ecsetek változatlanok |
| EXE bájt | 67,593,315 | 67,593,853 | +538 bájt, +0,0008%; gyakorlatilag változatlan |

A számteszt 8, felváltva használt érvényes/hibás, magyar/angol és Unicode szóközös bevitelt mér. 20 000 hívás bemelegítés mindkét változaton, utána 7 forduló, váltott mérési sorrenddel. A frissítőteszt 8 MiB véletlen adattartalmú EXE-t tartalmazó, tömörítés nélküli ZIP-et használ, két 4 MiB körüli és egy kis záró részben. Ugyanazok az adatok, StreamContent HTTP-fixture, egy bemelegítő és négy mért forduló. A fixture létrehozása kívül esik a mért allokáción. A párhuzamos EKB-kérések száma késleltetett, számlált tesztforráson ellenőrizve. Hálózati sebességre nem következtetünk a helyi frissítőteszt idejéből.

Megvalósítás:
- A számmezők normalizálása rövid bevitelnél stackalloc/Span segítségével, hosszúnál visszaadott ArrayPool-pufferrel. Ugyanazok a szeparátorok és ellenőrzések. A számítási képletek, kerekítés és szorzók változatlanok.
- A frissítő HTTP-fejlécek után közvetlenül a fájlba másol, legfeljebb 64 KiB-os újrafelhasználható átviteli pufferrel. Részenkénti SHA-256 folyamatosan, teljes ZIP SHA-256 továbbra is külön, a lemezről ellenőrizve. Hostellenőrzés, 8 MiB-os eszközméret-korlát, sorrend, pontos letöltési hossz, megszakítás, timeout és csak sikeres ellenőrzés utáni kicsomagolás megmaradnak.
- Az átfedő, azonos megszakítási körbe tartozó EKB-lekérések egy HTTP-letöltést használnak. Nincs tartós adatcache; befejezés után új kérés új letöltést indít. Külön megszakítási körök és tartalék devizapár-források önállóak maradnak.
- Online árfolyam-alkalmazáskor a közbenső TextChanged nem számol újra; az árfolyam és eredet beállítása után a meglévő explicit újraszámítás megmarad. Manuális bevitel és a revision-védelem működése változatlan.
- A kijelölések csak állapotváltozáskor írják a WPF-propertyt, új bool-objektumok nélkül. A frissítési szolgáltatás újrafelhasználható.
- 35 használatlan szín/ecset-erőforrás, egy használatlan ikon és a régi, nem beágyazott raster logó eltávolítva. A használt fotó, vektorlogó és natív ikon bájtról bájtra változatlan. Vektorok/fotóforrás fagyasztva; témaváltó ecsetek nem, mert dinamikusak.
- A paletta 42 helyett 32 használt színt tartalmaz. Csak az elsőként választott paletta készül el induláskor, a másik első használatkor. Témák színei és mentése változatlanok.

Ellenőrzés: 122 automatikus ellenőrzés, köztük 10 012 bemenet összevetése a korábbi parserrel, csonka/túlméretes/hibás letöltés, streaming, választest-timeout, frissítés integritása, párhuzamos EKB és független megszakítások. Három élő árfolyamlekérés sikeres. Hibamentes Windows x64 self-contained publish. Mind a 43 névvel hivatkozott UI-elem, méret, kötés, X gomb, 3×5-ös rács és a használt két téma színpárjai megőrizve. A Windows felület tényleges futtatása itt nem lehetséges.

---

# v0.8.1 optimalizálás

Mért EXE méret: 174,125,807 → 76,194,127 bájt (56.24% csökkenés).

- Minden gombot érintő elmosó árnyékszűrők megszüntetve; éles keretkiemelés.
- Szabályos nyolcfogú fogaskerék központi körrel, vektoros geometria.
- Fülváltásonként 15 új gomb létrehozása helyett a meglévő 15 gomb marad.
- Szorzóválasztáskor több mint 30 új ecset és egy árnyékeffekt helyett újrafelhasználható ecsetek, árnyékszűrő nélkül.
- Két előre elkészített, fagyasztott témapaletta: ismételt színfeldolgozás és ecsetgyártás nélkül. Ez kis induláskori többlet az ismételt váltások olcsóbb kezeléséért.
- Új ExchangeRateService példány lekérésenként helyett egy közös példány; az időzóna objektuma is újrafelhasználható.
- Single-file tömörítés csökkenti a lemezen lévő EXE méretét. Induláskor kicsomagolási munkát igényel, ezért nem állítjuk, hogy gyorsabb indulást eredményez.

A funkciók változatlanok. 103 automatikus ellenőrzés és 3 élő árfolyamlekérés. Windows CPU/GPU/RAM és indulási idő itt nem mérhető, ezek javulására nem adunk százalékos állítást. A fenti objektumszámok a kód vizsgálatából származnak, nem futásidejű profilozásból.

Letöltési ZIP: 69,922,900 → 70,333,417 bájt (+0.59%). A kisebb EXE nem eredményez kisebb letöltést, mert a korábbi ZIP már erősen tömörítette az alkalmazást.
