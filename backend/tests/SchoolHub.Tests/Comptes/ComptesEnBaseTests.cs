using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Donnees;
using SchoolHub.Tests.Outils;

namespace SchoolHub.Tests.Comptes;

/// <summary>
/// Le compte tel qu'il est enregistré en base : un email unique dans tout SchoolHub,
/// et un rôle et un état lisibles en texte.
/// </summary>
[Collection(CollectionPostgreSql.Nom)]
public sealed class ComptesEnBaseTests(PostgreSqlDeTest postgreSql) : IAsyncLifetime
{
    private Centre _jasmins = null!;
    private Centre _oliviers = null!;
    private string _suffixe = null!;

    public async Task InitializeAsync()
    {
        await using var contexte = Contexte();
        await contexte.Database.EnsureCreatedAsync();

        // De nouveaux centres et de nouveaux emails pour chaque test : les tests ne se gênent pas entre eux.
        _suffixe = Guid.NewGuid().ToString("N")[..8];
        _jasmins = new Centre("Centre Les Jasmins", $"les-jasmins-{_suffixe}");
        _oliviers = new Centre("Centre Les Oliviers", $"les-oliviers-{_suffixe}");
        contexte.Centres.AddRange(_jasmins, _oliviers);
        await contexte.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Deux_comptes_ne_peuvent_pas_avoir_le_meme_email_meme_dans_deux_centres()
    {
        var email = $"amira-{_suffixe}@exemple.tn";
        var emailNormalise = email.ToUpperInvariant();
        var amira = new Compte(_jasmins.Id, "Amira", "Trabelsi", email, RoleDuCompte.Secretaire) { NormalizedEmail = emailNormalise };
        var autre = new Compte(_oliviers.Id, "Amira", "Trabelsi", email, RoleDuCompte.Proprietaire) { NormalizedEmail = emailNormalise };

        await using (var contexte = Contexte())
        {
            contexte.Users.Add(amira);
            await contexte.SaveChangesAsync();
        }

        await using (var contexte = Contexte())
        {
            contexte.Users.Add(autre);
            await Assert.ThrowsAsync<DbUpdateException>(() => contexte.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Le_role_et_l_etat_sont_enregistres_en_texte()
    {
        var amira = new Compte(_jasmins.Id, "Amira", "Trabelsi", $"amira-{_suffixe}@exemple.tn", RoleDuCompte.Secretaire);
        await using (var contexte = Contexte())
        {
            contexte.Users.Add(amira);
            await contexte.SaveChangesAsync();
        }

        await using var connexion = new NpgsqlConnection(postgreSql.ChaineDeConnexion("comptes_en_base"));
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand("SELECT role, etat FROM comptes WHERE id = @id", connexion);
        commande.Parameters.AddWithValue("id", amira.Id);
        await using var lecteur = await commande.ExecuteReaderAsync();

        Assert.True(await lecteur.ReadAsync());
        Assert.Equal("Secretaire", lecteur.GetString(0));
        Assert.Equal("Invite", lecteur.GetString(1));
    }

    // Les comptes se lisent avant de connaître le centre : ici, comme à la connexion, il n'y a pas de centre courant.
    private SchoolHubDbContext Contexte()
    {
        var options = new DbContextOptionsBuilder<SchoolHubDbContext>();
        SchoolHubDbContext.Configurer(options, postgreSql.ChaineDeConnexion("comptes_en_base"));
        return new SchoolHubDbContext(options.Options, new CentreCourant());
    }
}
