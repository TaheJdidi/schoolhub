using System.Security.Cryptography;
using System.Text;

namespace SchoolHub.Socle.Comptes;

/// <summary>
/// Les codes à 6 chiffres des applications d'authentification (Google Authenticator, Microsoft Authenticator…),
/// selon la RFC 6238 : HMAC-SHA1 sur le nombre de périodes de 30 secondes depuis 1970, réglage que toutes ces applications lisent.
/// Écrit ici plutôt que pris à Identity, qui lit l'heure réelle : celui-ci prend le moment en paramètre,
/// et les tests règlent l'horloge.
/// </summary>
public static class Totp
{
    public const int Chiffres = 6;

    public static readonly TimeSpan Periode = TimeSpan.FromSeconds(30);

    /// <summary>Le numéro de la période de 30 secondes de ce moment.</summary>
    public static long Pas(DateTimeOffset moment) => moment.ToUnixTimeSeconds() / (long)Periode.TotalSeconds;

    /// <summary>Le code à 6 chiffres de cette clé pour cette période (RFC 4226, troncature dynamique).</summary>
    public static string Code(byte[] cle, long pas)
    {
        Span<byte> compteur = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(compteur, pas);
        Span<byte> empreinte = stackalloc byte[20];
        HMACSHA1.HashData(cle, compteur, empreinte);

        var decalage = empreinte[^1] & 0x0F;
        var nombre = ((empreinte[decalage] & 0x7F) << 24)
            | (empreinte[decalage + 1] << 16)
            | (empreinte[decalage + 2] << 8)
            | empreinte[decalage + 3];
        return (nombre % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// La période du code saisi s'il est bon à ce moment, ou <c>null</c>. Un code de la période d'avant ou d'après
    /// est accepté aussi : le téléphone et le serveur n'ont jamais tout à fait la même heure.
    /// </summary>
    public static long? Verifier(byte[] cle, string code, DateTimeOffset maintenant)
    {
        code = code.Replace(" ", "", StringComparison.Ordinal);
        if (code.Length != Chiffres || !code.All(char.IsAsciiDigit))
            return null;

        var pas = Pas(maintenant);
        for (var ecart = -1; ecart <= 1; ecart++)
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Code(cle, pas + ecart)), Encoding.ASCII.GetBytes(code)))
                return pas + ecart;
        return null;
    }

    /// <summary>Une nouvelle clé de 160 bits, comme le recommande la RFC 4226.</summary>
    public static byte[] NouvelleCle() => RandomNumberGenerator.GetBytes(20);

    /// <summary>
    /// L'adresse que lit l'application d'authentification dans le QR code :
    /// <c>otpauth://totp/SchoolUp:email?secret=…&amp;issuer=SchoolUp</c>.
    /// </summary>
    public static string Adresse(string email, byte[] cle) =>
        $"otpauth://totp/SchoolUp:{Uri.EscapeDataString(email)}?secret={Base32.Encoder(cle)}&issuer=SchoolUp&digits={Chiffres}&period={(int)Periode.TotalSeconds}";
}

/// <summary>Le Base32 de la RFC 4648 (A à Z, 2 à 7), sans « = » : la forme des clés TOTP.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encoder(byte[] octets)
    {
        var texte = new StringBuilder((octets.Length * 8 + 4) / 5);
        int tampon = 0, bits = 0;
        foreach (var octet in octets)
        {
            tampon = (tampon << 8) | octet;
            bits += 8;
            while (bits >= 5)
            {
                texte.Append(Alphabet[(tampon >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            texte.Append(Alphabet[(tampon << (5 - bits)) & 31]);
        return texte.ToString();
    }

    public static byte[] Decoder(string texte)
    {
        texte = texte.TrimEnd('=').Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var octets = new List<byte>(texte.Length * 5 / 8);
        int tampon = 0, bits = 0;
        foreach (var lettre in texte)
        {
            var valeur = Alphabet.IndexOf(lettre, StringComparison.Ordinal);
            if (valeur < 0)
                throw new FormatException("Ce n'est pas du Base32.");
            tampon = (tampon << 5) | valeur;
            bits += 5;
            if (bits >= 8)
            {
                octets.Add((byte)((tampon >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. octets];
    }
}
