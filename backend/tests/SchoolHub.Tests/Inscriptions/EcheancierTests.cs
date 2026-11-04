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
                new SommeDue("Mois 3 (3 séances)", 30.000m, 10, seances[9], "Mois 3"),
                new SommeDue("Mois 4", 40m, 13, seances[12], "Mois 4"),
                new SommeDue("Mois 5", 40m, 17, seances[16], "Mois 5"),
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

        Assert.Equal([new SommeDue("Mois 1", 40m, 1, seances[0], "Mois 1"), new SommeDue("Mois 2 (2 séances)", 20m, 5, seances[4], "Mois 2")], sommes);
    }

    [Fact]
    public void Un_prix_qui_se_divise_mal()
    {
        // 40,001 TND en 4 séances : 10,000 + 10,000 + 10,000 + 10,001. Arrivé à la 2ᵉ, Adem paie les trois dernières.
        var seances = Samedis(4);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40.001m)]), ModeDeTarification.ParMois, seances[1]);

        Assert.Equal([new SommeDue("Mois 1 (3 séances)", 30.001m, 2, seances[1], "Mois 1")], sommes);
    }

    // ----- Révision -----

    [Fact]
    public void Revision_en_totalite_et_a_la_seance()
    {
        var seances = Samedis(10);
        var revision = Revision(seances, 100m, 10);

        Assert.Equal(
            [new SommeDue("Totalité, séances 4 à 10", 70m, 4, seances[3], "Totalité")],
            Echeancier.Calculer(revision, ModeDeTarification.EnTotalite, seances[3]));
        Assert.Equal(
            [new SommeDue("Totalité", 100m, 1, seances[0], "Totalité")],
            Echeancier.Calculer(revision, ModeDeTarification.EnTotalite, seances[0]));

        var parSeance = Echeancier.Calculer(revision, ModeDeTarification.ALaSeance, seances[3]);
        Assert.Equal(7, parSeance.Count);
        Assert.Equal(new SommeDue("Séance 4", 10m, 4, seances[3], "Séance 4"), parSeance[0]);
        Assert.Equal(new SommeDue("Séance 10", 10m, 10, seances[9], "Séance 10"), parSeance[^1]);
    }

    [Fact]
    public void Un_prix_de_revision_qui_se_divise_mal()
    {
        // 50 TND en 3 séances : 16,667 + 16,667 + 16,666. Arrivé à la 2ᵉ : 33,333 TND.
        var seances = Samedis(3);

        Assert.Equal(
            [new SommeDue("Totalité, séances 2 à 3", 33.333m, 2, seances[1], "Totalité")],
            Echeancier.Calculer(Revision(seances, 50m, 3), ModeDeTarification.EnTotalite, seances[1]));
    }

    [Fact]
    public void Un_groupe_deja_en_cours()
    {
        // GRP-07 : une Révision saisie à la séance 4 sur 10 n'a dans SchoolHub que ses séances 4 à 10.
        var revision = Samedis(7);
        Assert.Equal(
            [new SommeDue("Totalité, séances 4 à 10", 70m, 4, revision[0], "Totalité")],
            Echeancier.Calculer(Revision(revision, 100m, 10, premiereSeance: 4), ModeDeTarification.EnTotalite, revision[0]));

        // Une Étude saisie au Mois 5 commence à la séance 17.
        var etude = Samedis(8);
        Assert.Equal(
            [new SommeDue("Mois 5 (3 séances)", 30m, 18, etude[1], "Mois 5"), new SommeDue("Mois 6", 40m, 21, etude[4], "Mois 6")],
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
            [new SommeDue("Formation en une fois", 300m, 6, seances[5], "Une fois")],
            Echeancier.Calculer(formation, ModeDeTarification.EnUneFois, seances[5]));
        Assert.Equal(
            [
                new SommeDue("Tranche 1", 100m, 6, seances[5], "Tranche 1"),
                new SommeDue("Tranche 2", 100m, 6, seances[5], "Tranche 2"),
                new SommeDue("Tranche 3", 100m, 9, seances[8], "Tranche 3"),
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

    // ----- Le prix spécial, les règlements et le reste (ELV-05, ELV-07, ELV-10) -----

    private static Dictionary<string, decimal> Reglees(params (string Cle, decimal Montant)[] reglements) =>
        reglements.ToDictionary(r => r.Cle, r => r.Montant);

    private static CalendrierAPayer FormationEnTranches(List<DateOnly> seances) =>
        new(TypeDeGroupe.Formation, 1, seances, [], 300m, 10, [new Tranche(100m, 1), new Tranche(100m, 5), new Tranche(100m, 9)]);

    [Fact]
    public void Les_cles_des_sommes()
    {
        var seances = Samedis(10);

        Assert.Equal("Mois 1", Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]), ModeDeTarification.ParMois, seances[1])[0].Cle);
        Assert.Equal("Séance 4", Echeancier.Calculer(Revision(seances, 100m, 10), ModeDeTarification.ALaSeance, seances[3])[0].Cle);
        Assert.Equal("Totalité", Echeancier.Calculer(Revision(seances, 100m, 10), ModeDeTarification.EnTotalite, seances[3])[0].Cle);
        Assert.Equal("Une fois", Echeancier.Calculer(FormationEnTranches(seances), ModeDeTarification.EnUneFois, seances[0])[0].Cle);
        Assert.Equal(["Tranche 1", "Tranche 2", "Tranche 3"],
            Echeancier.Calculer(FormationEnTranches(seances), ModeDeTarification.EnTranches, seances[0]).Select(s => s.Cle));
    }

    [Fact]
    public void L_exemple_du_prix_special()
    {
        // ELV-05 : Adem a payé les Mois 1 à 3 à 40 TND ; le propriétaire lui accorde 20 TND : les Mois suivants passent à 20 TND.
        var seances = Samedis(20);
        var conditions = new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], 20m,
            Reglees(("Mois 1", 40m), ("Mois 2", 40m), ("Mois 3", 40m)));

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]), conditions);

        Assert.Equal([(40m, true), (40m, true), (40m, true), (20m, false), (20m, false)], sommes.Select(s => (s.Montant, s.Reglee)));
    }

    [Fact]
    public void Le_prix_special_ne_change_pas_ce_qui_est_regle()
    {
        // Le Mois 3 en cours n'est pas payé : il prend le prix spécial, comme les suivants.
        var seances = Samedis(12);
        var conditions = new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], 20m, Reglees(("Mois 1", 40m), ("Mois 2", 40m)));

        Assert.Equal([40m, 40m, 20m], Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]), conditions).Select(s => s.Montant));
    }

    [Fact]
    public void Un_prix_special_nul()
    {
        var seances = Samedis(8);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[1], 0m));

        Assert.Equal([0m, 0m], sommes.Select(s => s.Montant));
    }

    [Fact]
    public void Le_prix_special_d_une_revision()
    {
        // 100 TND en 10 séances, prix spécial 50 TND : 5 TND la séance ; depuis la séance 4, la totalité vaut 35 TND.
        var seances = Samedis(10);
        var revision = Revision(seances, 100m, 10);

        Assert.All(Echeancier.Calculer(revision, new ConditionsDeLInscription(ModeDeTarification.ALaSeance, seances[3], 50m)),
            s => Assert.Equal(5m, s.Montant));
        Assert.Equal([35m], Echeancier.Calculer(revision, new ConditionsDeLInscription(ModeDeTarification.EnTotalite, seances[3], 50m))
            .Select(s => s.Montant));
    }

    [Fact]
    public void Le_prix_special_d_une_formation_en_tranches()
    {
        // ELV-05 : 300 TND en 3 × 100 TND avec un prix spécial de 150 TND donnent 3 × 50 TND.
        var seances = Samedis(10);
        var formation = FormationEnTranches(seances);

        Assert.Equal([50m, 50m, 50m],
            Echeancier.Calculer(formation, new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 150m)).Select(s => s.Montant));
        // La tranche 1 est déjà payée à 100 TND : les deux autres se partagent les 50 TND qui restent.
        Assert.Equal([(100m, true), (25m, false), (25m, false)],
            Echeancier.Calculer(formation, new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 150m, Reglees(("Tranche 1", 100m))))
                .Select(s => (s.Montant, s.Reglee)));
    }

    [Fact]
    public void Les_tranches_ne_descendent_pas_sous_zero()
    {
        var seances = Samedis(10);

        var sommes = Echeancier.Calculer(FormationEnTranches(seances),
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 150m, Reglees(("Tranche 1", 100m), ("Tranche 2", 100m))));

        Assert.Equal([100m, 100m, 0m], sommes.Select(s => s.Montant));
    }

    [Fact]
    public void Le_reste_regroupe()
    {
        // ELV-10 : 3 tranches de 100 TND, la première payée ; « Payer tout le reste » le jour de la séance 3 : 200 TND ce jour-là.
        var seances = Samedis(10);

        var sommes = Echeancier.Calculer(FormationEnTranches(seances),
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], null, Reglees(("Tranche 1", 100m)), seances[2]));

        Assert.Equal(
            [new SommeDue("Tranche 1", 100m, 1, seances[0], "Tranche 1", true), new SommeDue("Reste de la Formation", 200m, 5, seances[2], "Reste")],
            sommes);
    }

    [Fact]
    public void Le_reste_prend_le_prix_special()
    {
        var seances = Samedis(10);

        var sommes = Echeancier.Calculer(FormationEnTranches(seances),
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 150m, Reglees(("Tranche 1", 100m)), seances[2]));

        Assert.Equal([("Tranche 1", 100m), ("Reste", 50m)], sommes.Select(s => (s.Cle, s.Montant)));
    }

    // ----- Corrections de la relecture -----

    [Fact]
    public void Le_reste_laisse_a_part_la_tranche_due_a_l_arrivee()
    {
        // Une Formation saisie à la séance 4 (tranches aux séances 1, 5 et 9) : la tranche 1, d'avant SchoolHub, est due à la
        // première séance de l'élève, encore à venir. « Payer tout le reste » la veille ne regroupe que les tranches 2 et 3.
        var seances = Samedis(7);
        var formation = new CalendrierAPayer(TypeDeGroupe.Formation, 4, seances, [], 300m, 10,
            [new Tranche(100m, 1), new Tranche(100m, 5), new Tranche(100m, 9)]);
        var veille = seances[0].AddDays(-1);

        var sommes = Echeancier.Calculer(formation, new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], null, null, veille));

        Assert.Equal([("Reste", 200m, veille), ("Tranche 1", 100m, seances[0])], sommes.Select(s => (s.Cle, s.Montant, s.Date)));
    }

    [Fact]
    public void Le_prix_special_compte_le_reste_deja_regle()
    {
        // La tranche 1 n'est pas payée ; le reste (tranches 2 et 3, 200 TND) l'est. Un prix spécial de 150 TND : la tranche 1 vaut 0.
        var seances = Samedis(10);

        var sommes = Echeancier.Calculer(FormationEnTranches(seances),
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 150m, Reglees(("Reste", 200m)), seances[2]));

        Assert.Equal([("Tranche 1", 0m, false), ("Reste", 200m, true)], sommes.Select(s => (s.Cle, s.Montant, s.Reglee)));
    }

    [Fact]
    public void On_ne_regroupe_pas_un_reste_nul()
    {
        // La tranche 1 est payée 100 TND, et le prix spécial vaut 100 TND : les tranches 2 et 3 valent 0, rien n'est à regrouper.
        var seances = Samedis(10);
        var formation = FormationEnTranches(seances);

        Assert.True(Echeancier.PeutRegrouperLeReste(formation,
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], null, Reglees(("Tranche 1", 100m))), seances[2]));
        Assert.False(Echeancier.PeutRegrouperLeReste(formation,
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], 100m, Reglees(("Tranche 1", 100m))), seances[2]));
        Assert.False(Echeancier.PeutRegrouperLeReste(formation,
            new ConditionsDeLInscription(ModeDeTarification.EnTranches, seances[0], null, null, seances[1]), seances[2]));
    }

    // ----- L'arrêt et le report (ELV-08, ELV-09) -----

    [Fact]
    public void L_exemple_de_l_arret()
    {
        // ELV-08 : le Mois 5 a commencé (séance 17) ; l'arrêt tombe 10 jours plus tard : le Mois 5 reste dû en entier, le Mois 6 ne l'est plus.
        var seances = Samedis(24);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], ArreteLe: seances[16].AddDays(10)));

        Assert.Equal(["Mois 1", "Mois 2", "Mois 3", "Mois 4", "Mois 5"], sommes.Select(s => s.Cle));
        Assert.Equal(40m, sommes[^1].Montant);
    }

    [Fact]
    public void Une_somme_payee_apres_l_arret_reste_reglee()
    {
        var seances = Samedis(24);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], null, Reglees(("Mois 6", 40m)), ArreteLe: seances[16].AddDays(10)));

        Assert.Equal(("Mois 6", 40m, true), (sommes[^1].Cle, sommes[^1].Montant, sommes[^1].Reglee));
    }

    [Fact]
    public void Un_arret_avant_la_premiere_seance()
    {
        var seances = Samedis(8);

        Assert.Empty(Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], ArreteLe: seances[0].AddDays(-1))));
    }

    [Fact]
    public void L_exemple_du_report()
    {
        // ELV-09 : Adem arrive à la 2ᵉ séance d'un Mois à 48 TND : il doit 36 TND, moins 30 TND, soit 6 TND.
        var seances = Samedis(8);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 48m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[1], Report: 30m));

        Assert.Equal([6m, 48m], sommes.Select(s => s.Montant));
    }

    [Fact]
    public void Le_surplus_du_report()
    {
        var seances = Samedis(8);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 48m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[1], Report: 50m));

        Assert.Equal([0m, 34m], sommes.Select(s => s.Montant));
    }

    [Fact]
    public void Une_somme_reglee_garde_sa_part_du_report()
    {
        // Les 6 TND du Mois 1 sont payés : recalculer ne refait pas servir le report sur le Mois 2.
        var seances = Samedis(8);

        var sommes = Echeancier.Calculer(Etude(seances, [new PrixDuGroupe(1, 48m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[1], null, Reglees(("Mois 1", 6m)), Report: 30m));

        Assert.Equal([(6m, true), (48m, false)], sommes.Select(s => (s.Montant, s.Reglee)));
    }

    [Fact]
    public void La_valeur_non_suivie()
    {
        var seances = Samedis(10);

        // Le Mois 1 payé 40 TND, le changement après sa séance 1 : 3 séances sur 4, 30 TND.
        Assert.Equal(30m, Echeancier.ValeurNonSuivie(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0], null, Reglees(("Mois 1", 40m))), seances[1]));
        // Deux séances de Révision payées d'avance, pas suivies : leur prix.
        Assert.Equal(20m, Echeancier.ValeurNonSuivie(Revision(seances, 100m, 10),
            new ConditionsDeLInscription(ModeDeTarification.ALaSeance, seances[3], null, Reglees(("Séance 5", 10m), ("Séance 6", 10m))), seances[4]));
        // La totalité des séances 4 à 10 payée 70 TND, le changement à la séance 7 : 4 séances sur 7, 40 TND.
        Assert.Equal(40m, Echeancier.ValeurNonSuivie(Revision(Samedis(7), 100m, 10, premiereSeance: 4),
            new ConditionsDeLInscription(ModeDeTarification.EnTotalite, seances[0], null, Reglees(("Totalité", 70m))), seances[3]));
        // Une Formation payée 300 TND en une fois, 10 séances, le changement à la séance 6 : la moitié.
        Assert.Equal(150m, Echeancier.ValeurNonSuivie(FormationEnTranches(seances),
            new ConditionsDeLInscription(ModeDeTarification.EnUneFois, seances[0], null, Reglees(("Une fois", 300m))), seances[5]));
        // Rien de payé : rien à reporter.
        Assert.Equal(0m, Echeancier.ValeurNonSuivie(Etude(seances, [new PrixDuGroupe(1, 40m)]),
            new ConditionsDeLInscription(ModeDeTarification.ParMois, seances[0]), seances[1]));
    }

}
