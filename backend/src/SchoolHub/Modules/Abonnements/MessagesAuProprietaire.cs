using System.Globalization;
using SchoolHub.Modules.Groupes;

namespace SchoolHub.Modules.Abonnements;

/// <summary>Les réglages des abonnements, lus dans la section « Abonnements » de la configuration.</summary>
public sealed class OptionsDesAbonnements
{
    public const string Section = "Abonnements";

    /// <summary>Le prix mensuel de l'offre unique, en dinars : 90,000 TND par défaut.</summary>
    public decimal PrixMensuel { get; set; } = 90m;

    /// <summary>Les emails automatiques de relance (EDI-06) : les tests les coupent, pour les lancer eux-mêmes.</summary>
    public bool RelancesAutomatiques { get; set; } = true;
}

/// <summary>
/// Les messages au propriétaire (invitation, confirmation de paiement, relance). Aucune donnée d'élève n'y entre jamais.
/// </summary>
public static class MessagesAuProprietaire
{
    /// <summary>Une date telle qu'on la lit : « 04/04/2027 ».</summary>
    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>« 270,000 TND ».</summary>
    public static string Montant(decimal montant) => $"{SaisieDuGroupe.EcrireUnMontant(montant)} TND";

    /// <summary>« virement », « espèces », « chèque ».</summary>
    public static string Mode(ModeDePaiement mode) => mode switch
    {
        ModeDePaiement.Virement => "virement",
        ModeDePaiement.Especes => "espèces",
        ModeDePaiement.Cheque => "chèque",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>Le message WhatsApp de l'invitation, avec son lien.</summary>
    public static string Invitation(string prenom, string centre, string lien) =>
        $"Bonjour {prenom}, voici votre lien pour rejoindre le {centre} sur SchoolUp et choisir votre mot de passe (valable 7 jours) : {lien}";

    /// <summary>« Paiement reçu : 270,000 TND, virement, du 05/01/2027 au 04/04/2027, facture F-2027-003 » (EDI-04).</summary>
    public static string ConfirmationDePaiement(PaiementDAbonnement paiement) =>
        $"Paiement reçu : {Montant(paiement.Montant)}, {Mode(paiement.Mode)}, du {Date(paiement.Du)} au {Date(paiement.Au)}, facture {paiement.NumeroDeFacture}";

    public static (string Sujet, string Texte) EmailDeConfirmation(string prenom, string centre, PaiementDAbonnement paiement) =>
        ($"Paiement reçu pour le {centre}",
            string.Join(
                '\n',
                $"Bonjour {prenom},",
                "",
                ConfirmationDePaiement(paiement) + ".",
                $"Votre abonnement SchoolUp est actif jusqu'au {Date(paiement.Au)} inclus.",
                "",
                "Merci de votre confiance."));

    /// <summary>« Votre abonnement se termine le 04/04/2027 » : le bandeau, la relance WhatsApp et les emails (EDI-06).</summary>
    public static string FinDeLAbonnement(DateOnly fin) => $"Votre abonnement se termine le {Date(fin)}";

    /// <summary>Le premier jour de la lecture seule, sans paiement : le lendemain de la période de grâce.</summary>
    private static string LectureSeuleLe(DateOnly fin) => Date(fin.AddDays(SituationDeLAbonnement.JoursDeGrace + 1));

    public static string Relance(string prenom, string centre, DateOnly fin, decimal prixMensuel) =>
        $"Bonjour {prenom}, l'abonnement SchoolUp du {centre} se termine le {Date(fin)}. "
        + $"Pour continuer sans interruption, merci de régler la suite ({Montant(prixMensuel)} par mois). "
        + $"Sans paiement, SchoolUp passera en lecture seule le {LectureSeuleLe(fin)}.";

    public static (string Sujet, string Texte) EmailDeRelance(string prenom, string centre, DateOnly fin, decimal prixMensuel, bool leJourMeme) =>
        (leJourMeme ? $"Votre abonnement SchoolUp se termine aujourd'hui ({Date(fin)})" : FinDeLAbonnement(fin),
            string.Join(
                '\n',
                $"Bonjour {prenom},",
                "",
                leJourMeme
                    ? $"L'abonnement SchoolUp du {centre} se termine aujourd'hui, le {Date(fin)}."
                    : $"L'abonnement SchoolUp du {centre} se termine le {Date(fin)}, dans 7 jours.",
                $"Pour continuer sans interruption, merci de régler la suite ({Montant(prixMensuel)} par mois).",
                $"Après le {Date(fin)}, SchoolUp fonctionne encore {SituationDeLAbonnement.JoursDeGrace} jours, "
                + $"puis passe en lecture seule le {LectureSeuleLe(fin)}."));
}
