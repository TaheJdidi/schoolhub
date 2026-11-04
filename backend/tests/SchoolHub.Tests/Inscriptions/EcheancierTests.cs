using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;

namespace SchoolHub.Tests.Inscriptions;

/// <summary>
/// ELV-03 : ce qu'une inscription doit, séance par séance, selon le type du groupe et le mode de tarification, quand
/// l'élève arrive en cours de route. Ce sont des calculs, sans base de données. Les groupes ont lieu le samedi.
/// </summary>
public sealed class EcheancierTests
{
    /// <summary>Le premier samedi de l'année scolaire 2027-2028.</summary>
    private static readonly DateOnly Rentree = new(2027, 9, 18);

    private static List<DateOnly> Samedis(int nombre, DateOnly? du = null) =>
        Enumerable.Range(0, nombre).Select(i => (du ?? Rentree).AddDays(7 * i)).ToList();

    private static CalendrierAPayer Etude(List<DateOnly> seances, PrixDuGroupe[] prix, int premiereSeance = 1) =>
        new(TypeDeGroupe.Etude, premiereSeance, seances, prix, null, null, []);

    private static CalendrierAPayer Revision(List<DateOnly> seances, decimal total, int nombre, int premiereSeance = 1) =>
        new(TypeDeGroupe.Revision, premiereSeance, seances, [], total, nombre, []);

    // ----- Étude : par Mois -----

    [Fact]
    public void Arrivee_en_cours_de_mois()
    {
        // L'exemple de la story : Adem rejoint le groupe à 40 TND le Mois, à la 2ᵉ séance du Mois 3 (la séance 10).
        var seances = Samedis(20);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]), ModeDeTarification.ParMois, seances[9]);

        Assert.Equal(
            [
                new SommeDue("Mois 3 (3 séances)", 30.000m, 10, seances[9]),
                new SommeDue("Mois 4", 40m, 13, seances[12]),
                new SommeDue("Mois 5", 40m, 17, seances[16]),
            ],
            sommes);
    }

    [Fact]
    public void Un_prix_qui_change_garde_l_ancien_pour_un_mois_commence()
    {
        var seances = Samedis(20);
        var prix = new[] { new PrixDuGroupe(1, 40m), new PrixDuGroupe(4, 45m) };

        var sommes = Echeancier.Calculer(Etude(seances, prix), ModeDeTarification.ParMois, seances[0]);

        Assert.Equal([40m, 40m, 40m, 45m, 45m], sommes.Select(s => s.Montant));
        Assert.Equal(["Mois 1", "Mois 2", "Mois 3", "Mois 4", "Mois 5"], sommes.Select(s => s.Libelle));
    }

    [Fact]
    public void Le_dernier_mois_incomplet()
    {
        var seances = Samedis(6);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]), ModeDeTarification.ParMois, seances[0]);

        Assert.Equal([new SommeDue("Mois 1", 40m, 1, seances[0]), new SommeDue("Mois 2 (2 séances)", 20m, 5, seances[4])], sommes);
    }

    [Fact]
    public void Un_prix_qui_se_divise_mal()
    {
        // 40,001 TND en 4 séances : 10,000 + 10,000 + 10,000 + 10,001. Arrivé à la 2ᵉ, Adem paie les trois dernières.
        var seances = Samedis(4);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40.001m)]), ModeDeTarification.ParMois, seances[1]);

        Assert.Equal([new SommeDue("Mois 1 (3 séances)", 30.001m, 2, seances[1])], sommes);
    }

    // ----- Révision -----

    [Fact]
    public void Revision_en_totalite_et_a_la_seance()
    {
        var seances = Samedis(10);
        var revision = Revision(seances, 100m, 10);

        Assert.Equal(
            [new SommeDue("Totalité, séances 4 à 10", 70m, 4, seances[3])],
            Echeancier.Calculer(revision, ModeDeTarification.EnTotalite, seances[3]));
        Assert.Equal(
            [new SommeDue("Totalité", 100m, 1, seances[0])],
            Echeancier.Calculer(revision, ModeDeTarification.EnTotalite, seances[0]));

        var parSeance = Echeancier.Calculer(revision, ModeDeTarification.ALaSeance, seances[3]);
        Assert.Equal(7, parSeance.Count);
        Assert.Equal(new SommeDue("Séance 4", 10m, 4, seances[3]), parSeance[0]);
        Assert.Equal(new SommeDue("Séance 10", 10m, 10, seances[9]), parSeance[^1]);
    }

    [Fact]
    public void Un_prix_de_revision_qui_se_divise_mal()
    {
        // 50 TND en 3 séances : 16,667 + 16,667 + 16,666. Arrivé à la 2ᵉ : 33,333 TND.
        var seances = Samedis(3);

        Assert.Equal(
            [new SommeDue("Totalité, séances 2 à 3", 33.333m, 2, seances[1])],
            Echeancier.Calculer(Revision(seances, 50m, 3), ModeDeTarification.EnTotalite, seances[1]));
    }

    [Fact]
    public void Un_groupe_deja_en_cours()
    {
        // GRP-07 : une Révision saisie à la séance 4 sur 10 n'a dans SchoolHub que ses séances 4 à 10.
        var revision = Samedis(7);
        Assert.Equal(
            [new SommeDue("Totalité, séances 4 à 10", 70m, 4, revision[0])],
            Echeancier.Calculer(Revision(revision, 100m, 10, premiereSeance: 4), ModeDeTarification.EnTotalite, revision[0]));

        // Une Étude saisie au Mois 5 commence à la séance 17.
        var etude = Samedis(8);
        Assert.Equal(
            [new SommeDue("Mois 5 (3 séances)", 30m, 18, etude[1]), new SommeDue("Mois 6", 40m, 21, etude[4])],
            Echeancier.Calculer(Etude(etude, [new PrixDuGroupe(5, 40m)], premiereSeance: 17), ModeDeTarification.ParMois, etude[1]));
    }

    // ----- Formation -----

    [Fact]
    public void Formation_en_une_fois_et_en_tranches()
    {
        // 300 TND en 10 séances, trois tranches de 100 TND aux séances 1, 5 et 9 ; Sami arrive à la séance 6.
        var seances = Samedis(10);
        var formation = new CalendrierAPayer(TypeDeGroupe.Formation, 1, seances, [], 300m, 10,
            [new Tranche(100m, 1), new Tranche(100m, 5), new Tranche(100m, 9)]);

        Assert.Equal(
            [new SommeDue("Formation en une fois", 300m, 6, seances[5])],
            Echeancier.Calculer(formation, ModeDeTarification.EnUneFois, seances[5]));
        Assert.Equal(
            [
                new SommeDue("Tranche 1", 100m, 6, seances[5]),
                new SommeDue("Tranche 2", 100m, 6, seances[5]),
                new SommeDue("Tranche 3", 100m, 9, seances[8]),
            ],
            Echeancier.Calculer(formation, ModeDeTarification.EnTranches, seances[5]));
    }

    // ----- La première séance -----

    [Fact]
    public void La_premiere_seance_est_celle_du_jour_ou_la_suivante()
    {
        var seances = Samedis(3);
        var revision = Revision(seances, 50m, 3);

        Assert.Equal(2, Echeancier.PremiereSeance(revision, seances[1]));
        Assert.Equal(2, Echeancier.PremiereSeance(revision, seances[0].AddDays(1)));
        Assert.Equal(1, Echeancier.PremiereSeance(revision, seances[0].AddDays(-30)));
    }

    [Fact]
    public void Sans_seance_apres_le_debut()
    {
        var seances = Samedis(3);
        var revision = Revision(seances, 50m, 3);

        Assert.Null(Echeancier.PremiereSeance(revision, seances[^1].AddDays(1)));
        Assert.Empty(Echeancier.Calculer(revision, ModeDeTarification.EnTotalite, seances[^1].AddDays(1)));
    }

    [Fact]
    public void Les_modes_possibles()
    {
        Assert.Equal([ModeDeTarification.ParMois], Echeancier.ModesPossibles(TypeDeGroupe.Etude, aDesTranches: false));
        Assert.Equal([ModeDeTarification.ALaSeance, ModeDeTarification.EnTotalite], Echeancier.ModesPossibles(TypeDeGroupe.Revision, false));
        Assert.Equal([ModeDeTarification.EnUneFois], Echeancier.ModesPossibles(TypeDeGroupe.Formation, false));
        Assert.Equal([ModeDeTarification.EnUneFois, ModeDeTarification.EnTranches], Echeancier.ModesPossibles(TypeDeGroupe.Formation, true));
    }
}
