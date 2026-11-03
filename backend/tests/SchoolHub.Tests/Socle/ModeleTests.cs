using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Socle;

/// <summary>
/// Garde-fous sur le modèle de données : ils échouent si une nouvelle donnée oublie son centre,
/// ou si une migration manque.
/// </summary>
public sealed class ModeleTests
{
    // Les seules données communes à tous les centres. Toute autre donnée appartient à un centre.
    // Le compte se lit avant de connaître le centre (connexion) : toute autre lecture des comptes
    // filtre sur le centre courant et a son test d'isolation.
    private static readonly Type[] DonneesCommunes =
    [
        typeof(Centre),
        typeof(Compte),
        typeof(IdentityUserClaim<Guid>),
        typeof(IdentityUserLogin<Guid>),
        typeof(IdentityUserToken<Guid>),
    ];

    [Fact]
    public void Chaque_donnee_appartient_a_un_centre_sauf_les_donnees_communes()
    {
        using var contexte = ContexteSansBase();

        var sansCentre = contexte.Model.GetEntityTypes()
            .Where(t => !t.IsOwned())
            .Select(t => t.ClrType)
            .Where(t => !typeof(IAppartientAuCentre).IsAssignableFrom(t) && !DonneesCommunes.Contains(t));

        Assert.Empty(sansCentre);
    }

    [Fact]
    public void Chaque_donnee_d_un_centre_est_filtree_par_centre()
    {
        using var contexte = ContexteSansBase();

        var sansFiltre = contexte.Model.GetEntityTypes()
            .Where(t => typeof(IAppartientAuCentre).IsAssignableFrom(t.ClrType))
            .Where(t => t.FindDeclaredQueryFilter(SchoolHubDbContext.FiltreDuCentre) is null)
            .Select(t => t.ClrType);

        Assert.Empty(sansFiltre);
    }

    [Fact]
    public void Les_migrations_suivent_le_modele()
    {
        using var contexte = ContexteSansBase();

        Assert.False(
            contexte.Database.HasPendingModelChanges(),
            "Le modèle a changé sans migration : lancer « dotnet ef migrations add <Nom> ».");
    }

    // Le modèle se construit sans se connecter : l'adresse de la base ne sert pas.
    private static SchoolHubDbContext ContexteSansBase()
    {
        var options = new DbContextOptionsBuilder<SchoolHubDbContext>();
        SchoolHubDbContext.Configurer(options, "Host=localhost");
        return new SchoolHubDbContext(options.Options, new CentreCourant());
    }
}

[Collection(CollectionPostgreSql.Nom)]
public sealed class MigrationsTests(PostgreSqlDeTest postgreSql)
{
    [Fact]
    public async Task Les_migrations_s_appliquent_sur_une_base_vide()
    {
        var options = new DbContextOptionsBuilder<SchoolHubDbContext>();
        SchoolHubDbContext.Configurer(options, postgreSql.ChaineDeConnexion("migrations"));
        await using var contexte = new SchoolHubDbContext(options.Options, new CentreCourant());

        await contexte.Database.MigrateAsync();

        Assert.Empty(await contexte.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task La_migration_donne_les_niveaux_d_office_aux_centres_existants()
    {
        var options = new DbContextOptionsBuilder<SchoolHubDbContext>();
        SchoolHubDbContext.Configurer(options, postgreSql.ChaineDeConnexion("migrations_niveaux"));
        await using var contexte = new SchoolHubDbContext(options.Options, new CentreCourant());
        var migrations = contexte.GetService<IMigrator>();

        // Un centre qui existait avant la liste des niveaux.
        await migrations.MigrateAsync("AjouterLesAnneesScolaires");
        var ancien = Guid.NewGuid();
        await contexte.Database.ExecuteSqlAsync($"INSERT INTO centres (id, nom, code) VALUES ({ancien}, 'Centre Ancien', 'centre-ancien')");

        await migrations.MigrateAsync();

        var noms = await contexte.Database
            .SqlQuery<string>($"SELECT nom AS \"Value\" FROM niveaux WHERE centre_id = {ancien} AND d_office ORDER BY rang")
            .ToListAsync();
        Assert.Equal(NiveauxDOffice.Liste, noms);
    }

    [Fact]
    public async Task Les_migrations_protegent_le_journal()
    {
        var options = new DbContextOptionsBuilder<SchoolHubDbContext>();
        SchoolHubDbContext.Configurer(options, postgreSql.ChaineDeConnexion("migrations_journal"));
        await using var contexte = new SchoolHubDbContext(options.Options, new CentreCourant());

        await contexte.Database.MigrateAsync();

        // TRUNCATE déclenche le trigger même sur une table vide ; UPDATE et DELETE ne le feraient pas.
        var erreur = await Assert.ThrowsAsync<PostgresException>(() => contexte.Database.ExecuteSqlRawAsync("TRUNCATE journal"));
        Assert.Equal("Le journal ne se modifie pas et ne s'efface pas", erreur.MessageText);
    }
}
