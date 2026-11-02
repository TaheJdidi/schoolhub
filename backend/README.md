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

## Créer un centre

L'équipe SchoolHub ouvre un nouveau centre avec une commande de l'application elle-même (CEN-01). Elle crée le centre et son premier propriétaire, puis envoie à celui-ci l'invitation pour choisir son mot de passe. Ensuite, le propriétaire fait le reste.

En développement (PostgreSQL démarré et migrations appliquées, comme ci-dessus) :

```bash
dotnet run --project src/SchoolHub -- creer-centre --nom "Centre Les Jasmins" --code les-jasmins --prenom Hedi --nom-proprietaire "Ben Salah" --email hedi@exemple.tn
```

En production, depuis la console SSH de l'App Service, qui a déjà la base et la configuration des emails :

```bash
dotnet SchoolHub.dll creer-centre --nom "…" --code … --prenom … --nom-proprietaire "…" --email …
```

- Le code ne contient que des lettres minuscules, des chiffres et des tirets (il va dans l'adresse du formulaire public).
- C'est tout ou rien : si le code ou l'email est déjà pris, ou si l'email d'invitation ne part pas, rien n'est créé. On corrige et on relance la commande.
- La commande écrit son résultat, puis s'arrête avec le code de sortie 0 si tout va bien, ou 1 pour un refus (code ou email déjà pris, option manquante…). Le serveur ne démarre pas. Une erreur inattendue (base injoignable…) affiche une trace, avec un code de sortie différent de 0, et rien n'est créé.
- En développement, l'email d'invitation, lien compris, est écrit dans la console. En production, la commande refuse de s'exécuter tant que l'envoi des emails n'est pas branché (Brevo).

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
│   ├── Centres/           le centre, la séparation des centres, la commande creer-centre
│   ├── Comptes/           les comptes, la connexion, l'invitation, le mot de passe oublié,
│   │                      les rôles et le centre courant
│   ├── Emails/            l'envoi des emails (dans la console en développement)
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

**Les comptes ne sont pas filtrés.** Le compte est une donnée commune : il est lu à la connexion, avant de connaître le centre (c'est lui qui le donne). `UserManager<Compte>` et `contexte.Users` ne sont donc PAS filtrés par centre. Toute autre lecture des comptes (une liste des utilisateurs, par exemple) doit filtrer elle-même sur le centre courant, et avoir son test d'isolation.

## Ajouter une migration

```bash
dotnet ef migrations add <NomEnFrancais> --project src/SchoolHub --output-dir Socle/Donnees/Migrations
```

Les noms des tables et des colonnes sont en minuscules avec des `_` (`centre_id`), plus simples à écrire en SQL avec PostgreSQL.
