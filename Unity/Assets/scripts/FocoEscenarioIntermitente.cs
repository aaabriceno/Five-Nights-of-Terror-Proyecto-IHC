using UnityEngine;

[RequireComponent(typeof(Light))]
public sealed class FocoEscenarioIntermitente : MonoBehaviour
{
    [SerializeField] private Renderer panel;
    [SerializeField] private float fase;

    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private Light foco;
    private MaterialPropertyBlock propiedades;
    private float intensidad;

    public void Configurar(Renderer panelDelFoco, float desfase)
    {
        panel = panelDelFoco;
        fase = desfase;
    }

    private void Awake()
    {
        foco = GetComponent<Light>();
        intensidad = foco.intensity;
        propiedades = new MaterialPropertyBlock();
    }

    private void Update()
    {
        float reloj = Time.time + fase;
        float ciclo = Mathf.Repeat(reloj, 5.7f);
        float vibracion = Mathf.PerlinNoise(reloj * 11f, fase);
        float brillo = .58f + .35f * vibracion;
        if (ciclo < .37f || (ciclo > 1.55f && ciclo < 2.1f))
            brillo = .025f + .21f * vibracion;
        if (ciclo > 3.8f && ciclo < 4.15f)
            brillo *= .38f;

        foco.intensity = intensidad * brillo;
        if (panel == null) return;
        panel.GetPropertyBlock(propiedades);
        propiedades.SetColor(Emission, foco.color * (.18f * brillo));
        panel.SetPropertyBlock(propiedades);
    }
}
