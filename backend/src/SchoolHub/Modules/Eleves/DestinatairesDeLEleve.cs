namespace SchoolHub.Modules.Eleves;

/// <summary>Un destinataire d'un message sur un élève : « Parent » ou le prénom de l'élève, et ses 8 chiffres.</summary>
public sealed record DestinataireDUnMessage(string Nom, string Chiffres);

/// <summary>
/// Les destinataires d'un message sur un élève (exigences communes, « Messages ») : un élève majeur reçoit seul ses
/// messages ; un mineur qui a un téléphone les reçoit avec son parent (deux messages) ; sinon, le parent seul. Un majeur
/// sans téléphone à lui les fait suivre à son parent, s'il en a un. La règle vaut pour tous les messages.
/// </summary>
public static class DestinatairesDeLEleve
{
    public const string Parent = "Parent";

    public static IReadOnlyList<DestinataireDUnMessage> De(Eleve eleve)
    {
        var parent = eleve.TelephoneParent is { } chiffresDuParent ? new DestinataireDUnMessage(Parent, chiffresDuParent) : null;
        var lui = eleve.TelephoneEleve is { } chiffres ? new DestinataireDUnMessage(eleve.Prenom, chiffres) : null;
        if (eleve.Majeur)
            return lui is not null ? [lui] : parent is not null ? [parent] : [];

        return new[] { parent, lui }.OfType<DestinataireDUnMessage>().ToList();
    }
}
