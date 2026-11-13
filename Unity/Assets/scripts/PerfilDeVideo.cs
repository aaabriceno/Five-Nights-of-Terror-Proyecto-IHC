using System;

/// <summary>
/// Los números de rendimiento que el equipo acordó para el juego, juntos y sin
/// `UnityEngine` para poder probarlos fuera del editor.
///
/// * **1920x1080** es la resolución estándar: ni más (2560x1600 en esta laptop
///   iba a ~20 cuadros por segundo en la tarjeta integrada) ni menos.
/// * **60 cuadros por segundo como tope**: más no aporta y gasta calor y batería.
///   Si la PC no llega, el juego simplemente va a los que pueda.
/// * **30 posiciones por segundo** hacia el jugador 2. El ritmo de la red no
///   depende de los cuadros: lo que dependen de ellos es *cuándo* lee Unity lo
///   que llega (una vez por cuadro).
/// </summary>
public static class PerfilDeVideo
{
    public const int Ancho = 1920;
    public const int Alto = 1080;
    public const int FpsMaximos = 60;

    public const int PosicionesPorSegundo = 30;
    public const float SegundosEntrePosiciones = 1f / PosicionesPorSegundo;

    /// Quien lanza el juego con -screen-width, -screen-height, -screen-fullscreen o
    /// -popupwindow ya eligió la pantalla (las pruebas lo abren en ventana chica
    /// dentro de una pantalla virtual): el perfil no se la pisa.
    public static bool LaLineaDeComandosFijaLaPantalla(string[] argumentos)
    {
        if (argumentos == null) return false;
        foreach (string argumento in argumentos)
        {
            if (argumento == "-screen-width" || argumento == "-screen-height" ||
                argumento == "-screen-fullscreen" || argumento == "-popupwindow")
                return true;
        }
        return false;
    }

    /// MediaPipe detecta la cara en la GPU por defecto, pero hay dos casos en que
    /// la CPU es mejor desde el principio: una PC sin tarjeta gráfica (Mesa dibuja
    /// por software: "llvmpipe" o "softpipe", y simular una GPU en la CPU es más
    /// lento que usar la CPU directamente) y cuando alguien lo pide con
    /// -mediapipe-cpu. Si la GPU falla por otra razón (p. ej. el contexto EGL
    /// con la NVIDIA), el propio runner cae a la CPU: no hace falta adivinarlo aquí.
    public static bool MediaPipeDebeUsarLaCpu(string nombreDeLaTarjeta, string[] argumentos)
    {
        if (argumentos != null && Array.IndexOf(argumentos, "-mediapipe-cpu") >= 0) return true;
        if (string.IsNullOrEmpty(nombreDeLaTarjeta)) return false;
        string nombre = nombreDeLaTarjeta.ToLowerInvariant();
        return nombre.Contains("llvmpipe") || nombre.Contains("softpipe");
    }

    /// Cuánto gasta el ambiente (ver AjusteDeAmbiente): `Alto` suma bloom,
    /// sombras de la oficina y más polvo; `Basico` deja viñeta, grano, niebla y
    /// ojos, que casi no cuestan.
    public enum Nivel { Basico, Alto }

    /// Alto solo con una tarjeta dedicada (NVIDIA, Radeon RX, Intel Arc): la
    /// integrada de esta laptop ya va a ~35 cuadros con MediaPipe en la CPU y
    /// no le sobra. -graficos-alto y -graficos-basico mandan sobre la tarjeta.
    public static Nivel NivelGrafico(string nombreDeLaTarjeta, string[] argumentos)
    {
        if (argumentos != null)
        {
            if (Array.IndexOf(argumentos, "-graficos-alto") >= 0) return Nivel.Alto;
            if (Array.IndexOf(argumentos, "-graficos-basico") >= 0) return Nivel.Basico;
        }
        if (string.IsNullOrEmpty(nombreDeLaTarjeta)) return Nivel.Basico;
        string nombre = nombreDeLaTarjeta.ToLowerInvariant();
        if (nombre.Contains("llvmpipe") || nombre.Contains("softpipe")) return Nivel.Basico;
        bool dedicada = nombre.Contains("nvidia") || nombre.Contains("geforce") || nombre.Contains("rtx")
            || nombre.Contains("radeon rx") || nombre.Contains("radeon pro") || nombre.Contains("arc a");
        return dedicada ? Nivel.Alto : Nivel.Basico;
    }
}
