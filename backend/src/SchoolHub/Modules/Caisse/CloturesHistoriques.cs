namespace SchoolHub.Modules.Caisse;

/// <summary>
/// CAI-09 : les reçus et les sorties de caisse d'avant la clôture du soir. Sans elles, chaque jour passé d'une base qui a
/// déjà des reçus deviendrait une caisse « pas clôturée », à compter et clôturer jour par jour. La migration
/// <c>RangerLesAnciensRecus</c> range donc, pour chaque compte et chaque jour passé, ses reçus et ses sorties encore
/// ouverts dans une clôture « historique » : le compté est l'attendu (écart nul), et l'argent est marqué reçu. La caisse
/// du jour reste ouverte.
/// </summary>
public static class CloturesHistoriques
{
    /// <summary>Le jour de Tunis au moment où la migration s'applique.</summary>
    public const string AujourdhuiATunis = "(now() AT TIME ZONE 'Africa/Tunis')::date";

    /// <summary>
    /// Le SQL qui range les reçus et les sorties encore ouverts des jours avant <paramref name="aujourdhui"/> (une
    /// expression SQL de type date). Chaque clôture prend le rang suivant du jour de son compte.
    /// </summary>
    public static string Sql(string aujourdhui) => $"""
        INSERT INTO clotures_de_caisse (id, centre_id, compte_id, secretaire, jour, rang, encaisse, sorties, attendu, compte, ecart,
            cloturee_le, argent_recu_le, argent_recu_par_id)
        SELECT gen_random_uuid(), o.centre_id, o.auteur_id, min(o.auteur), o.jour,
            coalesce((SELECT max(c.rang) FROM clotures_de_caisse c WHERE c.compte_id = o.auteur_id AND c.jour = o.jour), 0) + 1,
            sum(o.encaisse), sum(o.sorties), sum(o.encaisse) - sum(o.sorties), sum(o.encaisse) - sum(o.sorties), 0,
            now(), now(), NULL
        FROM (
            SELECT centre_id, auteur_id, auteur, jour, CASE WHEN annule_le IS NULL THEN total ELSE 0 END AS encaisse, 0 AS sorties
            FROM recus WHERE cloture_id IS NULL AND jour < {aujourdhui}
            UNION ALL
            SELECT centre_id, auteur_id, auteur, jour, 0, montant
            FROM achats WHERE paye_depuis_la_caisse AND cloture_id IS NULL AND jour < {aujourdhui}
        ) o
        GROUP BY o.centre_id, o.auteur_id, o.jour;

        UPDATE recus r SET cloture_id = c.id
        FROM clotures_de_caisse c
        WHERE r.cloture_id IS NULL AND r.jour < {aujourdhui} AND c.centre_id = r.centre_id AND c.compte_id = r.auteur_id AND c.jour = r.jour
            AND c.rang = (SELECT max(d.rang) FROM clotures_de_caisse d WHERE d.compte_id = c.compte_id AND d.jour = c.jour);

        UPDATE achats a SET cloture_id = c.id
        FROM clotures_de_caisse c
        WHERE a.paye_depuis_la_caisse AND a.cloture_id IS NULL AND a.jour < {aujourdhui} AND c.centre_id = a.centre_id
            AND c.compte_id = a.auteur_id AND c.jour = a.jour
            AND c.rang = (SELECT max(d.rang) FROM clotures_de_caisse d WHERE d.compte_id = c.compte_id AND d.jour = c.jour);
        """;
}
