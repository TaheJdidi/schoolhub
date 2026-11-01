namespace SchoolHub.Socle.Centres;

/// <summary>
/// Levée quand on veut enregistrer une donnée hors du centre courant : sans centre courant,
/// pour un autre centre, ou en la passant d'un centre à un autre.
/// </summary>
public sealed class DonneeHorsDuCentreException(string message) : InvalidOperationException(message);
