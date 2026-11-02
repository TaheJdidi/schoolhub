using System.ComponentModel.DataAnnotations;

namespace SchoolHub.Socle.Emails;

/// <summary>
/// Ce que l'application sait d'elle-même, lu dans la section « Application » de la configuration.
/// </summary>
public sealed class OptionsDeLApplication
{
    public const string Section = "Application";

    /// <summary>L'adresse de l'application, qui sert à fabriquer les liens des emails, par exemple « http://localhost:5087 ».</summary>
    [Required]
    public string Adresse { get; set; } = "";
}
