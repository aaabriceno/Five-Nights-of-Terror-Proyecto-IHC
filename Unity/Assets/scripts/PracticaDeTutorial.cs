/// <summary>
/// Lo que el jugador 1 ya probó en la práctica del tutorial. Son dos
/// diapositivas: en la primera usa los controles de la tablet (girar la
/// cámara, prender la linterna, cerrar una compuerta, darle cuerda a la caja
/// de Puppet) y en la segunda resuelve los cables y sostiene la mirada.
///
/// Unity lo marca al recibir las mismas órdenes que manda la tablet en una
/// partida, así la PC y la tablet muestran la misma lista. Cada método devuelve
/// true solo si cambió algo, para publicar el estado en ese momento.
///
/// Sin UnityEngine, para probarla fuera del editor.
/// </summary>
public class PracticaDeTutorial
{
    /// Segundos seguidos mirando la pantalla para dar la mirada por verificada.
    public const float SegundosDeMirada = 2f;

    private bool camara, linterna, compuerta, cuerda, cables, mirada;
    private float segundosMirando;

    public bool Camara => camara;
    public bool Linterna => linterna;
    public bool Compuerta => compuerta;
    public bool Cuerda => cuerda;
    public bool Cables => cables;
    public bool Mirada => mirada;

    public bool ControlesListos => camara && linterna && compuerta && cuerda;
    public bool Completa => ControlesListos && cables && mirada;

    /// -1 o 1 es girar; 0 es soltar, que no cuenta.
    public bool UsarCamara(int direccion) => direccion != 0 && Marcar(ref camara);
    public bool UsarLinterna(bool encendida) => encendida && Marcar(ref linterna);
    /// Solo cuenta cerrarla: abrirla es lo que ya está.
    public bool UsarCompuerta(bool cerrar) => cerrar && Marcar(ref compuerta);
    public bool DarCuerda() => Marcar(ref cuerda);
    public bool CompletarCables() => Marcar(ref cables);

    /// Llamar cada cuadro mientras se muestra la práctica de la mirada.
    public bool Mirar(bool mirando, float segundos)
    {
        if (mirada) return false;
        segundosMirando = mirando ? segundosMirando + segundos : 0f;
        return segundosMirando + 1e-4f >= SegundosDeMirada && Marcar(ref mirada);
    }

    /// Al abrir el tutorial otra vez: todo sin probar.
    public void Reiniciar()
    {
        camara = linterna = compuerta = cuerda = cables = mirada = false;
        segundosMirando = 0f;
    }

    private static bool Marcar(ref bool paso)
    {
        if (paso) return false;
        paso = true;
        return true;
    }
}
