# ZISK – Žiarský Informačný Systém pre Kluby

> Toto je slovenský používateľský manuál (inštalácia, prihlasovacie údaje, navigácia podľa role). Anglické README zamerané na technický prehľad projektu je v [../README.md](../README.md).

Webová aplikácia pre evidenciu dochádzky, tréningov, tímov a komunikáciu medzi trénermi, rodičmi a deťmi v športovom klube. Postavená na **Blazor WebAssembly + Server (.NET 10)**, UI pomocou **MudBlazor**, databáza **SQL Server (LocalDB)**.

---

## Požiadavky

| Nástroj | Verzia |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** alebo novší |
| [SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) | súčasť Visual Studio alebo samostatne |
| [Visual Studio 2022](https://visualstudio.microsoft.com/) alebo [VS Code](https://code.visualstudio.com/) | odporúčané |

Overenie inštalácie:
```bash
dotnet --version   # musí byť 10.0.x
sqllocaldb info    # musí vypísať dostupné inštancie
```

---

## Inštalácia a spustenie

### 1. Klonuj repozitár

```bash
git clone <url-repozitara>
cd ZISK
```

### 2. Obnov závislosti

```bash
dotnet restore ZISK/ZISK.sln
```

### 3. Spusti aplikáciu

```bash
dotnet run --project ZISK/ZISK/ZISK.csproj
```

Aplikácia sa automaticky spustí na: **http://localhost:5224**

> **Poznámka:** Pri prvom spustení prebehne automaticky:
> - migrácia databázy (vytvorenie schémy)
> - seed dát – roly, testovacie účty, tímy, vzorové tréningy, dochádzka a oznamy

### Spustenie vo Visual Studio

1. Otvor `ZISK/ZISK.sln`
2. Nastav startup project na `ZISK` (serverový projekt)
3. Stlač `F5` (Debug) alebo `Ctrl+F5` (bez debuggera)

---

## Konfigurácia

Hlavný konfiguračný súbor: `ZISK/ZISK/appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ZISK;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

Pre lokálny vývoj nie je potrebná žiadna zmena konfigurácie – LocalDB sa spustí automaticky.

---

## Prihlasovacie údaje

### Hlavné testovacie účty

| Rola | E-mail | Heslo | Popis |
|---|---|---|---|
| **Admin** | `admin@zisk.sk` | `Admin1234` | Plný prístup – správa používateľov, tímov, sezón, štatistiky |
| **Tréner** | `trener@zisk.sk` | `Trener1234` | Správa tréningov, dochádzka, tím, ospravedlnenky |
| **Rodič** | `rodic@zisk.sk` | `Rodic1234` | Prehľad dieťaťa, posielanie ospravedlneniek |
| **Dieťa** | `dieta@zisk.sk` | `Dieta1234` | Vlastný dashboard – rozvrh, dochádzka |

### Vzorové účty (sample data)

| Rola | E-mail | Heslo |
|---|---|---|
| Tréner | `coach.marek.sample@zisk.sk` | `Sample1234` |
| Tréner | `coach.lukas.sample@zisk.sk` | `Sample1234` |
| Rodič | `parent.jana.sample@zisk.sk` | `Sample1234` |
| Rodič | `parent.milan.sample@zisk.sk` | `Sample1234` |
| Atlét | `athlete.adam.sample@zisk.sk` | `Sample1234` |
| Dieťa | `child.nina.sample@zisk.sk` | `Sample1234` |

---

## Navigácia a ovládanie

### Rola: Admin

Po prihlásení sa zobrazí **Admin Dashboard** s nasledujúcimi sekciami:

| Sekcia | Popis |
|---|---|
| **Dashboard** | Prehľad aktivity, štatistiky tréningov |
| **Používatelia** | Zoznam všetkých účtov, zmena roly, deaktivácia |
| **Tímy** | Vytváranie a úprava tímov, správa členov |
| **Tréningy** | Prehľad všetkých tréningov naprieč tímami |
| **Sezóny** | Správa sezón (aktivácia, dátumy) |
| **Oznamy** | Vytváranie a správa oznamov pre rodičov/atlétov |
| **Ospravedlnenky** | Prehľad všetkých žiadostí |
| **Štatistiky** | Grafy dochádzky, aktivita klubu |
| **Dokumenty** | GDPR, poriadky, nahrávanie súborov |

### Rola: Tréner (Coach)

| Sekcia | Popis |
|---|---|
| **Dashboard** | Nadchádzajúce tréningy, dochádzka môjho tímu |
| **Tréningy** | Zoznam tréningov, vytváranie jednorazových aj sérií |
| **Môj tím** | Členovia tímu, detail hráča |
| **Dochádzka** | Označenie prítomnosti/neprítomnosti na tréningu |
| **Ospravedlnenky** | Schvaľovanie/zamietnutie žiadostí rodičov |
| **Oznamy** | Čítanie oznamov |

### Rola: Rodič (Parent)

| Sekcia | Popis |
|---|---|
| **Dashboard** | Prehľad detí, nadchádzajúce tréningy |
| **Dochádzka** | Dochádzka môjho dieťaťa |
| **Ospravedlnenky** | Odoslanie ospravedlnenky na konkrétny tréning |
| **Oznamy** | Oznamy od trénera/admina |
| **Profil** | Správa účtu, zmena hesla |

### Rola: Dieťa / Atlét (Child / Athlete)

| Sekcia | Popis |
|---|---|
| **Dashboard** | Vlastný rozvrh tréningov, dochádzka |
| **Tréningový plán** | Kalendár tréningov |
| **Dochádzka** | História vlastnej dochádzky |
| **Oznamy** | Oznamy tímu |

---

## Registrácia nového používateľa

1. Choď na `/register`
2. Vyplň meno, priezvisko, e-mail, heslo a rodné číslo
3. registruj sa

---

## PWA – Inštalácia ako aplikácia

ZISK podporuje inštaláciu ako **Progressive Web App (PWA)** na desktop aj mobil.

**Nakonfigurované súčasti:**
- `manifest.webmanifest` – názov (`ZISK`), farby, ikony (192×192, 512×512, maskable)
- `service-worker.js` – registrovaný, umožňuje inštaláciu prehliadačom
- `theme-color` a `application-name` v HTML hlavičke

**Inštalácia na desktop (Chrome / Edge):**
1. Otvor aplikáciu na `http://localhost:5224`
2. V adresnom riadku klikni na ikonu **Inštalovať** (monitor s šípkou dole) alebo cez menu → *Inštalovať ZISK*
3. Potvrď inštaláciu – aplikácia sa otvorí ako samostatné okno

**Inštalácia na Android:**
1. Otvor aplikáciu v Chrome
2. Klepni na menu (⋮) → **Pridať na plochu**

> **Offline režim:** Súčasná implementácia service workera neobsahuje cache stratégiu – aplikácia vyžaduje aktívne sieťové pripojenie na funkčnosť. Inštalácia a spustenie ako PWA sú plne funkčné.

---

## Štruktúra projektu

```
ZISK/
├── ZISK/               # Serverový projekt (ASP.NET Core + Blazor Server)
│   ├── Controllers/    # REST API pre WASM klienta (Refit)
│   ├── Data/           # EF Core entity a DbContext
│   ├── Services/       # Biznis logika, background workery
│   └── Components/     # Server-side Razor stránky (login, register, verify)
├── ZISK.Client/        # WebAssembly klient (Blazor WASM)
│   ├── Pages/          # Stránky podľa roly (Admin, Coach, Parent, Child)
│   ├── Components/     # Dialógy a zdieľané komponenty (MudDialog)
│   └── Services/       # Refit API klienti (rozhrania a kontext používateľa)
├── ZISK.Shared/        # Zdieľané DTOs, enumerácie
└── ZISK.Tests/         # Unit testy (xUnit)
```

---

## Technológie

| | |
|---|---|
| Framework | .NET 10 / Blazor Hybrid (Server + WebAssembly) |
| UI knižnica | MudBlazor 9 |
| Databáza | SQL Server (LocalDB pre vývoj) |
| ORM | Entity Framework Core 10 |
| HTTP klient | Refit |
| Autentifikácia | ASP.NET Core Identity (cookie, 14-dňový token) |
| Lokalizácia | sk-SK (slovenčina) |

---

## Časté problémy

**LocalDB sa nespúšťa**
```bash
sqllocaldb start MSSQLLocalDB
```

**Port je obsadený**  
Zmeň `applicationUrl` v `ZISK/ZISK/Properties/launchSettings.json`.

**Migrácia zlyhá**
```bash
dotnet ef database update --project ZISK/ZISK/ZISK.csproj
```