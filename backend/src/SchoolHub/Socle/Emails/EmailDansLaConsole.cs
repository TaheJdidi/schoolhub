namespace SchoolHub.Socle.Emails;

/// <summary>
/// Écrit l'email complet, lien compris, dans la console du serveur : en développement, on copie le lien pour le tester.
/// En production, on ne s'en sert jamais : il y écrirait des liens de connexion dans les journaux du serveur.
/// </summary>
public sealed class EmailDansLaConsole(ILogger<EmailDansLaConsole> journal) : IEnvoiEmail
{
    public Task EnvoyerAsync(string destinataire, string sujet, string texte, CancellationToken annulation = default)
    {
        journal.LogInformation("Email pour {Destinataire}\nSujet : {Sujet}\n{Texte}", destinataire, sujet, texte);
        return Task.CompletedTask;
    }
}
