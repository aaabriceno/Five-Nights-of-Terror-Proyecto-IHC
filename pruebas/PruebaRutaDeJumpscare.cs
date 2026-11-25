using System;

// Pruebas de RutaDeJumpscare fuera de Unity (mismo esquema que
// PruebaDireccionDeRed): está fuera de Assets/ para que el editor no la
// compile, porque tiene su propio Main.
public static class PruebaRutaDeJumpscare
{
    private static int fallos;

    private static void Comprobar(string descripcion, bool condicion)
    {
        Console.WriteLine((condicion ? "  ok    " : "  FALLA ") + descripcion);
        if (!condicion) fallos++;
    }

    public static int Main()
    {
        Comprobar("Freddy -> jumpscare_freddy.webm",
            RutaDeJumpscare.NombreDeArchivo("Freddy") == "jumpscare_freddy.webm");
        Comprobar("Vixy -> jumpscare_vixy.webm",
            RutaDeJumpscare.NombreDeArchivo("Vixy") == "jumpscare_vixy.webm");
        Comprobar("Puppet -> jumpscare_puppet.webm",
            RutaDeJumpscare.NombreDeArchivo("Puppet") == "jumpscare_puppet.webm");
        Comprobar("no distingue mayúsculas ni espacios",
            RutaDeJumpscare.NombreDeArchivo("  fReDdY ") == "jumpscare_freddy.webm");
        Comprobar("el nombre genérico que manda Unity no tiene video",
            RutaDeJumpscare.NombreDeArchivo("Animatrónico") == null);
        Comprobar("un nombre desconocido no tiene video",
            RutaDeJumpscare.NombreDeArchivo("Bonnie") == null);
        Comprobar("vacío o null no tiene video",
            RutaDeJumpscare.NombreDeArchivo("") == null && RutaDeJumpscare.NombreDeArchivo(null) == null);
        Comprobar("un intento de salirse de la carpeta no pasa",
            RutaDeJumpscare.NombreDeArchivo("../../etc/passwd") == null);
        Comprobar("la lista de animatrónicos son los tres",
            RutaDeJumpscare.Animatronicos.Length == 3);

        // Cara de respaldo (2026-11-24): se ve al instante y queda si el video falla.
        Comprobar("Freddy -> recurso jumpscare_freddy (su cara)",
            RutaDeJumpscare.NombreDeImagen("Freddy") == "jumpscare_freddy");
        Comprobar("Vixy -> jumpscare_vixy y Puppet -> jumpscare_puppet",
            RutaDeJumpscare.NombreDeImagen(" vixy ") == "jumpscare_vixy"
            && RutaDeJumpscare.NombreDeImagen("PUPPET") == "jumpscare_puppet");
        Comprobar("el genérico, vacío, null o una ruta usan la imagen general",
            RutaDeJumpscare.NombreDeImagen("Animatrónico") == RutaDeJumpscare.ImagenGeneral
            && RutaDeJumpscare.NombreDeImagen("") == RutaDeJumpscare.ImagenGeneral
            && RutaDeJumpscare.NombreDeImagen(null) == RutaDeJumpscare.ImagenGeneral
            && RutaDeJumpscare.NombreDeImagen("../freddy") == RutaDeJumpscare.ImagenGeneral);
        Comprobar("la imagen general es desktop_jumpscare", RutaDeJumpscare.ImagenGeneral == "desktop_jumpscare");

        Console.WriteLine();
        Console.WriteLine(fallos == 0 ? "Todas las pruebas pasaron." : fallos + " prueba(s) fallaron.");
        return fallos == 0 ? 0 : 1;
    }
}
