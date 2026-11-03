using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Journal;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Socle;

/// <summary>
/// Le journal : qui a fait quoi, et quand. Une ligne appartient à son centre,
/// et personne ne peut la modifier ni l'effacer (exigences communes).
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class JournalTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private const string NomDeLaBase = "socle_journal";

    private const string Inalterable = "Le journal ne se modifie pas et ne s'efface pas";

    private ApplicationDeTest _application = null!;
    private Centre _jasmins = null!;
    private Centre _oliviers = null!;

    public async Task InitializeAsync()
    {
        _application = new ApplicationDeTest(postgreSql, NomDeLaBase);
        await _application.InitialiserLaBaseAsync();
        _jasmins = await _application.CreerCentreAsync();
        _oliviers = await _application.CreerCentreAsync("Centre Les Oliviers");
    }

    public Task DisposeAsync() => _application.DisposeAsync().AsTask();

    [Fact]
    public async Task Une_ligne_garde_l_heure_l_auteur_et_le_centre()
    {
        var hedi = await _application.CreerCompteAsync(_jasmins, "Hedi", "Ben Salah", RoleDuCompte.Proprietaire);

        await NoterAsync(_jasmins, hedi, ActionDuJournal.CompteDesactive, "Sonia Mabrouk · Secrétaire", "Actif", "Désactivé");

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal(_jasmins.Id, ligne.CentreId);
        Assert.Equal(hedi.Id, ligne.AuteurId);
        Assert.Equal("Hedi Ben Salah", ligne.Auteur);
        Assert.Equal(ActionDuJournal.CompteDesactive, ligne.Action);
        Assert.Equal("Sonia Mabrouk · Secrétaire", ligne.Detail);
        Assert.Equal(("Actif", "Désactivé"), (ligne.Avant, ligne.Apres));
        // PostgreSQL garde la microseconde, l'horloge de test le dixième de microseconde.
        Assert.True((ligne.Quand - _application.Horloge.GetUtcNow()).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Sans_auteur_la_ligne_est_ecrite_par_SchoolHub()
    {
        await NoterAsync(_jasmins, null, ActionDuJournal.CompteCree, "Hedi Ben Salah · Propriétaire · hedi@exemple.tn");

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Null(ligne.AuteurId);
        Assert.Equal("SchoolHub", ligne.Auteur);
        Assert.Null(ligne.Avant);
        Assert.Null(ligne.Apres);
    }

    [Fact]
    public async Task Un_centre_ne_lit_jamais_le_journal_d_un_autre()
    {
        await NoterAsync(_jasmins, null, ActionDuJournal.CompteCree, "ligne des Jasmins");
        await NoterAsync(_oliviers, null, ActionDuJournal.CompteCree, "ligne des Oliviers");

        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal("ligne des Jasmins", ligne.Detail);
    }

    [Theory]
    [InlineData("UPDATE journal SET detail = 'effacé'")]
    [InlineData("DELETE FROM journal")]
    [InlineData("TRUNCATE journal")]
    public async Task Le_journal_refuse_d_etre_modifie_ou_efface(string sql)
    {
        await NoterAsync(_jasmins, null, ActionDuJournal.CompteCree, "ligne à garder");

        var erreur = await _application.DansUnScopeAsync(services =>
            Assert.ThrowsAsync<PostgresException>(() =>
                services.GetRequiredService<SchoolHubDbContext>().Database.ExecuteSqlRawAsync(sql)));

        Assert.Equal(Inalterable, erreur.MessageText);
        var ligne = Assert.Single(await _application.JournalAsync(_jasmins));
        Assert.Equal("ligne à garder", ligne.Detail);
    }

    /// <summary>Note une ligne comme le fera une action : dans son centre, puis enregistrée avec le reste.</summary>
    private Task NoterAsync(
        Centre centre,
        Compte? auteur,
        ActionDuJournal action,
        string detail,
        string? avant = null,
        string? apres = null) =>
        _application.DansUnScopeAsync(async services =>
        {
            services.GetRequiredService<CentreCourant>().Definir(centre.Id);
            services.GetRequiredService<JournalDuCentre>().Noter(auteur, action, detail, avant, apres);
            return await services.GetRequiredService<SchoolHubDbContext>().SaveChangesAsync();
        });
}
