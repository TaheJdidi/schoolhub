namespace SchoolHub.Modules.Abonnements;

// Ce que l'API du back office et celle du centre donnent à lire. Les états, modes, objets et canaux sont les noms
// de leur énumération (« EnRetard », « Virement », « Invitation », « WhatsApp ») ; les montants sont en dinars.

/// <summary>
/// Un centre dans la liste du back office : son nom, son état, ses paiements et son nombre de comptes,
/// plus son premier propriétaire. L'Éditeur ne voit rien des élèves, des groupes ni de l'argent du centre.
/// </summary>
public sealed record LigneDeCentre(
    Guid Id,
    string Nom,
    string Code,
    string Etat,
    DateOnly? PayeJusquAu,
    decimal PrixMensuel,
    int NombreDeComptes,
    string? Proprietaire,
    DateOnly? ReactiveJusquAu);

/// <summary>La fiche d'un centre dans le back office.</summary>
public sealed record FicheDuCentre(
    Guid Id,
    string Nom,
    string Code,
    string Etat,
    DateOnly? PayeJusquAu,
    DateOnly? FinDeLaPeriodeDeGrace,
    DateOnly? SuspenduLe,
    string? Forcage,
    string? MotifDuForcage,
    DateOnly? ForceLe,
    decimal PrixMensuel,
    int NombreDeComptes,
    ProprietaireDuCentre? Proprietaire,
    IReadOnlyList<PaiementVu> Paiements,
    IReadOnlyList<EnvoiVu> Invitations,
    DateOnly? ReactiveJusquAu);

/// <summary>
/// Le premier propriétaire du centre (celui créé avec lui), pour lui envoyer son invitation.
/// L'état est celui de son compte (« Invite », « Actif »…).
/// </summary>
public sealed record ProprietaireDuCentre(string Prenom, string Nom, string Email, string? Telephone, string Etat);

public sealed record PaiementVu(
    Guid Id,
    DateOnly Date,
    decimal Montant,
    string Mode,
    DateOnly Du,
    DateOnly Au,
    string NumeroDeFacture,
    DateTimeOffset SaisiLe,
    string SaisiPar,
    IReadOnlyList<EnvoiVu> Confirmations);

public sealed record EnvoiVu(string Objet, string Canal, DateTimeOffset Quand, string Destinataire)
{
    public static EnvoiVu De(EnvoiAuProprietaire envoi) =>
        new(envoi.Objet.ToString(), envoi.Canal.ToString(), envoi.Quand, envoi.Destinataire);
}

/// <summary>Un lien WhatsApp à ouvrir (le message déjà écrit), et la fiche à jour, avec la date du clic.</summary>
public sealed record LienWhatsApp(string Lien, FicheDuCentre Fiche);

/// <summary>Un paiement enregistré, et ce qu'est devenue sa confirmation par email (EDI-04).</summary>
public sealed record PaiementEnregistre(FicheDuCentre Fiche, bool ConfirmationEnvoyee, string Message);

public sealed record LigneDuJournalVue(DateTimeOffset Quand, string Auteur, string Action, string Centre, string Detail);

/// <summary>Ce que l'Éditeur saisit pour créer un centre (EDI-01). Le prix mensuel est celui de l'offre si rien n'est donné.</summary>
public sealed record DemandeDeCentre(
    string? Nom,
    string? Code,
    string? PrenomDuProprietaire,
    string? NomDuProprietaire,
    string? EmailDuProprietaire,
    string? TelephoneDuProprietaire,
    decimal? PrixMensuel);

public sealed record SaisieDePaiement(
    DateOnly? Date,
    decimal? Montant,
    string? Mode,
    DateOnly? Du,
    DateOnly? Au,
    string? NumeroDeFacture);

public sealed record PrixDuCentre(decimal? PrixMensuel);

public sealed record CorrectionDuProprietaire(string? Prenom, string? Nom, string? Email, string? Telephone);

public sealed record Motif(string? Texte);

/// <summary>Le prix de l'offre, pour pré-remplir la création d'un centre (EDI-01).</summary>
public sealed record OffreDuBackOffice(decimal PrixMensuel);

/// <summary>L'abonnement vu par le centre (GET /api/abonnement) : de quoi afficher ses bandeaux (EDI-05).</summary>
public sealed record AbonnementDuCentre(
    string Etat,
    DateOnly? PayeJusquAu,
    DateOnly? FinDeLaPeriodeDeGrace,
    bool LectureSeule);
