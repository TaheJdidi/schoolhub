using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Groupes;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Centres;
using SchoolHub.Socle.Donnees;
using SchoolHub.Socle.Messages;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-08 : le rappel de paiement d'un élève orange ou rouge, prêt pour chacun de ses destinataires, avec ce qu'il doit
/// à ce jour. Ouvert, il est noté : « rappel envoyé le 12/10 ». Et « Élèves en rouge », pour l'accueil.
/// </summary>
public sealed class RappelsDePaiement(SchoolHubDbContext contexte, InscriptionsDuCentre inscriptions, CentreCourant centreCourant, TimeProvider temps)
{
    public const string EleveIntrouvable = "Élève introuvable";
    private const string RienAPayer = "Cet élève n'a rien à payer aujourd'hui";

    /// <summary>Le rappel de l'élève : son texte, un message par destinataire, la date du dernier rappel ; ou un refus.</summary>
    public async Task<IssueDuRappel> LireAsync(Guid eleveId)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
        if (eleve is null)
            return new IssueDuRappel(null, StatusCodes.Status404NotFound, EleveIntrouvable);
        var situation = (await inscriptions.SituationsAsync([eleveId]))[eleveId];
        if (situation.Dues.Count == 0)
            return new IssueDuRappel(null, StatusCodes.Status409Conflict, RienAPayer);

        var centre = await contexte.Centres.SingleAsync(c => c.Id == centreCourant.Id);
        var texte = Texte(eleve, situation.Dues, centre.Nom);
        return new IssueDuRappel(new RappelDePaiement(
            situation.Couleur.ToString(),
            texte,
            DestinatairesDeLEleve.De(eleve).Select(d => new MessageWhatsApp(d.Nom, TelephoneTunisien.Grouper(d.Chiffres), WhatsApp.Lien(d.Chiffres, texte))).ToList(),
            eleve.DernierRappelLe), StatusCodes.Status200OK, null);
    }

    /// <summary>Le rappel a été ouvert dans WhatsApp : sa date s'affiche, pour ne pas l'envoyer deux fois.</summary>
    public async Task<IssueDuRappel> NoterAsync(Guid eleveId)
    {
        var eleve = await contexte.Eleves.SingleOrDefaultAsync(e => e.Id == eleveId);
        if (eleve is null)
            return new IssueDuRappel(null, StatusCodes.Status404NotFound, EleveIntrouvable);
        eleve.NoterUnRappel(temps.GetUtcNow());
        await contexte.SaveChangesAsync();
        return new IssueDuRappel(null, StatusCodes.Status204NoContent, null);
    }

    /// <summary>
    /// SEA-01 et CAI-08 : les élèves en rouge, avec ce qu'ils doivent à ce jour et la date de leur dernier rappel, par
    /// nom puis prénom.
    /// </summary>
    public async Task<List<EleveEnRouge>> EnRougeAsync()
    {
        var ids = await contexte.Inscriptions.Select(i => i.EleveId).Distinct().ToListAsync();
        var situations = await inscriptions.SituationsAsync(ids);
        var rouges = situations.Where(s => s.Value.Couleur == Couleur.Rouge).Select(s => s.Key).ToList();
        var eleves = await contexte.Eleves.Where(e => rouges.Contains(e.Id)).OrderBy(e => e.Nom).ThenBy(e => e.Prenom).ToListAsync();
        return eleves.Select(e => new EleveEnRouge(e.Id, e.Prenom, e.Nom,
                situations[e.Id].Dues.Select(Decrire).ToList(), situations[e.Id].Dues.Sum(d => d.Montant), e.DernierRappelLe))
            .ToList();
    }

    /// <summary>
    /// « Bonjour, il reste à payer pour Adem Miled : Mois 3 de mathématiques, 40,000 TND. Centre Les Jasmins. » Plusieurs
    /// sommes se suivent, séparées par « ; », puis le total.
    /// </summary>
    internal static string Texte(Eleve eleve, IReadOnlyList<SommeDueAujourdhui> dues, string centre)
    {
        var sommes = string.Join(" ; ", dues.Select(d => $"{Decrire(d)}, {SaisieDuGroupe.EcrireUnMontant(d.Montant)} TND"));
        var total = dues.Count > 1 ? $" Total : {SaisieDuGroupe.EcrireUnMontant(dues.Sum(d => d.Montant))} TND." : "";
        return $"Bonjour, il reste à payer pour {eleve.Prenom} {eleve.Nom} : {sommes}.{total} {centre}.";
    }

    /// <summary>« Mois 3 de mathématiques », « Séance 2 d'informatique », « assurance 2027-2028 ».</summary>
    private static string Decrire(SommeDueAujourdhui due)
    {
        if (due.Matiere is not { } matiere)
            return char.ToLowerInvariant(due.Libelle[0]) + due.Libelle[1..];
        var enMinuscule = matiere.Length > 1 && char.IsLower(matiere[1]) ? char.ToLowerInvariant(matiere[0]) + matiere[1..] : matiere;
        return $"{due.Libelle} {Francais.De(enMinuscule)}";
    }
}

/// <summary>CAI-08 : la couleur de l'élève, le texte du rappel, un message par destinataire, et la date du dernier rappel.</summary>
public sealed record RappelDePaiement(string Couleur, string Texte, List<MessageWhatsApp> Messages, DateTimeOffset? DernierRappelLe);

/// <summary>Ce que la lecture ou la notation d'un rappel a donné : le rappel et le statut HTTP, ou un refus.</summary>
public sealed record IssueDuRappel(RappelDePaiement? Rappel, int Statut, string? Message);

/// <summary>Un élève en rouge, pour l'accueil : ce qu'il doit à ce jour (« Mois 3 de mathématiques »), le total, son dernier rappel.</summary>
public sealed record EleveEnRouge(Guid Id, string Prenom, string Nom, List<string> Dues, decimal Total, DateTimeOffset? DernierRappelLe);
