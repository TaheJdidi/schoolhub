using Microsoft.EntityFrameworkCore;
using SchoolHub.Modules.Eleves;
using SchoolHub.Modules.Inscriptions;
using SchoolHub.Socle.Comptes;
using SchoolHub.Socle.Dates;

namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-04 : l'encaissement groupé. Depuis un groupe, chaque élève coché paie tout ce qu'il doit à ce jour dans ce groupe
/// (assurance comprise si elle est due) et reçoit son propre reçu, numéroté à la suite. Tout ou rien.
/// </summary>
public sealed partial class RecusDuCentre
{
    public const string GroupeIntrouvable = "Groupe introuvable";
    private const string AucunEleve = "Cochez au moins un élève";
    private const string TropDEleves = "Cochez au plus 100 élèves";
    private const string APayeEntreTemps = "Un élève coché a payé entre-temps : rechargez la page";
    private const string MontantPartielPasValable = "Écrivez un montant de plus de 0, au plus ce que l'élève doit";
    private const string AssuranceEnEntier = "L'assurance se paie en entier : donnez au moins son montant";

    /// <summary>Le nombre d'élèves d'un encaissement groupé : un groupe, jamais tout le centre.</summary>
    private const int ElevesAuPlus = 100;

    /// <summary>
    /// CAI-04 : les élèves encore inscrits dans le groupe aujourd'hui, par nom puis prénom : ceux qui doivent quelque chose
    /// à ce jour (ou à la date <paramref name="au"/>, celle d'une séance à venir), avec leurs sommes, et ceux qui sont à
    /// jour. <c>null</c> si le groupe n'est pas dans ce centre.
    /// </summary>
    public async Task<EncaissementDuGroupe?> EncaissementDuGroupeAsync(Guid groupeId, DateOnly? au = null)
    {
        var groupe = await contexte.Groupes.SingleOrDefaultAsync(g => g.Id == groupeId);
        if (groupe is null)
            return null;
        var matiere = await contexte.Matieres.Where(m => m.Id == groupe.MatiereId).Select(m => m.Nom).SingleAsync();
        var eleves = await ElevesDuGroupeAsync(groupeId);
        var situations = await inscriptions.SituationsAsync(eleves.Select(e => e.Eleve.Id).ToList());

        var aPayer = new List<EleveAEncaisser>();
        var aJour = new List<EleveDuRecu>();
        foreach (var (eleve, inscriptionId) in eleves)
        {
            var sommes = await DuesDansLeGroupeAsync(eleve.Id, inscriptionId, au);
            if (sommes.Count == 0)
            {
                aJour.Add(new EleveDuRecu(eleve.Id, eleve.Prenom, eleve.Nom));
                continue;
            }

            // À jour aujourd'hui mais redevable à la séance à venir : à payer, sans retard.
            var couleur = situations[eleve.Id].Couleur.ToString();
            if (couleur == "AJour")
                couleur = "APayer";
            aPayer.Add(new EleveAEncaisser(eleve.Id, eleve.Prenom, eleve.Nom, couleur,
                sommes.Select(s => new SommeDuGroupe(s.InscriptionId, s.Cle, s.Libelle, s.Montant, s.Avance)).ToList(), sommes.Sum(s => s.Montant)));
        }

        return new EncaissementDuGroupe(new GroupeEncaisse(groupe.Id, groupe.Nom, matiere, groupe.Type.ToString()), aPayer, aJour);
    }

    /// <summary>
    /// CAI-04 : encaisse en espèces chaque élève coché, avec les sommes que la page a montrées : un reçu par élève, à la
    /// suite, dans l'ordre de la page. Si une somme n'est plus à payer, rien n'est enregistré. Les reçus partent ensuite
    /// par email, chacun à ses destinataires qui en ont un.
    /// </summary>
    public async Task<IssueDeLEncaissementGroupe> EncaisserLeGroupeAsync(Compte auteur, Guid groupeId, EncaissementGroupeSaisi saisie)
    {
        var coches = (saisie.Eleves ?? []).Where(e => e?.EleveId is not null).Select(e => e!).ToList();
        if (coches.Count == 0)
            return RefusDuGroupe(StatusCodes.Status400BadRequest, AucunEleve);
        if (coches.Count > ElevesAuPlus)
            return RefusDuGroupe(StatusCodes.Status400BadRequest, TropDEleves);
        // Un élève coché deux fois paierait deux fois la même somme : la seconde est refusée comme « déjà payée ».
        if (coches.Select(e => e.EleveId).Distinct().Count() != coches.Count)
            return RefusDuGroupe(StatusCodes.Status409Conflict, APayeEntreTemps);

        var recus = new List<Recu>();
        await using (var transaction = await contexte.Database.BeginTransactionAsync())
        {
            if (!await contexte.Groupes.AnyAsync(g => g.Id == groupeId))
                return RefusDuGroupe(StatusCodes.Status404NotFound, GroupeIntrouvable);
            // Les élèves dans l'ordre de leur identifiant, comme partout : pas d'interblocage avec un autre encaissement.
            var ids = coches.Select(e => e.EleveId!.Value).Order().ToArray();
            await contexte.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM eleves WHERE id = ANY({ids}) AND centre_id = {CentreId} ORDER BY id FOR NO KEY UPDATE");

            var duGroupe = (await ElevesDuGroupeAsync(groupeId)).ToDictionary(e => e.Eleve.Id);
            var aEncaisser = new List<(Eleve Eleve, List<SommeEncaissable> Sommes, ModeDePaiement Mode)>();
            foreach (var coche in coches)
            {
                if (!duGroupe.TryGetValue(coche.EleveId!.Value, out var inscrit))
                    return RefusDuGroupe(StatusCodes.Status409Conflict, APayeEntreTemps);
                var dues = await DuesDansLeGroupeAsync(inscrit.Eleve.Id, inscrit.InscriptionId, saisie.Au);
                var choisies = (coche.Sommes ?? []).Where(s => s is not null).Select(s => (s.InscriptionId, Cle: s.Cle?.Trim() ?? "")).Distinct().ToList();
                var sommes = choisies.Select(c => dues.FirstOrDefault(s => s.InscriptionId == c.InscriptionId && s.Cle == c.Cle)).ToList();
                if (sommes.Count == 0 || sommes.Any(s => s is null))
                    return RefusDuGroupe(StatusCodes.Status409Conflict, APayeEntreTemps);
                if (LireLeMode(coche.Mode) is not { } mode)
                    return RefusDuGroupe(StatusCodes.Status400BadRequest, ModeInconnu);
                // Dans l'ordre où elles sont dues, comme la page les montre.
                var aRegler = sommes.OfType<SommeEncaissable>().OrderBy(dues.IndexOf).ToList();
                if (coche.Montant is { } donne)
                {
                    if (donne <= 0 || decimal.Round(donne, 3) != donne || donne > aRegler.Sum(s => s.Montant))
                        return RefusDuGroupe(StatusCodes.Status400BadRequest, MontantPartielPasValable);
                    if (await EnPartieAsync(aRegler, donne) is not { } enPartie)
                        return RefusDuGroupe(StatusCodes.Status400BadRequest, AssuranceEnEntier);
                    aRegler = enPartie;
                }

                aEncaisser.Add((inscrit.Eleve, aRegler, mode));
            }

            var numero = await NumeroSuivantAsync();
            foreach (var (eleve, sommes, mode) in aEncaisser.OrderBy(e => e.Eleve.Nom).ThenBy(e => e.Eleve.Prenom).ThenBy(e => e.Eleve.Id))
                recus.Add(CreerLeRecu(auteur, eleve, sommes, numero++, mode));
            await contexte.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var decrits = new List<RecuDecrit>();
        foreach (var recu in recus)
        {
            await EnvoyerParEmailAsync(recu);
            decrits.Add(await DecrireAsync(recu, auteur));
        }

        return new IssueDeLEncaissementGroupe(new RecusDuGroupe(decrits), StatusCodes.Status201Created, null);
    }

    /// <summary>
    /// Ce que paie un élève qui donne une partie seulement : l'assurance d'abord, en entier ; puis les sommes du groupe, dans
    /// l'ordre, tant que ce qu'il donne les couvre ; la suivante en partie, comme une avance (« Mois 3 (avance) »), dont le
    /// reste reste à payer. <c>null</c> s'il donne moins que l'assurance.
    /// </summary>
    private async Task<List<SommeEncaissable>?> EnPartieAsync(List<SommeEncaissable> sommes, decimal donne)
    {
        var payees = new List<SommeEncaissable>();
        foreach (var somme in sommes.OrderBy(s => s.InscriptionId is not null))
        {
            if (donne <= 0)
                break;
            if (donne >= somme.Montant)
            {
                payees.Add(somme);
                donne -= somme.Montant;
                continue;
            }

            if (somme.InscriptionId is not { } inscriptionId)
                return null;
            // Le numéro qui suit la dernière avance de cette somme : une avance annulée ne laisse pas de trou gênant.
            var avances = await contexte.Reglements
                .Where(r => r.InscriptionId == inscriptionId && r.Cle.StartsWith(somme.Cle + " (avance "))
                .Select(r => r.Cle).ToListAsync();
            var numero = avances.Select(Echeancier.NumeroDAvance).DefaultIfEmpty(0).Max() + 1;
            var libelle = somme.Libelle.EndsWith(SommeEncaissable.SuffixeDuReste, StringComparison.Ordinal)
                ? somme.Libelle[..^SommeEncaissable.SuffixeDuReste.Length]
                : somme.Libelle;
            payees.Add(somme with { Cle = Echeancier.CleDAvance(somme.Cle, numero), Libelle = $"{libelle} (avance)", Montant = donne, ReportDeduit = 0m });
            donne = 0;
        }

        return payees;
    }

    /// <summary>Les élèves encore inscrits dans le groupe aujourd'hui (pas arrêtés), avec leur inscription, par nom puis prénom.</summary>
    private async Task<List<(Eleve Eleve, Guid InscriptionId)>> ElevesDuGroupeAsync(Guid groupeId)
    {
        var aujourdhui = DateDeTunis.Aujourdhui(temps);
        return (await contexte.Inscriptions
                .Where(i => i.GroupeId == groupeId && (i.ArreteLe == null || i.ArreteLe >= aujourdhui))
                .Join(contexte.Eleves, i => i.EleveId, e => e.Id, (i, e) => new { Eleve = e, InscriptionId = i.Id })
                .ToListAsync())
            .OrderBy(x => x.Eleve.Nom).ThenBy(x => x.Eleve.Prenom).ThenBy(x => x.Eleve.Id)
            .Select(x => (x.Eleve, x.InscriptionId))
            .ToList();
    }

    /// <summary>
    /// Ce que l'élève doit dans ce groupe à ce jour, ou à la date <paramref name="au"/> : les sommes dues de cette
    /// inscription, puis son assurance si elle est due.
    /// </summary>
    private async Task<List<SommeEncaissable>> DuesDansLeGroupeAsync(Guid eleveId, Guid inscriptionId, DateOnly? au = null)
    {
        var limite = au ?? DateDeTunis.Aujourdhui(temps);
        return (await inscriptions.SommesEncaissablesAsync(eleveId))
            .Where(s => (s.InscriptionId == inscriptionId || s.AssuranceId is not null) && s.DueLe <= limite)
            .ToList();
    }

    private static IssueDeLEncaissementGroupe RefusDuGroupe(int statut, string message) => new(null, statut, message);
}

/// <summary>Ce que l'encaissement groupé a donné : les reçus et le statut HTTP, ou un refus (statut et message).</summary>
public sealed record IssueDeLEncaissementGroupe(RecusDuGroupe? Recus, int Statut, string? Message);
