FedEx / GLS és egységes futárbeállítások – v1.1.0-beta.7. A v1.0.0 marad a hivatalos stabil.

- FedEx Tracking API és magyar MyGLS GetParcelStatuses: kézi soronkénti/összes frissítés, közös napló, státusz, utolsó hely és idő. FedEx éles projekt API Key / Secret Key; GLS API-jogosultságos MyGLS-felhasználó/jelszó. A GLS API nem ad ETA-t, és idegen feladók beérkező csomagjaihoz külön GLS-jog kellhet; hiányzó adatot nem találunk ki.
- Beállítások: Általános / DHL / UPS / FedEx / GLS / API-napló. Egy közös futárszerkesztő, vektorlogók, egységes mező/gombpozíciók és megnevezések, szemgombok, DPAPI-védett mentés és kézi teszt. Meglévő DHL/UPS hozzáférések kompatibilisek.
- Robo Sanyi munkagépes fejléce a kalkulátorokkal közös fotót, színátmenetet és tipográfiát használ. Változatlan ablakméret, két téma, lapozott táblázatok, görgetősáv nélkül. Nincs automatikus csomaglekérés vagy OAuth-hívás; 48 órás helyi tisztítás megmarad.
- Optimalizáció: közös beállításnézet, UPS/FedEx OAuth-motor és négyfutáros UTF-8 JSON-feldolgozás. A két új integráció mellett EXE 64,30 → 64,32 MB, ZIP 58,19 → 58,20 MB (kb. +0,03%). Meleg pufferes, izolált 20 × 1 MiB JSON-mérésben allokáció 62,95 → 0,010 MB (−99,98%); nem teljes app RAM-mérés. Módszer és korlátok: OPTIMIZATION.md.

Windows: CAT-Letoltes-Windows-v1.1.0-beta.7.cmd, PowerShell nélkül. Telepítés előtt zárd be az appot, vagy engedélyezd a béta frissítést és használd a Frissítések gombot. Hozzáférési útmutatók: DHL-CONNECTION.md, UPS-CONNECTION.md, FEDEX-CONNECTION.md, GLS-CONNECTION.md.

503 automatizált ellenőrzés, hibamentes Windows x64 self-contained publish, hivatalos sémákra épülő hálózat nélküli HTTP-fixture tesztek. Élő céges kulcsos próba és tényleges WPF/DPAPI-futtatás Linuxon nem történt.
