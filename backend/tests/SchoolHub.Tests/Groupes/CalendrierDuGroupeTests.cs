using Microsoft.Extensions.Time.Testing;
using SchoolHub.Modules.Groupes;
using SchoolHub.Socle.Dates;

namespace SchoolHub.Tests.Groupes;

/// <summary>
/// GRP-04 : les séances d'un groupe depuis ses créneaux, rangées en Mois de 4, le prix de chaque Mois et le nom proposé.
/// Ce sont des calculs, sans base de données.
/// </summary>
public sealed class CalendrierDuGroupeTests
{
    private static readonly Creneau Samedi10h = new(Jour.Samedi, new TimeOnly(10, 0), new TimeOnly(12, 0));

    [Fact]
    public void L_exemple_de_la_story()
    {
        var seances = CalendrierDuGroupe.Seances([Samedi10h], new DateOnly(2027, 9, 18), new DateOnly(2028, 6, 30));

        Assert.Equal(41, seances.Count);
        Assert.Equal(new SeancePrevue(new DateOnly(2027, 9, 18), new TimeOnly(10, 0), new TimeOnly(12, 0)), seances[0]);
        Assert.Equal(new DateOnly(2028, 6, 24), seances[^1].Date);
        Assert.All(seances, s => Assert.Equal(DayOfWeek.Saturday, s.Date.DayOfWeek));
    }

    [Fact]
    public void Les_seances_commencent_au_premier_jour_du_creneau()
    {
        var seances = CalendrierDuGroupe.Seances([Samedi10h], new DateOnly(2027, 9, 15), new DateOnly(2027, 10, 1));

        Assert.Equal([new DateOnly(2027, 9, 18), new DateOnly(2027, 9, 25)], seances.Select(s => s.Date));
    }

    [Fact]
    public void Les_bornes_sont_comprises()
    {
        var seances = CalendrierDuGroupe.Seances([Samedi10h], new DateOnly(2027, 9, 18), new DateOnly(2027, 9, 25));

        Assert.Equal(2, seances.Count);
    }

    [Fact]
    public void Deux_creneaux_par_semaine_sont_ranges_par_date_puis_heure()
    {
        Creneau[] creneaux =
        [
            Samedi10h,
            new(Jour.Mardi, new TimeOnly(17, 0), new TimeOnly(19, 0)),
            new(Jour.Samedi, new TimeOnly(8, 0), new TimeOnly(10, 0)),
        ];

        var seances = CalendrierDuGroupe.Seances(creneaux, new DateOnly(2027, 9, 13), new DateOnly(2027, 9, 19));

        Assert.Equal(
            [
                (new DateOnly(2027, 9, 14), new TimeOnly(17, 0)),
                (new DateOnly(2027, 9, 18), new TimeOnly(8, 0)),
                (new DateOnly(2027, 9, 18), new TimeOnly(10, 0)),
            ],
            seances.Select(s => (s.Date, s.Debut)));
    }

    [Fact]
    public void Les_mois_sont_des_paquets_de_4()
    {
        var mois = CalendrierDuGroupe.EnMois(Enumerable.Range(1, 10));

        Assert.Equal([[1, 2, 3, 4], [5, 6, 7, 8], [9, 10]], mois);
    }

    [Fact]
    public void Avec_deux_creneaux_par_semaine_un_mois_dure_deux_semaines()
    {
        Creneau[] creneaux = [new(Jour.Mardi, new TimeOnly(17, 0), new TimeOnly(19, 0)), Samedi10h];
        var seances = CalendrierDuGroupe.Seances(creneaux, new DateOnly(2027, 9, 13), new DateOnly(2027, 10, 31));

        var mois1 = CalendrierDuGroupe.EnMois(seances)[0];

        Assert.Equal(
            [new DateOnly(2027, 9, 14), new DateOnly(2027, 9, 18), new DateOnly(2027, 9, 21), new DateOnly(2027, 9, 25)],
            mois1.Select(s => s.Date));
    }

    [Fact]
    public void Le_prix_d_un_mois_est_celui_de_la_derniere_ligne_qui_ne_le_depasse_pas()
    {
        PrixDuGroupe[] prix = [new(1, 40m), new(6, 45m)];

        Assert.Equal(40m, CalendrierDuGroupe.PrixDuMois(prix, 1));
        Assert.Equal(40m, CalendrierDuGroupe.PrixDuMois(prix, 5));
        Assert.Equal(45m, CalendrierDuGroupe.PrixDuMois(prix, 6));
        Assert.Equal(45m, CalendrierDuGroupe.PrixDuMois(prix, 9));
    }

    [Theory]
    [InlineData(2027, 12, 31, 1)]
    [InlineData(2028, 1, 15, 3)]
    [InlineData(2028, 2, 1, 3)]
    [InlineData(2028, 2, 13, 4)]
    public void Le_premier_mois_non_commence(int annee, int mois, int jour, int attendu)
    {
        DateOnly[] premieresDates = [new(2028, 1, 1), new(2028, 1, 15), new(2028, 2, 12)];

        Assert.Equal(attendu, CalendrierDuGroupe.PremierMoisNonCommence(premieresDates, new DateOnly(annee, mois, jour)));
    }

    [Theory]
    [InlineData(Jour.Samedi, 10, 0, 12, 0, "Groupe Samedi 10h → 12h")]
    [InlineData(Jour.Mardi, 17, 30, 19, 0, "Groupe Mardi 17h30 → 19h")]
    [InlineData(Jour.Dimanche, 8, 0, 10, 0, "Groupe Dimanche 8h → 10h")]
    public void Le_nom_propose(Jour jour, int h1, int m1, int h2, int m2, string attendu)
    {
        var nom = CalendrierDuGroupe.NomPropose(new Creneau(jour, new TimeOnly(h1, m1), new TimeOnly(h2, m2)));

        Assert.Equal(attendu, nom);
    }

    [Theory]
    [InlineData("samedi", Jour.Samedi)]
    [InlineData("Mercredi", Jour.Mercredi)]
    public void Un_jour_se_lit_sans_tenir_compte_des_majuscules(string saisie, Jour attendu)
    {
        Assert.True(Jours.EssayerDeLire(saisie, out var jour));
        Assert.Equal(attendu, jour);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Saturday")]
    [InlineData("8")]
    public void Un_jour_inconnu_ne_se_lit_pas(string? saisie)
    {
        Assert.False(Jours.EssayerDeLire(saisie, out _));
    }

    [Fact]
    public void La_date_de_Tunis()
    {
        var horloge = new FakeTimeProvider(new DateTimeOffset(2027, 12, 31, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2028, 1, 1), DateDeTunis.Aujourdhui(horloge));
    }
}
