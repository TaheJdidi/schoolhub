using System.Globalization;
using SchoolHub.Modules.Groupes;

namespace SchoolHub.Modules.Abonnements;

/// <summary>Les réglages des abonnements, lus dans la section « Abonnements » de la configuration.</summary>
public sealed class OptionsDesAbonnements
{
    public const string Section = "Abonnements";

    /// <summary>Le prix mensuel de l'offre unique, en dinars : 90,000 TND par défaut.</summary>
    public decimal PrixMensuel { get; set; } = 90m;
}

/// <summary>
/// Les messages au propriétaire (invitation, confirmation de paiement). Aucune donnée d'élève n'y entre jamais.
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
}
