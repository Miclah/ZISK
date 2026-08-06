# Ako spustiť ZISK v Dockeri

Compose zdvihne dva kontajnery: SQL Server 2022 a samotnú aplikáciu. Image
aplikácie buildí server projekt, ktorý si potiahne WebAssembly klienta aj shared
projekt, takže jeden build vyrobí všetko.

## Čo je treba

Docker Desktop, prípadne Docker Engine s Compose pluginom.

## Nastavenie

```bash
cp .env.example .env
```

Otvor `.env` a nastav `MSSQL_SA_PASSWORD`. SQL Server si vynucuje vlastnú
password policy: aspoň 8 znakov a tri zo štyroch skupín (veľké písmená, malé
písmená, číslice, symboly). Heslo, ktoré policy nesplní, zhodí databázový
kontajner hneď pri štarte.

`.env` je v gitignore.

## Spustenie

Prvýkrát, alebo po zmene Dockerfilu:

```bash
docker compose up --build
```

Potom už stačí:

```bash
docker compose up        # na popredí, logy vidno v termináli
docker compose up -d     # na pozadí
```

Aplikácia beží na **http://localhost:8080**. Poradie štartu rieši healthcheck:
kontajner s aplikáciou čaká, kým mu SQL Server odpovie na query, potom pustí EF
Core migration a naseeduje databázu. Prvý beh trvá zhruba minútu.

## Seed mode

Compose má predvolene `ZISK_SEED_MODE=local`, čo znamená bežnú login stránku a
development účty popísané v hlavnom [README](README.sk.md#vývojárske-účty).

Keď v `.env` nastavíš `ZISK_SEED_MODE=demo`, prepneš sa na verejný demo build:
one-click prepínač rolí na `/demo`, oddelené dáta pre každého návštevníka,
žiadne odchádzajúce e-maily a 404 na `/login`. V tomto režime sa pre naseedované
účty použijú hodnoty `Seed__Passwords__*` z `docker-compose.yml`. V `local`
režime sa ignorujú.

## Zastavenie

```bash
docker compose down       # zastaví kontajnery, databáza ostane
docker compose down -v    # zastaví a zmaže aj database volume
```

`down -v` použi vtedy, keď chceš, aby sa pri ďalšom štarte seedovalo od nuly.

## Rebuild

Zmena v `.csproj` alebo v Dockerfile znamená, že cachovaná restore vrstva je
neaktuálna:

```bash
docker compose build --no-cache
docker compose up
```

Na zmeny v zdrojákoch stačí obyčajný `docker compose up --build`.

## Keď niečo nejde

**Pri builde zlyhá NuGet signature validation (NU3012).** Author signing
certificate balíka Refit bol po vydaní balíka revoknutý. Samotný balík je v
poriadku, jeho hash stále sedí, ale clean restore ho odmietne. V repozitári je
`NuGet.Config`, ktorý signature validation vypína, a Dockerfile ho explicitne
posiela do `dotnet restore`. Ak na túto chybu narazíš, býva za tým build
context, v ktorom `NuGet.Config` chýbal, tak skontroluj, či riadok
`COPY NuGet.Config ./` v Dockerfile naozaj prebehol. CI workflow rieši to isté
inak, cez `NUGET_CERT_REVOCATION_MODE=offline`.

**Kontajner s aplikáciou naštartuje a hneď spadne.** Skoro vždy je to databáza:
pozri sa do `docker compose logs db`, či tam nie je odmietnuté
`MSSQL_SA_PASSWORD`.

**Uploadnuté súbory po rebuilde zmiznú.** Prílohy sa zapisujú do
`wwwroot/uploads` vnútri kontajnera a nie je tam namountovaný žiadny volume,
takže znovuvytvorenie kontajnera ich zmaže. Databáza to prežije, tá volume má.
