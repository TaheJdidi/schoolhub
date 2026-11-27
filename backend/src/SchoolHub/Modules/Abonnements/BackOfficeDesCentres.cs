using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Emails;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Abonnements;

/// <summary>Ce qu'une action du back office a donné : sa valeur, ou un refus (statut HTTP et message en français).</summary>
public sealed record Issue<T>(T? Valeur, int Statut, string? Message) where T : class
{
    public static Issue<T> Reussie(T valeur) => new(valeur, StatusCodes.Status200OK, null);

    public static Issue<T> Refusee(int statut, string message) => new(null, statut, message);
}

/// <summary>
/// Le back office de l'Éditeur (EDI-01 à EDI-06, EDI-08) : les centres, leurs abonnements, leurs paiements, l'invitation
/// de leur premier propriétaire, leurs relances et la suppression d'un centre parti. L'Éditeur n'a pas de centre courant :
/// chaque lecture vise le centre demandé, et rien ne lit les données du centre lui-même (voir <see cref="AccesSupport"/>).
/// </summary>
public sealed class BackOfficeDesCentres(
    SchoolHubDbContext contexte,
    UserManager<Compte> comptes,
    CreationDeCentre creation,
    Invitations invitations,
    IEnvoiEmail emails,
    JournalDeLEditeur journal,
    TimeProvider temps,
    IOptions<OptionsDesAbonnements> options,
    ILogger<BackOfficeDesCentres> journaux)
{
    public const string Introuvable = "Centre introuvable";
    public const string ChampsDuCentreObligatoires =
        "Le nom du centre, le code de son lien, et le prénom, le nom, l'email et le téléphone du propriétaire sont obligatoires";
    public const string TelephonePasValable = "Le téléphone du propriétaire doit avoir 8 chiffres (ex. 98 401 220)";
    public const string EmailPasValable = "Cet email n'est pas valable";
    public const string EmailDejaUtilise = "Cet email est déjà utilisé sur SchoolUp";
    public const string PrixPasValable = "Le prix mensuel doit être supérieur à 0, au millime près";
    public const string ChampsDuPaiementObligatoires = "La date, le montant, le mode, la période et le n° de facture sont obligatoires";
    public const string MontantPasValable = "Le montant doit être supérieur à 0, au millime près";
    public const string PeriodePasValable = "La fin de la période doit venir après son début";
    public const string InvitationAvantLePaiement = "L'invitation part après le premier paiement d'abonnement : saisissez-le d'abord";
    public const string ProprietaireDejaInscrit = "Le propriétaire a déjà choisi son mot de passe : il se connecte avec son email";
    public const string SansTelephone = "Ajoutez le téléphone du propriétaire pour lui écrire sur WhatsApp";
    public const string EmailPasParti = "L'email n'est pas parti : réessayez dans un moment";
    public const string EmailFigeApresLInscription =
        "Le propriétaire a déjà choisi son mot de passe : son email de connexion ne se change plus depuis le back office";
    public const string MotifObligatoire = "Saisissez le motif";
    public const string DejaSuspendu = "Ce centre est déjà suspendu";
    public const string PasSuspendu = "Ce centre n'est pas suspendu";
    public const string PaiementIntrouvable = "Paiement introuvable";
    public const string RienARelancer = "Ce centre n'a pas de fin d'abonnement à rappeler";
    public const string NomARetaper = "Retapez exactement le nom du centre pour confirmer la suppression";

    private DateOnly Aujourdhui => DateDeTunis.Aujourdhui(temps);

    private decimal PrixDeLOffre => options.Value.PrixMensuel;

    public OffreDuBackOffice Offre() => new(PrixDeLOffre);

    // ----- La liste et la fiche -----

    /// <summary>
    /// Les centres, par nom, avec l'état de leur abonnement aujourd'hui. <paramref name="filtre"/> : « a-relancer » (fin dans
    /// 7 jours ou moins, ou En retard, EDI-06), « a-supprimer » (Suspendus depuis 12 mois, EDI-08), ou rien pour tous.
    /// </summary>
    public async Task<List<LigneDeCentre>> ListerAsync(string? filtre = null, CancellationToken annulation = default)
    {
        var centres = await contexte.Centres.AsNoTracking().OrderBy(c => c.Nom).ToListAsync(annulation);
        var abonnements = await contexte.Set<Abonnement>().AsNoTracking().ToDictionaryAsync(a => a.CentreId, annulation);
        var premiers = (await contexte.Users.AsNoTracking()
                .Where(c => c.Role == RoleDuCompte.Proprietaire)
                .OrderBy(c => c.Id)
                .Select(c => new { CentreId = c.CentreId!.Value, c.Prenom, c.Nom, c.PhoneNumber })
                .ToListAsync(annulation))
            .GroupBy(c => c.CentreId)
            .ToDictionary(g => g.Key, g => g.First());
        var nombres = await contexte.Users.AsNoTracking()
            .Where(c => c.CentreId != null)
            .GroupBy(c => c.CentreId!.Value)
            .Select(g => new { Centre = g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(g => g.Centre, g => g.Nombre, annulation);

        var aujourdhui = Aujourdhui;
        var lignes = centres.Select(centre =>
        {
            var abonnement = abonnements.GetValueOrDefault(centre.Id);
            var situation = SituationDeLAbonnement.Calculer(abonnement, aujourdhui);
            var proprietaire = premiers.GetValueOrDefault(centre.Id);
            return new LigneDeCentre(
                centre.Id,
                centre.Nom,
                centre.Code,
                situation.Etat.ToString(),
                situation.PayeJusquAu,
                abonnement?.PrixMensuel ?? PrixDeLOffre,
                nombres.GetValueOrDefault(centre.Id),
                proprietaire is null ? null : $"{proprietaire.Prenom} {proprietaire.Nom}",
                situation.ReactiveJusquAu,
                situation.ARelancer,
                situation.ASupprimer,
                situation.SuppressionPossibleLe,
                proprietaire?.PhoneNumber is not null);
        });

        return filtre switch
        {
            "a-relancer" => lignes.Where(l => l.ARelancer).OrderBy(l => l.PayeJusquAu).ToList(),
            "a-supprimer" => lignes.Where(l => l.ASupprimer).OrderBy(l => l.SuppressionPossibleLe).ToList(),
            _ => lignes.ToList(),
        };
    }

    public async Task<FicheDuCentre?> FicheAsync(Guid id, CancellationToken annulation = default)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        if (centre is null)
            return null;

        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == id, annulation);
        var situation = SituationDeLAbonnement.Calculer(abonnement, Aujourdhui);
        var proprietaire = await ProprietaireAsync(id, suivi: false, annulation);
        var nombre = await contexte.Users.CountAsync(c => c.CentreId == id, annulation);
        var paiements = await contexte.Set<PaiementDAbonnement>().AsNoTracking()
            .Where(p => p.CentreId == id)
            .OrderByDescending(p => p.Au)
            .ThenByDescending(p => p.SaisiLe)
            .ToListAsync(annulation);
        var envois = await contexte.Set<EnvoiAuProprietaire>().AsNoTracking()
            .Where(e => e.CentreId == id)
            .OrderByDescending(e => e.Quand)
            .ToListAsync(annulation);

        return new FicheDuCentre(
            centre.Id,
            centre.Nom,
            centre.Code,
            situation.Etat.ToString(),
            situation.PayeJusquAu,
            situation.FinDeLaPeriodeDeGrace,
            situation.SuspenduLe,
            abonnement?.Forcage?.ToString(),
            abonnement?.MotifDuForcage,
            abonnement?.ForceLe,
            abonnement?.PrixMensuel ?? PrixDeLOffre,
            nombre,
            proprietaire is null
                ? null
                : new ProprietaireDuCentre(proprietaire.Prenom, proprietaire.Nom, proprietaire.Email!, proprietaire.PhoneNumber, proprietaire.Etat.ToString()),
            paiements.Select(p => new PaiementVu(
                p.Id,
                p.Date,
                p.Montant,
                p.Mode.ToString(),
                p.Du,
                p.Au,
                p.NumeroDeFacture,
                p.SaisiLe,
                p.SaisiPar,
                envois.Where(e => e.PaiementId == p.Id).Select(EnvoiVu.De).ToList())).ToList(),
            envois.Where(e => e.Objet == ObjetDeLEnvoi.Invitation).Select(EnvoiVu.De).ToList(),
            envois.Where(e => e.Objet is ObjetDeLEnvoi.Relance or ObjetDeLEnvoi.RelanceAvantLaFin or ObjetDeLEnvoi.RelanceDeLaFin)
                .Select(EnvoiVu.De).ToList(),
            situation.ReactiveJusquAu,
            situation.ARelancer,
            situation.ASupprimer,
            situation.SuppressionPossibleLe,
            abonnement is not null && abonnement.SupportOuvert(temps.GetUtcNow()) ? abonnement.SupportAutoriseJusquAu : null);
    }

    // ----- EDI-01 : créer un centre, son prix -----

    /// <summary>
    /// Crée le centre (En attente), avec ses niveaux d'office, son premier propriétaire invité et son abonnement,
    /// au prix de l'offre ou à celui donné. L'invitation ne part pas encore : elle attend le premier paiement (EDI-03).
    /// </summary>
    public async Task<Issue<FicheDuCentre>> CreerAsync(Compte editeur, DemandeDeCentre demande, CancellationToken annulation = default)
    {
        var nom = demande.Nom?.Trim() ?? "";
        var code = demande.Code?.Trim() ?? "";
        var prenom = demande.PrenomDuProprietaire?.Trim() ?? "";
        var nomDuProprietaire = demande.NomDuProprietaire?.Trim() ?? "";
        var email = demande.EmailDuProprietaire?.Trim() ?? "";
        var saisieDuTelephone = demande.TelephoneDuProprietaire?.Trim() ?? "";
        if (new[] { nom, code, prenom, nomDuProprietaire, email, saisieDuTelephone }.Any(v => v.Length == 0))
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, ChampsDuCentreObligatoires);

        if (prenom.Length > Compte.LongueurMaximaleDuNom || nomDuProprietaire.Length > Compte.LongueurMaximaleDuNom)
            return Issue<FicheDuCentre>.Refusee(
                StatusCodes.Status400BadRequest,
                $"Le prénom et le nom du propriétaire font au plus {Compte.LongueurMaximaleDuNom} caractères");
        if (TelephoneTunisien.Normaliser(saisieDuTelephone) is not { } telephone)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, TelephonePasValable);
        if (!AdresseEmail.EstValable(email))
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, EmailPasValable);

        var prix = demande.PrixMensuel ?? PrixDeLOffre;
        if (!MontantValable(prix))
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, PrixPasValable);

        // Le code (format, déjà pris) et l'email déjà utilisé sont vérifiés par la création, comme pour la commande.
        var resultat = await creation.CreerAsync(
            new NouveauCentre(nom, code, prenom, nomDuProprietaire, email, telephone),
            envoyerLInvitation: false,
            completer: centre =>
            {
                contexte.Set<Abonnement>().Add(new Abonnement(centre.Id, prix));
                journal.Noter(
                    editeur,
                    ActionDeLEditeur.CentreCree,
                    centre.Id,
                    centre.Nom,
                    $"Propriétaire {prenom} {nomDuProprietaire} · {email} · {MessagesAuProprietaire.Montant(prix)} par mois");
                return Task.CompletedTask;
            },
            annulation);

        if (!resultat.Reussi)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, resultat.Message);

        return Issue<FicheDuCentre>.Reussie((await FicheAsync(resultat.Centre!.Id, annulation))!);
    }

    /// <summary>Le prix mensuel de ce centre, par exemple 90,000 → 75,000 TND pour une remise négociée.</summary>
    public async Task<Issue<FicheDuCentre>> ChangerLePrixAsync(Compte editeur, Guid id, PrixDuCentre saisie, CancellationToken annulation = default)
    {
        if (saisie.PrixMensuel is not { } prix || !MontantValable(prix))
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, PrixPasValable);

        var (centre, abonnement) = await CentreEtAbonnementAsync(id, annulation);
        if (centre is null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status404NotFound, Introuvable);

        var avant = abonnement!.PrixMensuel;
        abonnement.ChangerLePrix(prix);
        journal.Noter(
            editeur,
            ActionDeLEditeur.PrixChange,
            centre.Id,
            centre.Nom,
            $"{MessagesAuProprietaire.Montant(avant)} → {MessagesAuProprietaire.Montant(prix)} par mois");
        await contexte.SaveChangesAsync(annulation);
        return await FicheEnIssueAsync(id, annulation);
    }

    // ----- EDI-02 et EDI-04 : un paiement d'abonnement et sa confirmation -----

    /// <summary>
    /// Enregistre le paiement : le centre devient Actif jusqu'à la fin de sa période (et tout état posé à la main tombe).
    /// Puis la confirmation part toute seule au propriétaire ; si elle ne part pas, le paiement reste, et l'Éditeur la renvoie.
    /// </summary>
    public async Task<Issue<PaiementEnregistre>> SaisirUnPaiementAsync(
        Compte editeur,
        Guid id,
        SaisieDePaiement saisie,
        CancellationToken annulation = default)
    {
        var facture = saisie.NumeroDeFacture?.Trim() ?? "";
        if (saisie is not { Date: { } date, Montant: { } montant, Du: { } du, Au: { } au }
            || facture.Length == 0
            || !Enum.GetNames<ModeDePaiement>().Contains(saisie.Mode))
            return Issue<PaiementEnregistre>.Refusee(StatusCodes.Status400BadRequest, ChampsDuPaiementObligatoires);
        if (!MontantValable(montant))
            return Issue<PaiementEnregistre>.Refusee(StatusCodes.Status400BadRequest, MontantPasValable);
        if (au < du)
            return Issue<PaiementEnregistre>.Refusee(StatusCodes.Status400BadRequest, PeriodePasValable);
        if (facture.Length > PaiementDAbonnement.LongueurMaximaleDuNumeroDeFacture)
            return Issue<PaiementEnregistre>.Refusee(
                StatusCodes.Status400BadRequest,
                $"Le n° de facture fait au plus {PaiementDAbonnement.LongueurMaximaleDuNumeroDeFacture} caractères");

        var (centre, abonnement) = await CentreEtAbonnementAsync(id, annulation);
        if (centre is null)
            return Issue<PaiementEnregistre>.Refusee(StatusCodes.Status404NotFound, Introuvable);

        var paiement = new PaiementDAbonnement(
            id,
            date,
            montant,
            Enum.Parse<ModeDePaiement>(saisie.Mode!),
            du,
            au,
            facture,
            temps.GetUtcNow(),
            $"{editeur.Prenom} {editeur.Nom}");
        contexte.Set<PaiementDAbonnement>().Add(paiement);
        abonnement!.Prolonger(au);
        journal.Noter(editeur, ActionDeLEditeur.PaiementSaisi, centre.Id, centre.Nom, MessagesAuProprietaire.ConfirmationDePaiement(paiement));
        await contexte.SaveChangesAsync(annulation);

        var envoye = await EnvoyerLaConfirmationAsync(centre, paiement, annulation);
        var fiche = (await FicheAsync(id, annulation))!;
        return Issue<PaiementEnregistre>.Reussie(new PaiementEnregistre(
            fiche,
            envoye,
            envoye
                ? $"Paiement enregistré. La confirmation est partie à {fiche.Proprietaire?.Email}."
                : "Paiement enregistré, mais la confirmation n'est pas partie : renvoyez-la depuis l'historique des paiements."));
    }

    /// <summary>Renvoie la confirmation d'un paiement par email (EDI-04).</summary>
    public async Task<Issue<FicheDuCentre>> ConfirmerParEmailAsync(Guid id, Guid paiementId, CancellationToken annulation = default)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        var paiement = await contexte.Set<PaiementDAbonnement>().AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == paiementId && p.CentreId == id, annulation);
        if (centre is null || paiement is null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status404NotFound, PaiementIntrouvable);

        return await EnvoyerLaConfirmationAsync(centre, paiement, annulation)
            ? await FicheEnIssueAsync(id, annulation)
            : Issue<FicheDuCentre>.Refusee(StatusCodes.Status503ServiceUnavailable, EmailPasParti);
    }

    /// <summary>Le même message de confirmation, pré-écrit dans WhatsApp ; la date du clic est notée dans le paiement.</summary>
    public async Task<Issue<LienWhatsApp>> ConfirmerParWhatsAppAsync(Guid id, Guid paiementId, CancellationToken annulation = default)
    {
        var paiement = await contexte.Set<PaiementDAbonnement>().AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == paiementId && p.CentreId == id, annulation);
        if (paiement is null)
            return Issue<LienWhatsApp>.Refusee(StatusCodes.Status404NotFound, PaiementIntrouvable);

        return await WhatsAppAsync(
            id,
            ObjetDeLEnvoi.ConfirmationDePaiement,
            (_, _) => Task.FromResult(MessagesAuProprietaire.ConfirmationDePaiement(paiement)),
            paiementId,
            exigerUnPaiement: false,
            annulation);
    }

    // ----- EDI-03 : l'invitation du propriétaire -----

    /// <summary>
    /// Envoie l'invitation par email, après le premier paiement, tant que le propriétaire n'a pas choisi son mot de passe.
    /// Elle se renvoie autant de fois qu'il le faut ; chaque envoi est daté. Les liens déjà envoyés restent valables.
    /// </summary>
    public async Task<Issue<FicheDuCentre>> InviterParEmailAsync(Guid id, CancellationToken annulation = default)
    {
        var (centre, proprietaire, refus) = await PreparerLInvitationAsync(id, annulation);
        if (refus is not null)
            return Issue<FicheDuCentre>.Refusee(refus.Value.Statut, refus.Value.Message);

        try
        {
            await invitations.EnvoyerAsync(proprietaire!, centre!, annulation);
        }
        catch (Exception exception)
        {
            journaux.LogError(exception, "L'invitation du propriétaire du centre {Centre} n'est pas partie", id);
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status503ServiceUnavailable, EmailPasParti);
        }

        contexte.Set<EnvoiAuProprietaire>().Add(new EnvoiAuProprietaire(
            id, ObjetDeLEnvoi.Invitation, CanalDEnvoi.Email, temps.GetUtcNow(), proprietaire!.Email!));
        await contexte.SaveChangesAsync(annulation);
        return await FicheEnIssueAsync(id, annulation);
    }

    /// <summary>Le lien WhatsApp de l'invitation : le message et un lien d'invitation neuf, au numéro du propriétaire.</summary>
    public async Task<Issue<LienWhatsApp>> InviterParWhatsAppAsync(Guid id, CancellationToken annulation = default)
    {
        var (_, _, refus) = await PreparerLInvitationAsync(id, annulation);
        if (refus is not null)
            return Issue<LienWhatsApp>.Refusee(refus.Value.Statut, refus.Value.Message);

        return await WhatsAppAsync(
            id,
            ObjetDeLEnvoi.Invitation,
            async (centre, proprietaire) =>
                MessagesAuProprietaire.Invitation(proprietaire.Prenom, centre.Nom, await invitations.LienAsync(proprietaire)),
            paiementId: null,
            exigerUnPaiement: true,
            annulation);
    }

    /// <summary>
    /// Corrige le premier propriétaire avant de renvoyer l'invitation : son prénom, son nom, son téléphone, et son email
    /// tant qu'il n'a pas choisi son mot de passe. Un nouvel email rend caducs les liens envoyés à l'ancien.
    /// </summary>
    public async Task<Issue<FicheDuCentre>> CorrigerLeProprietaireAsync(
        Compte editeur,
        Guid id,
        CorrectionDuProprietaire correction,
        CancellationToken annulation = default)
    {
        var prenom = correction.Prenom?.Trim() ?? "";
        var nom = correction.Nom?.Trim() ?? "";
        var email = correction.Email?.Trim() ?? "";
        var saisieDuTelephone = correction.Telephone?.Trim() ?? "";
        if (prenom.Length == 0 || nom.Length == 0 || email.Length == 0 || saisieDuTelephone.Length == 0)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, "Le prénom, le nom, l'email et le téléphone du propriétaire sont obligatoires");
        if (prenom.Length > Compte.LongueurMaximaleDuNom || nom.Length > Compte.LongueurMaximaleDuNom)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, $"Le prénom et le nom font au plus {Compte.LongueurMaximaleDuNom} caractères");
        if (TelephoneTunisien.Normaliser(saisieDuTelephone) is not { } telephone)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, TelephonePasValable);
        if (!AdresseEmail.EstValable(email))
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, EmailPasValable);

        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        var proprietaire = centre is null ? null : await ProprietaireAsync(id, suivi: true, annulation);
        if (centre is null || proprietaire is null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status404NotFound, Introuvable);

        var nouvelEmail = !string.Equals(email, proprietaire.Email, StringComparison.OrdinalIgnoreCase);
        if (nouvelEmail && proprietaire.Etat != EtatDuCompte.Invite)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, EmailFigeApresLInscription);
        if (nouvelEmail && await comptes.FindByEmailAsync(email) is not null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, EmailDejaUtilise);

        var avant = $"{proprietaire.Prenom} {proprietaire.Nom} · {proprietaire.Email} · {proprietaire.PhoneNumber}";
        proprietaire.Renommer(prenom, nom);
        proprietaire.PhoneNumber = telephone;
        proprietaire.Email = proprietaire.UserName = email;
        await comptes.UpdateNormalizedEmailAsync(proprietaire);
        await comptes.UpdateNormalizedUserNameAsync(proprietaire);
        journal.Noter(editeur, ActionDeLEditeur.ProprietaireCorrige, centre.Id, centre.Nom, $"{avant} → {prenom} {nom} · {email} · {telephone}");

        // Le tampon change avec un nouvel email : les liens d'invitation partis à l'ancien ne servent plus.
        IdentityResult resultat;
        try
        {
            resultat = nouvelEmail ? await comptes.UpdateSecurityStampAsync(proprietaire) : await comptes.UpdateAsync(proprietaire);
        }
        catch (DbUpdateException)
        {
            // Deux corrections ensemble vers le même email : l'index unique refuse la seconde.
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, EmailDejaUtilise);
        }

        if (!resultat.Succeeded)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, "Le propriétaire vient de changer : rechargez la page");
        return await FicheEnIssueAsync(id, annulation);
    }

    // ----- EDI-05 : suspendre et réactiver à la main -----

    public async Task<Issue<FicheDuCentre>> SuspendreAsync(Compte editeur, Guid id, Motif motif, CancellationToken annulation = default)
    {
        if (RefusDuMotif(motif) is { } refus)
            return refus;
        var texte = motif.Texte!.Trim();

        var (centre, abonnement) = await CentreEtAbonnementAsync(id, annulation);
        if (centre is null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status404NotFound, Introuvable);
        if (SituationDeLAbonnement.Calculer(abonnement, Aujourdhui).Etat == EtatDeLAbonnement.Suspendu)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, DejaSuspendu);

        abonnement!.Forcer(ForcageDeLEtat.Suspendu, texte, Aujourdhui);
        journal.Noter(editeur, ActionDeLEditeur.CentreSuspendu, centre.Id, centre.Nom, $"Suspendu à la main · {texte}");
        await contexte.SaveChangesAsync(annulation);
        return await FicheEnIssueAsync(id, annulation);
    }

    /// <summary>
    /// Réactive un centre Suspendu. Une suspension posée à la main est levée ; si la fin payée (période de grâce comprise)
    /// est dépassée, le centre est réactivé à la main pour 15 jours (le jour de la réactivation + 15 jours inclus), puis
    /// repasse Suspendu sans paiement. Un paiement le rend Actif selon sa période.
    /// </summary>
    public async Task<Issue<FicheDuCentre>> ReactiverAsync(Compte editeur, Guid id, Motif motif, CancellationToken annulation = default)
    {
        if (RefusDuMotif(motif) is { } refus)
            return refus;
        var texte = motif.Texte!.Trim();

        var (centre, abonnement) = await CentreEtAbonnementAsync(id, annulation);
        if (centre is null)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status404NotFound, Introuvable);
        var aujourdhui = Aujourdhui;
        if (SituationDeLAbonnement.Calculer(abonnement, aujourdhui).Etat != EtatDeLAbonnement.Suspendu)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status409Conflict, PasSuspendu);

        var sansForcage = SituationDeLAbonnement.Calculer(abonnement!.PayeJusquAu, null, null, aujourdhui).Etat;
        abonnement.Forcer(sansForcage == EtatDeLAbonnement.Suspendu ? ForcageDeLEtat.Actif : null, texte, aujourdhui);
        journal.Noter(editeur, ActionDeLEditeur.CentreReactive, centre.Id, centre.Nom, $"Réactivé à la main · {texte}");
        await contexte.SaveChangesAsync(annulation);
        return await FicheEnIssueAsync(id, annulation);
    }

    // ----- EDI-06 : relancer -----

    /// <summary>Le message de relance pré-écrit dans WhatsApp, depuis la liste « à relancer » ; la date du clic est notée.</summary>
    public async Task<Issue<LienWhatsApp>> RelancerParWhatsAppAsync(Guid id, CancellationToken annulation = default)
    {
        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == id, annulation);
        if (abonnement?.PayeJusquAu is not { } fin)
            return await contexte.Centres.AnyAsync(c => c.Id == id, annulation)
                ? Issue<LienWhatsApp>.Refusee(StatusCodes.Status409Conflict, RienARelancer)
                : Issue<LienWhatsApp>.Refusee(StatusCodes.Status404NotFound, Introuvable);

        return await WhatsAppAsync(
            id,
            ObjetDeLEnvoi.Relance,
            (centre, proprietaire) => Task.FromResult(MessagesAuProprietaire.Relance(proprietaire.Prenom, centre.Nom, fin, abonnement.PrixMensuel)),
            paiementId: null,
            exigerUnPaiement: false,
            annulation,
            echeance: fin);
    }

    // ----- EDI-08 : supprimer un centre parti -----

    /// <summary>
    /// Supprime un centre Suspendu depuis 12 mois, quand l'Éditeur retape exactement son nom : le centre, ses comptes, son
    /// abonnement, ses paiements, son journal et toutes ses données. Rien n'est supprimé automatiquement.
    /// Une ligne reste au journal de l'Éditeur.
    /// </summary>
    public async Task<Issue<Avis>> SupprimerAsync(Compte editeur, Guid id, ConfirmationDeSuppression confirmation, CancellationToken annulation = default)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        if (centre is null)
            return Issue<Avis>.Refusee(StatusCodes.Status404NotFound, Introuvable);

        var abonnement = await contexte.Set<Abonnement>().AsNoTracking().SingleOrDefaultAsync(a => a.CentreId == id, annulation);
        var situation = SituationDeLAbonnement.Calculer(abonnement, Aujourdhui);
        if (!situation.ASupprimer)
            return Issue<Avis>.Refusee(
                StatusCodes.Status409Conflict,
                situation.SuppressionPossibleLe is { } le
                    ? $"Ce centre ne pourra être supprimé qu'à partir du {MessagesAuProprietaire.Date(le)}, 12 mois après sa suspension"
                    : "Seul un centre Suspendu depuis 12 mois peut être supprimé");
        if (!string.Equals(confirmation.Nom?.Trim(), centre.Nom, StringComparison.Ordinal))
            return Issue<Avis>.Refusee(StatusCodes.Status400BadRequest, NomARetaper);

        await SuppressionDUnCentre.SupprimerAsync(contexte, id, annulation, avantDeValider: async () =>
        {
            journal.Noter(
                editeur,
                ActionDeLEditeur.CentreSupprime,
                id,
                centre.Nom,
                $"Données supprimées · Suspendu le {MessagesAuProprietaire.Date(situation.SuspenduLe!.Value)}");
            await contexte.SaveChangesAsync(annulation);
        });
        return Issue<Avis>.Reussie(new Avis($"Le {centre.Nom} et toutes ses données sont supprimés."));
    }

    public Task<List<LigneDuJournalVue>> JournalAsync(CancellationToken annulation = default) =>
        contexte.Set<LigneDuJournalDeLEditeur>().AsNoTracking()
            .OrderByDescending(l => l.Quand)
            .Take(500)
            .Select(l => new LigneDuJournalVue(l.Quand, l.Auteur, l.Action.ToString(), l.Centre, l.Detail))
            .ToListAsync(annulation);

    // ----- Outils -----

    /// <summary>Un montant en dinars supérieur à 0, au millime près, qui tient dans la colonne (12 chiffres dont 3 décimales).</summary>
    private static bool MontantValable(decimal montant) =>
        montant > 0 && montant < 1_000_000_000m && decimal.Round(montant, 3) == montant;

    /// <summary>Le refus d'un motif vide ou trop long, ou <c>null</c> s'il est bon.</summary>
    private static Issue<FicheDuCentre>? RefusDuMotif(Motif motif)
    {
        var texte = motif.Texte?.Trim() ?? "";
        if (texte.Length == 0)
            return Issue<FicheDuCentre>.Refusee(StatusCodes.Status400BadRequest, MotifObligatoire);
        if (texte.Length > Abonnement.LongueurMaximaleDuMotif)
            return Issue<FicheDuCentre>.Refusee(
                StatusCodes.Status400BadRequest,
                $"Le motif fait au plus {Abonnement.LongueurMaximaleDuMotif} caractères");
        return null;
    }

    private async Task<Issue<FicheDuCentre>> FicheEnIssueAsync(Guid id, CancellationToken annulation) =>
        Issue<FicheDuCentre>.Reussie((await FicheAsync(id, annulation))!);

    /// <summary>Le centre et son abonnement, suivi ; un centre ouvert avant le back office en reçoit un, au prix de l'offre.</summary>
    private async Task<(Centre? Centre, Abonnement? Abonnement)> CentreEtAbonnementAsync(Guid id, CancellationToken annulation)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        if (centre is null)
            return (null, null);

        var abonnement = await contexte.Set<Abonnement>().SingleOrDefaultAsync(a => a.CentreId == id, annulation);
        if (abonnement is null)
        {
            abonnement = new Abonnement(id, PrixDeLOffre);
            contexte.Set<Abonnement>().Add(abonnement);
        }

        return (centre, abonnement);
    }

    /// <summary>
    /// Le premier propriétaire du centre : celui créé avec lui. Les identifiants des comptes suivent l'ordre de création
    /// (UUID version 7), le plus petit est donc le premier.
    /// </summary>
    private Task<Compte?> ProprietaireAsync(Guid centreId, bool suivi, CancellationToken annulation)
    {
        var requete = contexte.Users
            .Where(c => c.CentreId == centreId && c.Role == RoleDuCompte.Proprietaire)
            .OrderBy(c => c.Id);
        return (suivi ? requete : requete.AsNoTracking()).FirstOrDefaultAsync(annulation);
    }

    private async Task<(Centre? Centre, Compte? Proprietaire, (int Statut, string Message)? Refus)> PreparerLInvitationAsync(
        Guid id,
        CancellationToken annulation)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        var proprietaire = centre is null ? null : await ProprietaireAsync(id, suivi: true, annulation);
        if (centre is null || proprietaire is null)
            return (null, null, (StatusCodes.Status404NotFound, Introuvable));
        if (!await contexte.Set<PaiementDAbonnement>().AnyAsync(p => p.CentreId == id, annulation))
            return (null, null, (StatusCodes.Status409Conflict, InvitationAvantLePaiement));
        if (proprietaire.Etat != EtatDuCompte.Invite)
            return (null, null, (StatusCodes.Status409Conflict, ProprietaireDejaInscrit));
        return (centre, proprietaire, null);
    }

    /// <summary>Envoie la confirmation d'un paiement par email et note l'envoi ; <c>false</c> si l'email n'est pas parti.</summary>
    private async Task<bool> EnvoyerLaConfirmationAsync(Centre centre, PaiementDAbonnement paiement, CancellationToken annulation)
    {
        var proprietaire = await ProprietaireAsync(centre.Id, suivi: false, annulation);
        if (proprietaire is null)
            return false;

        var (sujet, texte) = MessagesAuProprietaire.EmailDeConfirmation(proprietaire.Prenom, centre.Nom, paiement);
        try
        {
            await emails.EnvoyerAsync(proprietaire.Email!, sujet, texte, annulation);
        }
        catch (Exception exception)
        {
            journaux.LogError(exception, "La confirmation du paiement {Paiement} n'est pas partie", paiement.Id);
            return false;
        }

        contexte.Set<EnvoiAuProprietaire>().Add(new EnvoiAuProprietaire(
            centre.Id, ObjetDeLEnvoi.ConfirmationDePaiement, CanalDEnvoi.Email, temps.GetUtcNow(), proprietaire.Email!, paiement.Id));
        await contexte.SaveChangesAsync(annulation);
        return true;
    }

    /// <summary>
    /// Fabrique le lien WhatsApp d'un message au propriétaire et note la date du clic. Le navigateur de l'Éditeur ouvre
    /// ensuite WhatsApp : SchoolUp ne sait pas si le message est vraiment parti, seulement que le bouton a servi.
    /// </summary>
    private async Task<Issue<LienWhatsApp>> WhatsAppAsync(
        Guid id,
        ObjetDeLEnvoi objet,
        Func<Centre, Compte, Task<string>> message,
        Guid? paiementId,
        bool exigerUnPaiement,
        CancellationToken annulation,
        DateOnly? echeance = null)
    {
        var centre = await contexte.Centres.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, annulation);
        var proprietaire = centre is null ? null : await ProprietaireAsync(id, suivi: false, annulation);
        if (centre is null || proprietaire is null)
            return Issue<LienWhatsApp>.Refusee(StatusCodes.Status404NotFound, Introuvable);
        if (exigerUnPaiement && !await contexte.Set<PaiementDAbonnement>().AnyAsync(p => p.CentreId == id, annulation))
            return Issue<LienWhatsApp>.Refusee(StatusCodes.Status409Conflict, InvitationAvantLePaiement);
        if (proprietaire.PhoneNumber is null || TelephoneTunisien.Normaliser(proprietaire.PhoneNumber) is not { } telephone)
            return Issue<LienWhatsApp>.Refusee(StatusCodes.Status409Conflict, SansTelephone);

        var lien = WhatsApp.Lien(telephone, await message(centre, proprietaire));
        contexte.Set<EnvoiAuProprietaire>().Add(new EnvoiAuProprietaire(
            id, objet, CanalDEnvoi.WhatsApp, temps.GetUtcNow(), telephone, paiementId, echeance));
        await contexte.SaveChangesAsync(annulation);
        return Issue<LienWhatsApp>.Reussie(new LienWhatsApp(lien, (await FicheAsync(id, annulation))!));
    }
}
