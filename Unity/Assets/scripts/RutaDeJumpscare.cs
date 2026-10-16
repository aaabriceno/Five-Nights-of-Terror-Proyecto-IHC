/// <summary>
/// Qué archivo de video corresponde a cada animatrónico. Vive aparte, sin
/// UnityEngine, para poder probar la regla fuera del editor.
///
/// Los videos van en `Assets/StreamingAssets/jumpscares/` con el nombre
/// `jumpscare_<animatrónico>.webm` (WebM/VP8: es el formato que el reproductor
/// de video de Unity acepta tanto en Linux como en Windows). Para cambiar un
/// jumpscare basta con reemplazar el archivo: no hay que tocar código.
/// </summary>
public static class RutaDeJumpscare
{
    public const string Carpeta = "jumpscares";

    /// Los animatrónicos que tienen video, tal como los nombra
    /// `AnimatronicUnityBrain.nombreAnimatronico` (en minúsculas).
    public static readonly string[] Animatronicos = { "freddy", "vixy", "puppet" };

    /// Nombre del archivo, o null si ese animatrónico no tiene video. Un nombre
    /// que no sea exactamente uno de los tres (por ejemplo el genérico
    /// "Animatrónico" que manda Unity cuando no identifica al atacante) no
    /// produce ningún archivo: así tampoco se puede armar una ruta fuera de la
    /// carpeta a partir de un texto raro.
    public static string NombreDeArchivo(string animatronico)
    {
        if (string.IsNullOrEmpty(animatronico)) return null;
        string limpio = animatronico.Trim().ToLowerInvariant();
        foreach (string conocido in Animatronicos)
            if (limpio == conocido) return "jumpscare_" + conocido + ".webm";
        return null;
    }
}
