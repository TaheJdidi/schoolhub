namespace SchoolHub.Socle.Centres;

/// <summary>
/// Le centre pour lequel travaille la requête en cours. Il se déduit du compte connecté,
/// qui appartient à un seul centre, ou se donne à la main pour une commande (CEN-01)
/// ou pour le formulaire public (DEM-01). Sans centre courant, on ne voit aucune donnée.
/// </summary>
public sealed class CentreCourant
{
    public Guid? Id { get; private set; }

    public void Definir(Guid id)
    {
        if (Id is not null && Id != id)
            throw new InvalidOperationException("Le centre courant est déjà défini : une requête ne change pas de centre.");

        Id = id;
    }
}
