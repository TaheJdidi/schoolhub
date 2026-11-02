namespace SchoolHub.Socle.Emails;

/// <summary>
/// Envoie un email en texte simple, en français. L'application ne connaît que cette interface :
/// en développement, les emails vont dans la console (<see cref="EmailDansLaConsole"/>), et plus tard chez Brevo.
/// </summary>
public interface IEnvoiEmail
{
    Task EnvoyerAsync(string destinataire, string sujet, string texte, CancellationToken annulation = default);
}
