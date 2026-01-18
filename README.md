# ZISK - Žiarský Informačný Systém pre Kluby

Webová aplikácia pre správu dochádzky, ospravedlneniek a komunikácie v športovom klube.

## Technológie

- **Backend:** ASP.NET Core 9, Entity Framework Core
- **Frontend:** Blazor WebAssembly
- **UI:** MudBlazor
- **Databáza:** SQL Server
- **Autentifikácia:** ASP.NET Core Identity

## Požiadavky

- .NET 9 SDK
- SQL Server (LocalDB alebo full)
- Visual Studio 2022 (odporúčané) alebo VS Code

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

Po prvom spustení sa automaticky vytvoria seed dáta vrátane admin účtu:

| Email | Heslo | Rola |
|-------|-------|------|
| admin@zisk.sk | admin123 | Admin |

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


