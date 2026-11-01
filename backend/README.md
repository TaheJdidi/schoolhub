# SchoolHub — backend

L'application .NET 10 de SchoolHub. Elle servira aussi les pages Angular, à la même adresse ([ADR 0003](../docs/adr/0003-un-seul-deploiement-decoupe-en-modules.md)).

## Ce qu'il faut sur le poste

- le SDK .NET 10 ;
- Docker Desktop, qui a besoin de WSL 2 sous Windows : il fait tourner PostgreSQL pour l'application et pour les tests.

## Lancer l'application

```bash
docker compose up -d                 # démarre PostgreSQL
dotnet tool restore                  # installe dotnet-ef
dotnet ef database update --project src/SchoolHub
dotnet run --project src/SchoolHub
```

Puis ouvrir http://localhost:5087/api/sante : la page répond « SchoolHub fonctionne. ».

## Lancer les tests

```bash
dotnet test
```

Les tests qui touchent à la base lancent eux-mêmes un PostgreSQL jetable dans Docker (Testcontainers) : Docker doit être démarré.

## Organisation du code

```
src/SchoolHub/
├── Program.cs
├── Socle/                 ce qui sert à tous les modules
│   ├── Centres/           le centre, et la séparation des centres
│   └── Donnees/           le contexte EF Core et les migrations
└── Modules/               un dossier par module, quand ses premières stories arrivent :
                           élèves et inscriptions, groupes et séances, caisse et reçus,
                           bibliothèque, professeurs, RH et achats, journal
tests/SchoolHub.Tests/
```

## La séparation des centres

Tous les centres partagent une seule base ([ADR 0001](../docs/adr/0001-une-base-partagee-entre-les-centres.md)). Une donnée qui appartient à un centre implémente `IAppartientAuCentre`, et c'est tout :

- **à l'ajout**, elle prend le centre courant, celui du compte connecté ;
- **à la lecture**, un filtre ne montre que les données du centre courant ; sans centre courant, on ne voit rien ;
- **à l'enregistrement**, modifier ou supprimer la donnée d'un autre centre, ou la passer d'un centre à un autre, lève `DonneeHorsDuCentreException`.

Les tests de `ModeleTests` échouent si une nouvelle donnée oublie `IAppartientAuCentre`. Une donnée commune à tous les centres (comme le centre lui-même) doit être ajoutée exprès à leur liste `DonneesCommunes`.

## Ajouter une migration

```bash
dotnet ef migrations add <NomEnFrancais> --project src/SchoolHub --output-dir Socle/Donnees/Migrations
```

Les noms des tables et des colonnes sont en minuscules avec des `_` (`centre_id`), plus simples à écrire en SQL avec PostgreSQL.
