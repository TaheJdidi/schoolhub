namespace SchoolHub.Socle.Messages;

/// <summary>
/// Les messages WhatsApp (exigences communes) : la plateforme prépare un lien <c>wa.me</c> avec le texte déjà écrit ;
/// la secrétaire l'ouvre et l'envoie depuis le WhatsApp du centre. Rien ne part tout seul.
/// </summary>
public static class WhatsApp
{
    /// <summary>« https://wa.me/21698401220?text=… » : le numéro tunisien de 8 chiffres, avec l'indicatif 216.</summary>
    public static string Lien(string chiffres, string texte) => $"https://wa.me/216{chiffres}?text={Uri.EscapeDataString(texte)}";
}

/// <summary>Les petites règles du français des messages.</summary>
public static class Francais
{
    /// <summary>« d'Adem Miled », « d'Hédi », « de Sami Chaabane », « de Yassine » : l'élision devant une voyelle ou un h.</summary>
    public static string De(string nom) =>
        nom.Length > 0 && "aeiouhàâéèêëîïôûüAEIOUHÀÂÉÈÊËÎÏÔÛÜ".Contains(nom[0]) ? $"d'{nom}" : $"de {nom}";
}
