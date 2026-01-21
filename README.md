# ZISK - Žiarský Informačný Systém pre Kluby

Webová aplikácia pre správu dochádzky, ospravedlneniek a komunikácie v športovom klube.

## Technológie

- **Backend:** ASP.NET Core 9, Entity Framework Core
- **Frontend:** Blazor WebAssembly
- **UI:** MudBlazor
- **Databáza:** SQL Server
- **Autentifikácia:** ASP.NET Core Identity

## Architektúra

**Client-Server architektúru s REST API**:
- **Server (ZISK):** ASP.NET Core s Controllers (API endpoints)
- **Klient (ZISK.Client):** Blazor WebAssembly
- **Zdieľané (ZISK.Shared):** DTOs a Enumy

## Požiadavky

- .NET 9 SDK
- SQL Server (LocalDB alebo full)
- Visual Studio 2022/2026 alebo VS Code

## Inštalácia

### 1. Klonovanie repozitára

```bash
git clone https://github.com/Miclah/ZISK.git
cd ZISK
```

### 2. Konfigurácia databázy

Upravte connection string v súbore `ZISK/ZISK/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ZISK;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### 3. Migrácia databázy

```bash
cd ZISK/ZISK
dotnet ef database update
```

### 4. Spustenie aplikácie

```bash
dotnet run
```

Aplikácia bude dostupná na: `http://localhost:5224`

## Prihlasovacie údaje

Po prvom spustení sa automaticky vytvoria používatelia:

| Email | Heslo | Rola |
|-------|-------|------|
| admin@zisk.sk | admin123 | Admin |
| trener@zisk.sk | trener123 | Coach |
| rodic@zisk.sk | rodic123 | Parent |

## Štruktúra projektu

```
ZISK/
├── ZISK/                    # Server projekt (ASP.NET Core)
│   ├── Controllers/         # API kontrolery
│   ├── Components/          # Razor komponenty (server-side)
│   ├── Data/                # Entity a DbContext
│   └── wwwroot/             # Statické súbory (CSS)
├── ZISK.Client/             # Blazor WebAssembly klient
│   ├── Pages/               # Stránky aplikácie
│   ├── Layout/              # Layouty
│   └── Services/            # API služby (Refit)
└── ZISK.Shared/             # Zdieľané DTO a enumy
```

### Vzťahy medzi entitami
- **1:N:** Team → ChildProfile, Team → TrainingEvent, ChildProfile → AttendanceRecord
- **M:N:** ApplicationUser ↔ ChildProfile (cez ParentChild)

## Bezpečnosť

- **Heslá:** Uložené pomocou ASP.NET Core Identity (hashované s PBKDF2)
- **SQL Injection:** Ošetrené pomocou Entity Framework (parametrizované dotazy)
- **Autorizácia:** Role-based ([Authorize(Roles = "Admin,Coach")])
- **Validácia:** Na strane klienta (MudForm) aj servera (Controllers)

## AJAX komunikácia

Klient komunikuje so serverom asynchrónne pomocou Refit (HTTP client):
- Načítavanie obsahu tabuliek (GetExcuses, GetAnnouncements, ...)
- Odosielanie formulárov (CreateExcuse, CreateAnnouncement, ...)
- Filtrovanie záznamov (GetExcuses?status=Pending)
- Aktualizácia stavu (UpdateExcuseStatus)

## Funkcie

### Dochádzka
- Označovanie dochádzky trénerom (1-klik)
- Prehľad dochádzky pre rodičov
- Uzamknutie dochádzky
- Štatistiky účasti

### Ospravedlnenky
- Podávanie ospravedlneniek rodičmi
- Schvaľovanie trénermi/adminmi
- Prehľad stavu ospravedlneniek

### Oznamy
- Vytváranie oznamov trénermi
- Priorita oznamov (nízka, stredná, vysoká)
- Cielenie na tímy/skupiny
- Platnosť oznamov

### Dokumenty
- Upload a správa dokumentov
- Kategorizácia (všeobecné, zmluvy, tréningové plány)
- Sťahovanie dokumentov

### Administrácia
- Správa používateľov
- Správa tímov
- Štatistiky klubu

## Roly používateľov

- **Admin** - plný prístup ku všetkým funkciám
- **Coach (Tréner)** - správa dochádzky, oznamov, schvaľovanie ospravedlneniek
- **Parent (Rodič)** - prehľad dochádzky, podávanie ospravedlneniek

## Autor

Michal - Bakalárska práca 2025/26


