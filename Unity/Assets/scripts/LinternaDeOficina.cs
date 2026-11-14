using System;

/// <summary>
/// La regla de la linterna del jugador 1 (paso 8). Se prende mientras mantiene
/// apretado su botón en la tablet (la batería es la de las compuertas, ver
/// CompuertasDeDuctos) y alumbra hacia donde mira la cámara: un cono de
/// `MitadDelAngulo` a cada lado y `Alcance` metros. Si alumbra a Freddy
/// `SegundosParaAhuyentar` seguidos, Freddy retrocede `PuntosQueRetrocede`
/// puntos de su ruta (de P07 a P05, de P06 a P04), donde ya queda fuera de
/// alcance. Cada amenaza tiene su defensa: Freddy, mirar la pantalla y la
/// linterna; Vixy y Puppet, las compuertas.
///
/// Sin `UnityEngine`, para probarla fuera del editor: todo se mide visto desde
/// arriba (x, z), sin la altura. Freddy solo se acerca por el pasillo recto
/// del frente, así que no hace falta comprobar paredes en medio.
/// </summary>
public sealed class LinternaDeOficina
{
    public const float Alcance = 10f;
    public const float MitadDelAngulo = 20f;
    public const float SegundosParaAhuyentar = 2f;
    public const int PuntosQueRetrocede = 2;

    private static readonly float CosenoDelBorde = (float)Math.Cos(MitadDelAngulo * Math.PI / 180.0);

    private float segundos;

    /// ¿Cae (x, z) dentro del cono que sale de (desdeX, desdeZ) hacia (haciaX, haciaZ)?
    public static bool Alumbra(float desdeX, float desdeZ, float haciaX, float haciaZ, float x, float z)
    {
        float dx = x - desdeX, dz = z - desdeZ;
        float distancia = (float)Math.Sqrt(dx * dx + dz * dz);
        float largo = (float)Math.Sqrt(haciaX * haciaX + haciaZ * haciaZ);
        if (largo < 1e-4f || distancia > Alcance) return false;
        if (distancia < 0.01f) return true;
        return (dx * haciaX + dz * haciaZ) / (distancia * largo) >= CosenoDelBorde;
    }

    /// Devuelve true una vez, cuando lleva `SegundosParaAhuyentar` seguidos
    /// alumbrado: es el momento de retroceder. Sin luz la cuenta vuelve a cero.
    public bool Avanzar(float segundosTranscurridos, bool alumbrado)
    {
        if (!alumbrado)
        {
            segundos = 0f;
            return false;
        }
        segundos += Math.Max(0f, segundosTranscurridos);
        if (segundos < SegundosParaAhuyentar) return false;
        segundos = 0f;
        return true;
    }

    public void Reiniciar() { segundos = 0f; }
}
