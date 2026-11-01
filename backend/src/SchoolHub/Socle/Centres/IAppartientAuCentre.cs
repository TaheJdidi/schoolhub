namespace SchoolHub.Socle.Centres;

/// <summary>
/// Donnée qui appartient à un seul centre (ADR 0001). Le contexte de données remplit
/// <see cref="CentreId"/> à l'ajout, et ne montre la donnée qu'à son centre.
/// </summary>
public interface IAppartientAuCentre
{
    Guid CentreId { get; }
}
