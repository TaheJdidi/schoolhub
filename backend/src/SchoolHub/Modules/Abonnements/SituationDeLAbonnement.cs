namespace SchoolHub.Modules.Abonnements;

/// <summary>Où en est l'abonnement d'un centre (CONTEXT.md : État de l'abonnement).</summary>
public enum EtatDeLAbonnement
{
    /// <summary>Aucun paiement d'abonnement encore saisi.</summary>
    EnAttente,
    /// <summary>Payé jusqu'à une date, incluse.</summary>
    Actif,
    /// <summary>La période de grâce : 15 jours après la fin, tout fonctionne encore, avec un bandeau rouge.</summary>
    EnRetard,
    /// <summary>Lecture seule : le centre consulte, mais ne crée et ne modifie plus rien.</summary>
    Suspendu,
}

/// <summary>
/// L'état de l'abonnement d'un centre un jour donné (EDI-05). Rien n'est enregistré : tout se calcule depuis la fin de la
/// période payée, l'état posé à la main, et la date du jour à Tunis.
/// </summary>
/// <param name="FinDeLaPeriodeDeGrace">Le dernier jour de la période de grâce, quand le centre est En retard.</param>
/// <param name="SuspenduLe">Le premier jour de la suspension, quand le centre est Suspendu.</param>
/// <param name="ReactiveJusquAu">
/// Le dernier jour (inclus) d'une réactivation à la main d'un centre impayé : le jour de la réactivation + 15 jours.
/// Le lendemain, sans paiement, le centre repasse Suspendu.
/// </param>
public sealed record SituationDeLAbonnement(
    EtatDeLAbonnement Etat,
    DateOnly? PayeJusquAu,
    DateOnly? FinDeLaPeriodeDeGrace,
    DateOnly? SuspenduLe,
    DateOnly? ReactiveJusquAu)
{
    /// <summary>La durée d'une réactivation à la main d'un centre impayé, en jours après le jour de la réactivation.</summary>
    public const int JoursDeReactivation = 15;

    /// <summary>La période de grâce, en jours après la fin payée.</summary>
    public const int JoursDeGrace = 15;

    public bool LectureSeule => Etat == EtatDeLAbonnement.Suspendu;

    /// <summary>
    /// La situation d'un abonnement (ou d'un centre sans abonnement enregistré, <c>null</c> : En attente) le jour donné.
    /// Exemple : payé jusqu'au 04/04, Actif jusqu'au 04/04 inclus, En retard du 05/04 au 19/04, Suspendu le 20/04.
    /// </summary>
    public static SituationDeLAbonnement Calculer(Abonnement? abonnement, DateOnly aujourdhui) =>
        Calculer(abonnement?.PayeJusquAu, abonnement?.Forcage, abonnement?.ForceLe, aujourdhui);

    public static SituationDeLAbonnement Calculer(
        DateOnly? payeJusquAu,
        ForcageDeLEtat? forcage,
        DateOnly? forceLe,
        DateOnly aujourdhui)
    {
        var etatCalcule = EtatSansForcage(payeJusquAu, aujourdhui);

        // Réactivé à la main sans paiement : Actif jusqu'au jour de la réactivation + 15 jours inclus, puis de nouveau Suspendu.
        var reactiveJusquAu = forcage == ForcageDeLEtat.Actif && forceLe is { } reactiveLe ? reactiveLe.AddDays(JoursDeReactivation) : (DateOnly?)null;
        var etat = forcage switch
        {
            ForcageDeLEtat.Suspendu => EtatDeLAbonnement.Suspendu,
            ForcageDeLEtat.Actif => reactiveJusquAu is { } dernierJour && aujourdhui > dernierJour ? EtatDeLAbonnement.Suspendu : EtatDeLAbonnement.Actif,
            _ => etatCalcule,
        };

        DateOnly? suspenduLe = null;
        if (etat == EtatDeLAbonnement.Suspendu && forcage == ForcageDeLEtat.Actif)
        {
            // La réactivation a expiré : la nouvelle suspension commence le lendemain de son dernier jour.
            suspenduLe = reactiveJusquAu!.Value.AddDays(1);
        }
        else if (etat == EtatDeLAbonnement.Suspendu)
        {
            // La suspension commence le premier des deux : celle posée à la main, ou celle de la fin de la période de grâce.
            var automatique = etatCalcule == EtatDeLAbonnement.Suspendu ? payeJusquAu!.Value.AddDays(JoursDeGrace + 1) : (DateOnly?)null;
            var manuelle = forcage == ForcageDeLEtat.Suspendu ? forceLe : null;
            suspenduLe = (automatique, manuelle) switch
            {
                ({ } a, { } m) => a < m ? a : m,
                ({ } a, null) => a,
                (null, { } m) => m,
                _ => aujourdhui,
            };
        }

        return new SituationDeLAbonnement(
            etat,
            payeJusquAu,
            etat == EtatDeLAbonnement.EnRetard ? payeJusquAu!.Value.AddDays(JoursDeGrace) : null,
            suspenduLe,
            etat == EtatDeLAbonnement.Actif ? reactiveJusquAu : null);
    }

    private static EtatDeLAbonnement EtatSansForcage(DateOnly? payeJusquAu, DateOnly aujourdhui)
    {
        if (payeJusquAu is not { } fin)
            return EtatDeLAbonnement.EnAttente;
        if (aujourdhui <= fin)
            return EtatDeLAbonnement.Actif;
        if (aujourdhui <= fin.AddDays(JoursDeGrace))
            return EtatDeLAbonnement.EnRetard;
        return EtatDeLAbonnement.Suspendu;
    }
}
