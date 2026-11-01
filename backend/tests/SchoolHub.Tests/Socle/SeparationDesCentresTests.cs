using Microsoft.EntityFrameworkCore;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Socle;

/// <summary>
/// Un centre ne voit jamais les données d'un autre centre, même en tapant une adresse à la main
/// (exigences communes, ADR 0001).
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class SeparationDesCentresTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private Centre _jasmins = null!;
    private Centre _oliviers = null!;

    public async Task InitializeAsync()
    {
        await using var contexte = Contexte(centre: null);
        await contexte.Database.EnsureCreatedAsync();

        // De nouveaux centres pour chaque test : les tests ne se gênent pas entre eux.
        var suffixe = Guid.NewGuid().ToString("N")[..8];
        _jasmins = new Centre("Centre Les Jasmins", $"les-jasmins-{suffixe}");
        _oliviers = new Centre("Centre Les Oliviers", $"les-oliviers-{suffixe}");
        contexte.Centres.AddRange(_jasmins, _oliviers);
        await contexte.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Un_centre_ne_voit_que_ses_propres_donnees()
    {
        await AjouterNote(_jasmins, "note des Jasmins");
        await AjouterNote(_oliviers, "note des Oliviers");

        await using var contexte = Contexte(_jasmins);
        var textes = await contexte.Notes.Select(n => n.Texte).ToListAsync();

        Assert.Equal(["note des Jasmins"], textes);
    }

    [Fact]
    public async Task Un_centre_ne_trouve_pas_la_donnee_d_un_autre_centre_meme_avec_son_identifiant()
    {
        var noteDesOliviers = await AjouterNote(_oliviers, "note des Oliviers");

        await using var contexte = Contexte(_jasmins);

        Assert.Null(await contexte.Notes.FindAsync(noteDesOliviers.Id));
    }

    [Fact]
    public async Task Sans_centre_courant_on_ne_voit_aucune_donnee()
    {
        await AjouterNote(_jasmins, "note des Jasmins");

        await using var contexte = Contexte(centre: null);

        Assert.Empty(await contexte.Notes.ToListAsync());
    }

    [Fact]
    public async Task Une_donnee_ajoutee_prend_le_centre_courant()
    {
        var note = await AjouterNote(_jasmins, "note des Jasmins");

        Assert.Equal(_jasmins.Id, note.CentreId);
    }

    [Fact]
    public async Task Ajouter_une_donnee_sans_centre_courant_est_refuse()
    {
        await using var contexte = Contexte(centre: null);
        contexte.Notes.Add(new NoteDeTest("note sans centre"));

        await Assert.ThrowsAsync<DonneeHorsDuCentreException>(() => contexte.SaveChangesAsync());
    }

    [Fact]
    public async Task Modifier_la_donnee_d_un_autre_centre_est_refuse()
    {
        var noteDesOliviers = await AjouterNote(_oliviers, "note des Oliviers");

        await using var contexte = Contexte(_jasmins);
        contexte.Notes.Update(noteDesOliviers);
        noteDesOliviers.Texte = "modifiée par les Jasmins";

        await Assert.ThrowsAsync<DonneeHorsDuCentreException>(() => contexte.SaveChangesAsync());
    }

    [Fact]
    public async Task Supprimer_la_donnee_d_un_autre_centre_est_refuse()
    {
        var noteDesOliviers = await AjouterNote(_oliviers, "note des Oliviers");

        await using var contexte = Contexte(_jasmins);
        contexte.Notes.Remove(noteDesOliviers);

        await Assert.ThrowsAsync<DonneeHorsDuCentreException>(() => contexte.SaveChangesAsync());
    }

    [Fact]
    public async Task Passer_une_donnee_dans_un_autre_centre_est_refuse()
    {
        var note = await AjouterNote(_jasmins, "note des Jasmins");

        await using var contexte = Contexte(_jasmins);
        var chargee = await contexte.Notes.SingleAsync(n => n.Id == note.Id);
        contexte.Entry(chargee).Property(n => n.CentreId).CurrentValue = _oliviers.Id;

        await Assert.ThrowsAsync<DonneeHorsDuCentreException>(() => contexte.SaveChangesAsync());
    }

    private async Task<NoteDeTest> AjouterNote(Centre centre, string texte)
    {
        await using var contexte = Contexte(centre);
        var note = new NoteDeTest(texte);
        contexte.Notes.Add(note);
        await contexte.SaveChangesAsync();
        return note;
    }

    private ContexteDeTest Contexte(Centre? centre)
    {
        var centreCourant = new CentreCourant();
        if (centre is not null)
            centreCourant.Definir(centre.Id);

        var options = new DbContextOptionsBuilder<ContexteDeTest>();
        SchoolHubDbContext.Configurer(options, postgreSql.ChaineDeConnexion("separation_des_centres"));
        return new ContexteDeTest(options.Options, centreCourant);
    }
}
