using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class IntegrarMapaBosquejoV2
{
    const string ModelPath = "Assets/models/edificio/mapa_bosquejo_v2.obj";
    const string NavMeshPath = "Assets/models/edificio/Mapa_V2_NavMesh.asset";

    [MenuItem("Tools/Mapa V2/Actualizar navegación y comprobar escena")]
    public static void UpdateNavigation()
    {
        var scene = SceneManager.GetActiveScene();
        var map = GameObject.Find("Mapa_V2");
        var building = map == null ? null : map.transform.Find("Edificio_Mapa_V2");
        if (scene.name != "Face Landmark Detection" || scene.isDirty || building == null)
        {
            Debug.LogError("Abre y guarda la escena Face Landmark Detection con Mapa_V2 antes de actualizarla.");
            return;
        }

        var renderers = building.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0)
            throw new InvalidOperationException("El edificio importado no contiene mallas visibles.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        Debug.Log($"Mapa V2: límites del edificio {bounds.min} a {bounds.max}.");
        if (!bounds.Contains(new Vector3(20, 1.6f, 2.5f)))
            throw new InvalidOperationException("El edificio aún está fuera de las coordenadas del plano.");

        foreach (var renderer in renderers)
        {
            if (!renderer.name.StartsWith("Techo_", StringComparison.OrdinalIgnoreCase))
                continue;
            var modifier = renderer.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = Undo.AddComponent<NavMeshModifier>(renderer.gameObject);
            modifier.overrideArea = true;
            modifier.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        var surface = map.GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = Undo.AddComponent<NavMeshSurface>(map);
        BakeAndSaveNavigation(surface);

        var nodes = map.transform.Find("Nodos_Ruta");
        nodes.Find("P01").localPosition = new Vector3(16.5f, .64f, 24.3f);
        nodes.Find("P02").localPosition = new Vector3(23.5f, .64f, 26.3f);
        AdjustLights(map.transform);
        foreach (var brain in Object.FindObjectsOfType<AnimatronicUnityBrain>())
        {
            var legacyPatrol = brain.GetComponent<PatrullaPorPuntos>();
            if (legacyPatrol != null)
                legacyPatrol.controladoPorIAExterna = true;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity no pudo guardar la escena.");
        ValidateRoutes();
    }

    static void AdjustLights(Transform map)
    {
        SetLightIntensity(map, "Luz_Comedor", 1.1f);
        SetLightIntensity(map, "Luz_Oficina", .75f);
        SetLightIntensity(map, "Luz_Escenario", .14f);
        SetLightIntensity(map, "Alerta_Ventilacion", .8f);
        SetLightIntensity(map, "Alerta_Ventilacion_2", .8f);
    }

    static void SetLightIntensity(Transform map, string name, float intensity)
    {
        var fixture = map.Find(name);
        if (fixture != null && fixture.TryGetComponent<Light>(out var light))
            light.intensity = intensity;
    }

    static void BakeAndSaveNavigation(NavMeshSurface surface)
    {
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.overrideVoxelSize = true;
        surface.voxelSize = .08f;
        surface.BuildNavMesh();
        var baked = surface.navMeshData;
        if (baked == null || EditorUtility.IsPersistent(baked))
            throw new InvalidOperationException("Unity no generó un NavMesh nuevo para el mapa.");

        var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
        if (asset == null)
        {
            AssetDatabase.CreateAsset(baked, NavMeshPath);
        }
        else
        {
            surface.RemoveData();
            EditorUtility.CopySerialized(baked, asset);
            EditorUtility.SetDirty(asset);
            surface.navMeshData = asset;
            surface.AddData();
            Object.DestroyImmediate(baked);
        }
        AssetDatabase.SaveAssets();
    }

    static void ValidateRoutes()
    {
        var path = new NavMeshPath();
        foreach (string actorName in new[] { "enemigo1", "enemigo3", "personaje2" })
        {
            var actor = GameObject.Find(actorName);
            var brain = actor.GetComponent<AnimatronicUnityBrain>();
            var agent = actor.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            bool found = NavMesh.SamplePosition(actor.transform.position, out var previous, .6f, filter);
            var previousName = actorName;
            var failures = new List<string>();
            var missing = new List<string>();
            foreach (var node in brain.ruta)
            {
                bool nextFound = NavMesh.SamplePosition(node.position, out var next, .6f, filter);
                if (!nextFound) missing.Add(node.name);
                if (!found || !nextFound ||
                    !NavMesh.CalculatePath(previous.position, next.position, filter, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    failures.Add(previousName + " → " + node.name);
                previous = next;
                previousName = node.name;
                found = nextFound;
            }
            if (failures.Count > 0)
                Debug.LogError($"Mapa V2: {brain.nombreAnimatronico}, tramos incompletos: {string.Join(", ", failures)}. Nodos fuera de malla: {string.Join(", ", missing)}.");
            else
                Debug.Log($"Mapa V2: ruta completa de {brain.nombreAnimatronico} ({brain.ruta.Length} nodos).");
        }
    }
    static readonly string[] MapNodes = {
        "P01","P02","P03","P04","P05","P06","P07","P08","P09","P10","P11","P12","P13","P14","P15","P16",
        "V01","V02","V03","V04","V05","V06","V07","V08","J1"
    };

    [MenuItem("Tools/Mapa V2/Integrar edificio, nodos y rutas")]
    public static void Integrate()
    {
        if (!SceneManager.GetActiveScene().IsValid() || string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
        { EditorUtility.DisplayDialog("Mapa V2", "Abre la escena Face Landmark Detection antes de integrar el mapa.", "Aceptar"); return; }
        if (SceneManager.GetActiveScene().isDirty)
        { EditorUtility.DisplayDialog("Mapa V2", "La escena tiene cambios sin guardar. Guarda la escena y vuelve a ejecutar la integración.", "Aceptar"); return; }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null) { EditorUtility.DisplayDialog("Mapa V2", "Unity aún no ha importado el OBJ: " + ModelPath, "Aceptar"); return; }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Integrar mapa Bosquejo V2");
        int undoGroup = Undo.GetCurrentGroup();
        Transform old = GameObject.Find("Mapa_V2")?.transform;
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        string[] removeNames = { "primer_proyecto", "primer_proyecto (1)", "Piso", "Terrain", "Terrain (1)", "NavMesh", "NavMesh2" };
        foreach (string name in removeNames) { GameObject go = GameObject.Find(name); if (go != null) Undo.DestroyObjectImmediate(go); }
        foreach (string name in MapNodes) { GameObject go = GameObject.Find(name); if (go != null) Undo.DestroyObjectImmediate(go); }
        string[] oldNodes = { "E1_punto1", "E1_punto2", "E1_punto3", "E1_punto4", "E2_punto1", "E2_punto2", "E2_punto3", "E2_punto4", "punto_penultimo_jumpscare" };
        foreach (string name in oldNodes) { GameObject go = GameObject.Find(name); if (go != null) Undo.DestroyObjectImmediate(go); }

        GameObject map = new GameObject("Mapa_V2"); Undo.RegisterCreatedObjectUndo(map, "Crear mapa V2");
        GameObject building = (GameObject)PrefabUtility.InstantiatePrefab(model, SceneManager.GetActiveScene());
        if (building == null) building = Object.Instantiate(model);
        Undo.RegisterCreatedObjectUndo(building, "Instanciar edificio V2"); building.name = "Edificio_Mapa_V2"; building.transform.SetParent(map.transform, false);
        MakeDoubleSided(building);
        foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
            if (t.name.ToLowerInvariant().Contains("techo"))
            { var mod = t.GetComponent<NavMeshModifier>() ?? Undo.AddComponent<NavMeshModifier>(t.gameObject); mod.overrideArea = true; mod.area = NavMesh.GetAreaFromName("Not Walkable"); }

        GameObject nodesRoot = new GameObject("Nodos_Ruta"); Undo.RegisterCreatedObjectUndo(nodesRoot, "Crear nodos"); nodesRoot.transform.SetParent(map.transform, false);
        Vector2[] pos = {
            new Vector2(16.5f,24.3f),new Vector2(23.5f,26.3f),new Vector2(20,19.5f),new Vector2(20,15.8f),new Vector2(20,11.8f),new Vector2(20,8),new Vector2(20,6.2f),new Vector2(13.5f,16),new Vector2(26.5f,16),new Vector2(7.5f,25.5f),new Vector2(7.5f,16),new Vector2(7.5f,10),new Vector2(34,25.5f),new Vector2(32.5f,19.5f),new Vector2(32.5f,16),new Vector2(32.5f,10),
            new Vector2(7.5f,7),new Vector2(7.5f,3),new Vector2(12.5f,3),new Vector2(16.3f,3),new Vector2(32.5f,7),new Vector2(32.5f,3),new Vector2(27.5f,3),new Vector2(23.7f,3),new Vector2(20,2.5f)
        };
        for (int i=0;i<MapNodes.Length;i++) { var n = new GameObject(MapNodes[i]); Undo.RegisterCreatedObjectUndo(n,"Crear nodo"); n.transform.SetParent(nodesRoot.transform,false); n.transform.position = new Vector3(pos[i].x, .1f, pos[i].y); }

        var surface = Undo.AddComponent<NavMeshSurface>(map);
        BakeAndSaveNavigation(surface);

        GameObject freddy=FindOrMake("enemigo1","Assets/models/enemigo1.obj"); GameObject vixy=FindOrMake("enemigo3","Assets/models/enemigo3.obj"); GameObject puppet=FindOrMake("personaje2","Assets/models/personaje2.obj");
        Place(freddy,new Vector3(16.5f,.64f,24.3f)); Place(vixy,new Vector3(23.5f,.64f,26.3f)); Place(puppet,new Vector3(34,0,25.5f));
        ConfigureActor(freddy,"Freddy",Route("P01","P03","P04","P05","P06","P07","J1"),true,true,false,true,false);
        ConfigureActor(vixy,"Vixy",Route("P02","P10","P11","P12","V01","V02","V03","V04","J1"),true,false,false,true,true);
        ConfigureActor(puppet,"Puppet",Route("P13","P14","P15","P16","V05","V06","V07","V08","J1"),false,false,true,false,false);

        FrameOfficeCamera();
        FaceActorsTowardPlayer();
        UnityGameSessionController controller=Object.FindObjectOfType<UnityGameSessionController>();
        if(controller!=null){ controller.jugador=GameObject.Find("J1").transform; controller.actorFreddy="enemigo1"; controller.actorVixy="enemigo3"; controller.actorPuppet="personaje2"; }
        AddLight(map.transform,"Luz_Comedor",new Vector3(20,2.8f,16),new Color(.65f,.82f,1f),13,7);
        AddLight(map.transform,"Luz_Oficina",new Vector3(20,2.5f,5),new Color(.55f,.75f,1f),10,5);
        AddLight(map.transform,"Luz_Escenario",new Vector3(20,3.1f,24),new Color(1f,.35f,.24f),11,6);
        AddLight(map.transform,"Alerta_Ventilacion",new Vector3(7.5f,2.3f,10),new Color(1f,.05f,.03f),5,4);
        AddLight(map.transform,"Alerta_Ventilacion_2",new Vector3(32.5f,2.3f,10),new Color(1f,.05f,.03f),5,4);
        AdjustLights(map.transform);
        ApplyAbandonedLook();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Undo.CollapseUndoOperations(undoGroup);
        EditorUtility.DisplayDialog("Mapa V2", "Edificio sustituido. Nodos, NavMesh, luces y rutas configurados en la escena.", "Aceptar");
    }

    [MenuItem("Tools/Mapa V2/Aplicar ambiente abandonado")]
    public static void ApplyAbandonedLook()
    {
        var scene = SceneManager.GetActiveScene();
        var map = GameObject.Find("Mapa_V2");
        if (EditorApplication.isPlaying || scene.name != "Face Landmark Detection" || map == null)
            return;

        var building = map.transform.Find("Edificio_Mapa_V2");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/models/edificio/MapaV2Desgastado.shader");
        if (building == null || shader == null || ShaderUtil.ShaderHasError(shader))
        {
            Debug.LogError("Mapa V2: falta el edificio o el shader de desgaste no pudo compilar.");
            return;
        }

        var renderers = building.GetComponentsInChildren<MeshRenderer>();
        foreach (var renderer in renderers)
        {
            var materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.shader == shader)
                    continue;
                string name = source.name.Replace("MapaV2_", "");
                float style;
                if (name.StartsWith("Piso_") || name == "Oficina_Amarillo")
                    style = 0;
                else if (name.StartsWith("Muros_"))
                    style = 1;
                else if (name == "Mesas_Madera" || name == "Sillas_Madera" ||
                         name == "Caja_Musica" || name == "Cortina_Roja" || name == "Escenario_Morado")
                    style = 2;
                else
                    continue;

                materials[i] = GetLookMaterial(source, shader, name, style);
                changed = true;
            }
            if (changed)
            {
                Undo.RecordObject(renderer, "Aplicar materiales de desgaste");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        AdjustLights(map.transform);
        var stageFill = map.transform.Find("Luz_Escenario");
        if (stageFill != null && stageFill.TryGetComponent<Light>(out var fill))
            fill.range = 6f;

        var targets = new[] {
            new Vector3(16.5f, 1.42f, 24.3f),
            new Vector3(20f, .64f, 24.4f),
            new Vector3(23.5f, 1.42f, 26.3f)
        };
        for (int i = 0; i < targets.Length; i++)
        {
            string prefix = "Foco_Escenario_" + (i + 1) + "_Panel";
            var panel = Array.Find(renderers, r => r.name.StartsWith(prefix, StringComparison.Ordinal));
            if (panel == null)
            {
                Debug.LogWarning("Mapa V2: no se encontró " + prefix);
                continue;
            }
            ConfigureStageLight(map.transform, panel, targets[i], i);
        }

        FitActorHeight("enemigo1", 1.76f);
        FitActorHeight("enemigo3", 1.70f);
        FitActorHeight("personaje2", 1.68f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Mapa V2: materiales desgastados, focos del escenario y escala de los actores actualizados.");
    }

    static Material GetLookMaterial(Material source, Shader shader, string name, float style)
    {
        string path = "Assets/models/edificio/Materials/Desgaste_" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(shader);
        material.SetFloat("_Style", style);
        if (source.HasProperty("_Color"))
            material.color = source.color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void ConfigureStageLight(Transform map, MeshRenderer panel, Vector3 target, int index)
    {
        const string panelPath = "Assets/models/edificio/Materials/Panel_Escenario_Tenue.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(panelPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            material.color = new Color(.13f, .12f, .10f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(.10f, .08f, .06f));
            AssetDatabase.CreateAsset(material, panelPath);
        }
        Undo.RecordObject(panel, "Atenuar panel del escenario");
        panel.sharedMaterial = material;
        PrefabUtility.RecordPrefabInstancePropertyModifications(panel);

        string name = "Luz_Intermitente_Escenario_" + (index + 1);
        var fixture = map.Find(name);
        if (fixture == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Añadir luz intermitente");
            fixture = go.transform;
            fixture.SetParent(map, false);
        }
        var light = fixture.GetComponent<Light>();
        if (light == null)
            light = Undo.AddComponent<Light>(fixture.gameObject);
        Vector3 origin = panel.bounds.center + Vector3.down * .045f;
        fixture.SetPositionAndRotation(origin, Quaternion.LookRotation(target - origin));
        light.type = LightType.Spot;
        light.spotAngle = 108f;
        light.range = 5f;
        light.color = index == 2 ? new Color(.60f, .72f, .89f) : new Color(1f, .74f, .49f);
        light.intensity = index == 1 ? .22f : .56f;
        light.renderMode = LightRenderMode.ForcePixel;
        var flicker = fixture.GetComponent<FocoEscenarioIntermitente>();
        if (flicker == null)
            flicker = Undo.AddComponent<FocoEscenarioIntermitente>(fixture.gameObject);
        flicker.Configurar(panel, index * 1.83f);
        EditorUtility.SetDirty(flicker);
    }

    static void FitActorHeight(string name, float maximumHeight)
    {
        var actor = GameObject.Find(name);
        if (actor == null) return;
        var renderers = actor.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        if (bounds.size.y <= maximumHeight + .01f)
            return;

        Undo.RecordObject(actor.transform, "Ajustar altura de " + name);
        actor.transform.localScale *= maximumHeight / bounds.size.y;
        PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
        Debug.Log($"Mapa V2: {name} reducido de {bounds.size.y:F2} m a {maximumHeight:F2} m.");
    }

    [MenuItem("Tools/Mapa V2/Orientar animatrónicos hacia el jugador")]
    public static void FaceActorsTowardPlayer()
    {
        var scene = SceneManager.GetActiveScene();
        var camera = Camera.main;
        if (EditorApplication.isPlaying || scene.name != "Face Landmark Detection" ||
            GameObject.Find("Mapa_V2") == null || camera == null)
            return;

        foreach (string name in new[] { "enemigo1", "enemigo3", "personaje2" })
        {
            var actor = GameObject.Find(name);
            if (actor == null)
                continue;

            Vector3 direction = camera.transform.position - actor.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f)
                continue;

            Undo.RecordObject(actor.transform, "Orientar animatrónico hacia el jugador");
            actor.transform.rotation = Quaternion.LookRotation(direction);
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Mapa V2/Encuadrar oficina y conductos")]
    public static void FrameOfficeCamera()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "Face Landmark Detection" ||
            GameObject.Find("Mapa_V2") == null || Camera.main == null)
            return;

        var camera = Camera.main;
        Undo.RecordObjects(new Object[] { camera, camera.transform }, "Encuadrar oficina y conductos");
        camera.transform.SetPositionAndRotation(new Vector3(20f, 1.6f, .55f), Quaternion.identity);
        camera.fieldOfView = 76f;
        camera.nearClipPlane = .05f;
        camera.farClipPlane = 100f;
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static GameObject FindOrMake(string name,string path) { GameObject go=GameObject.Find(name); if(go!=null)return go; var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(prefab==null)return null; go=(GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name=name; return go; }
    static void MakeDoubleSided(GameObject root){const string folder="Assets/models/edificio/Materials";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/models/edificio","Materials");foreach(Renderer r in root.GetComponentsInChildren<Renderer>(true)){var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){Material source=mats[i];if(source==null)continue;string safe=source.name.Replace(" ","_").Replace("/","_");string path=folder+"/MapaV2_"+safe+".mat";Material material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(source);material.name="MapaV2_"+safe;if(material.HasProperty("_Cull"))material.SetInt("_Cull",0);AssetDatabase.CreateAsset(material,path);}mats[i]=material;}r.sharedMaterials=mats;}}
    static void Place(GameObject go,Vector3 p){if(go!=null)go.transform.position=p;}
    static Transform[] Route(params string[] names){var a=new List<Transform>();foreach(var n in names){var g=GameObject.Find(n);if(g!=null)a.Add(g.transform);}return a.ToArray();}
    static void ConfigureActor(GameObject go,string role,Transform[] route,bool gaze,bool freeze,bool puppet,bool probabilistic,bool threshold){if(go==null)return; var agent=go.GetComponent<NavMeshAgent>();if(agent==null)agent=go.AddComponent<NavMeshAgent>();agent.speed=1.5f;agent.radius=.35f;agent.height=1.8f;agent.baseOffset=0;var brain=go.GetComponent<AnimatronicUnityBrain>();if(brain!=null)Undo.DestroyObjectImmediate(brain);brain=go.AddComponent<AnimatronicUnityBrain>();brain.Configurar(role,route,gaze,freeze,puppet,false,probabilistic,threshold);}
    static void AddLight(Transform root,string name,Vector3 p,Color c,float range,float intensity){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Añadir iluminación");go.transform.SetParent(root,false);go.transform.position=p;var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.range=range;l.intensity=intensity;}
}
