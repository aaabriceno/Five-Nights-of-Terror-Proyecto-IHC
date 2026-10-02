using UnityEngine;

/// <summary>
/// Centraliza el audio del juego en dos canales: musica (loop, ambiente) y
/// efectos (one-shot). Los efectos de fin de noche (campanas/jumpscare) tienen
/// prioridad alta y cortan cualquier otro efecto sonando; los efectos de la
/// caja de Puppet tienen prioridad baja y se cortan sin problema ante ellos.
/// </summary>
public class GestorSonidoJuego : MonoBehaviour
{
    private enum Prioridad { Baja, Alta }

    [Header("Canales")]
    [SerializeField] private AudioSource fuenteMusica;
    [SerializeField] private AudioSource fuenteEfectos;

    [Header("Musica de ambiente")]
    public AudioClip musicaMenu;

    [Header("Caja de Puppet (prioridad baja)")]
    public AudioClip cuerdaCajaMusica;
    public AudioClip peligro;
    public AudioClip popGoesTheWeasel;

    [Header("Fin de noche (prioridad alta)")]
    public AudioClip campanas6am;

    private Prioridad prioridadActual = Prioridad.Baja;

    private void Awake()
    {
        if (fuenteMusica == null)
        {
            fuenteMusica = gameObject.AddComponent<AudioSource>();
            fuenteMusica.loop = true;
            fuenteMusica.playOnAwake = false;
        }
        if (fuenteEfectos == null)
        {
            fuenteEfectos = gameObject.AddComponent<AudioSource>();
            fuenteEfectos.loop = false;
            fuenteEfectos.playOnAwake = false;
        }
    }

    public void ReproducirMusicaMenu()
    {
        ReproducirMusica(musicaMenu);
    }

    public void DetenerMusica()
    {
        fuenteMusica.Stop();
    }

    public void ReproducirCuerdaCajaMusica() => ReproducirEfecto(cuerdaCajaMusica, Prioridad.Baja);

    public void ReproducirPeligro() => ReproducirEfecto(peligro, Prioridad.Baja);

    public void ReproducirPopGoesTheWeasel() => ReproducirEfecto(popGoesTheWeasel, Prioridad.Baja);

    public void ReproducirCampanas6am() => ReproducirEfecto(campanas6am, Prioridad.Alta);

    /// <summary>
    /// El jumpscare de escritorio reproduce su propio audio (ver
    /// UnityGameSessionController.ShowDesktopJumpscare); esto solo libera el
    /// canal de efectos para que un sonido de Puppet en curso no siga sonando
    /// encima del jumpscare.
    /// </summary>
    public void InterrumpirEfectosPorJumpscare()
    {
        if (fuenteEfectos.isPlaying && prioridadActual == Prioridad.Baja)
            fuenteEfectos.Stop();
        prioridadActual = Prioridad.Alta;
    }

    private void ReproducirMusica(AudioClip clip)
    {
        if (clip == null || fuenteMusica.clip == clip) return;
        fuenteMusica.clip = clip;
        fuenteMusica.Play();
    }

    private void ReproducirEfecto(AudioClip clip, Prioridad prioridad)
    {
        if (clip == null) return;
        // Un efecto de prioridad baja no puede cortar a uno de prioridad alta
        // que todavia este sonando; alta siempre puede cortar a cualquiera.
        if (fuenteEfectos.isPlaying && prioridadActual == Prioridad.Alta && prioridad == Prioridad.Baja)
            return;
        prioridadActual = prioridad;
        fuenteEfectos.Stop();
        fuenteEfectos.clip = clip;
        fuenteEfectos.Play();
    }
}
