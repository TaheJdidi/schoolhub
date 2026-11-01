namespace SchoolHub.Socle.Centres;

/// <summary>
/// Établissement client qui utilise SchoolHub. L'équipe SchoolHub le crée (CEN-01).
/// </summary>
public sealed class Centre(string nom, string code)
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Nom { get; private set; } = nom;

    /// <summary>Code du lien public du formulaire d'inscription, par exemple « les-jasmins ».</summary>
    public string Code { get; private set; } = code;
}
