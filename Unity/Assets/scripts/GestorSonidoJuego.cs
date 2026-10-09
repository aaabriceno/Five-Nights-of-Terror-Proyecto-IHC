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
    [SerializeField] private AudioSource fuenteEstatica;

    [Header("Musica de ambiente")]
    public AudioClip musicaMenu;
    public AudioClip estatica;

    [Header("Caja de Puppet (prioridad baja)")]
    public AudioClip cuerdaCajaMusica;
    public AudioClip miGrandfathersClock;
    public AudioClip peligro;
    public AudioClip popGoesTheWeasel;

    [Header("Fin de noche (prioridad alta)")]
    public AudioClip campanas6am;

    private Prioridad prioridadActual = Prioridad.Baja;
    private float volumenEstaticaNormal = 0.5f;

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
        if (fuenteEstatica == null)
        {
            fuenteEstatica = gameObject.AddComponent<AudioSource>();
            fuenteEstatica.loop = true;
            fuenteEstatica.playOnAwake = false;
            fuenteEstatica.volume = volumenEstaticaNormal;
        }

        // Cargar clips si no están asignados en el Inspector
        if (musicaMenu == null) musicaMenu = Resources.Load<AudioClip>("sounds/FNaF_2_-_Música_del_menú");
        if (estatica == null) estatica = Resources.Load<AudioClip>("sounds/FNaF_2_-_Estática");
        if (cuerdaCajaMusica == null) cuerdaCajaMusica = Resources.Load<AudioClip>("sounds/FNaF_2_-_Dándole_cuerda_a_la_caja_de_música");
        if (miGrandfathersClock == null) miGrandfathersClock = Resources.Load<AudioClip>("sounds/FNaF_2_-_My_Grandfather's_Clock");
        if (peligro == null) peligro = Resources.Load<AudioClip>("sounds/FNaF_2_-_Peligro");
        if (popGoesTheWeasel == null) popGoesTheWeasel = Resources.Load<AudioClip>("sounds/FNaF_2_-_Pop!_Goes_the_Weasel");
        if (campanas6am == null) campanas6am = Resources.Load<AudioClip>("sounds/FNaF_2_-_Campanas_(6_a.m.)");
    }

    public void ReproducirMusicaMenu()
    {
        ReproducirMusica(musicaMenu);
    }

    public void DetenerMusica()
    {
        fuenteMusica.Stop();
    }

    public void IniciarEstativa()
    {
        if (fuenteEstatica != null && estatica != null)
        {
            fuenteEstatica.clip = estatica;
            fuenteEstatica.volume = volumenEstaticaNormal;
            fuenteEstatica.Play();
        }
    }

    public void DetenerEstatica()
    {
        if (fuenteEstatica != null) fuenteEstatica.Stop();
    }

    public void ReproducirCuerdaCajaMusica()
    {
        if (fuenteEstatica != null) fuenteEstatica.volume = volumenEstaticaNormal * 0.3f;
        ReproducirEfecto(cuerdaCajaMusica, Prioridad.Baja);
    }

    public void ReproducirMiGrandfathersClock()
    {
        if (fuenteEstatica != null) fuenteEstatica.volume = volumenEstaticaNormal * 0.3f;
        ReproducirEfecto(miGrandfathersClock, Prioridad.Baja);
    }

    public void ReproducirPeligro()
    {
        if (fuenteEstatica != null) fuenteEstatica.volume = volumenEstaticaNormal * 0.3f;
        ReproducirEfecto(peligro, Prioridad.Baja);
    }

    public void ReproducirPopGoesTheWeasel()
    {
        if (fuenteEstatica != null) fuenteEstatica.volume = volumenEstaticaNormal * 0.3f;
        ReproducirEfecto(popGoesTheWeasel, Prioridad.Baja);
    }

    public void ReproducirCampanas6am()
    {
        if (fuenteEstatica != null) fuenteEstatica.volume = 0;
        ReproducirEfecto(campanas6am, Prioridad.Alta);
    }

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
        if (clip == null || (fuenteMusica.clip == clip && fuenteMusica.isPlaying)) return;
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
        // Cuando termina un efecto, vuelve estática a volumen normal
        StartCoroutine(RestablecerEstaticaAlTerminar(clip.length));
    }

    private System.Collections.IEnumerator RestablecerEstaticaAlTerminar(float duracion)
    {
        yield return new WaitForSeconds(duracion);
        if (fuenteEstatica != null && fuenteEstatica.isPlaying)
            fuenteEstatica.volume = volumenEstaticaNormal;
    }
}
