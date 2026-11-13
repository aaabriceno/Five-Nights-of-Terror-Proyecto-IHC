// Lógica pura de la página del jugador 2: sin DOM ni red, para poder probarla
// con Node (probar_logica.js). En el navegador queda como window.Logica.
(function (raiz, fabrica) {
  if (typeof module === "object" && module.exports) module.exports = fabrica();
  else raiz.Logica = fabrica();
})(typeof self !== "undefined" ? self : this, function () {

  // Posición mostrada de un animatrónico. Se dibuja un poco "en el pasado"
  // (retrasoMs) para tener siempre dos muestras entre las que interpolar:
  // el movimiento sale continuo aunque lleguen 30 mensajes por segundo.
  // muestras: [{t, x, z}] ordenadas por t (milisegundos).
  function suavizar(muestras, ahoraMs, retrasoMs) {
    if (retrasoMs === undefined) retrasoMs = 60;
    if (!muestras || muestras.length === 0) return null;
    const objetivo = ahoraMs - retrasoMs;
    const primera = muestras[0];
    const ultima = muestras[muestras.length - 1];
    if (objetivo <= primera.t) return { x: primera.x, z: primera.z };
    if (objetivo >= ultima.t) return { x: ultima.x, z: ultima.z };
    for (let i = 1; i < muestras.length; i++) {
      const b = muestras[i];
      if (objetivo <= b.t) {
        const a = muestras[i - 1];
        const k = (objetivo - a.t) / (b.t - a.t);
        return { x: a.x + (b.x - a.x) * k, z: a.z + (b.z - a.z) * k };
      }
    }
    return { x: ultima.x, z: ultima.z };
  }

  // Guarda las últimas `maximo` latencias (ms) y resume: último, p50, p95, max.
  function crearMedidor(maximo) {
    if (maximo === undefined) maximo = 200;
    const valores = [];
    function percentil(ordenados, p) {
      const posicion = Math.ceil((p / 100) * ordenados.length) - 1;
      return ordenados[Math.max(0, Math.min(ordenados.length - 1, posicion))];
    }
    return {
      registrar(ms) {
        valores.push(ms);
        if (valores.length > maximo) valores.shift();
      },
      resumen() {
        if (valores.length === 0) return { n: 0, ultimo: 0, p50: 0, p95: 0, max: 0 };
        const ordenados = valores.slice().sort((a, b) => a - b);
        return {
          n: valores.length,
          ultimo: valores[valores.length - 1],
          p50: percentil(ordenados, 50),
          p95: percentil(ordenados, 95),
          max: ordenados[ordenados.length - 1],
        };
      },
    };
  }

  // ------------------------------------------------------------ terminal (tareas)
  // Unity inventa la tarea y comprueba la respuesta; aquí solo se calcula lo que
  // hace falta para dibujarla mientras el jugador 2 mueve los interruptores.

  // El circuito de una tarea con los interruptores puestos (0 o 1 por entrada,
  // en el orden de `tarea.entradas`; los que falten valen 0). Devuelve el valor
  // de cada entrada y puerta y el de la salida.
  function evaluarCircuito(tarea, interruptores) {
    const valores = {};
    tarea.entradas.forEach((nombre, i) => { valores[nombre] = !!(interruptores && interruptores[i]); });
    for (const p of tarea.puertas) {
      const a = valores[p.a];
      switch (p.op) {
        case "NOT": valores[p.id] = !a; break;
        case "AND": valores[p.id] = a && valores[p.b]; break;
        case "OR": valores[p.id] = a || valores[p.b]; break;
        default: valores[p.id] = a !== valores[p.b]; break;   // XOR
      }
    }
    return { valores, salida: valores[tarea.salida] };
  }

  // ¿Con esos interruptores la salida es la que pide la tarea?
  function circuitoResuelto(tarea, interruptores) {
    return evaluarCircuito(tarea, interruptores).salida === !!tarea.objetivo;
  }

  // Una puerta como se lee en voz alta: "G1 = A Y B", "G2 = NO C".
  function textoDePuerta(p) {
    switch (p.op) {
      case "NOT": return p.id + " = NO " + p.a;
      case "AND": return p.id + " = " + p.a + " Y " + p.b;
      case "OR": return p.id + " = " + p.a + " O " + p.b;
      default: return p.id + " = " + p.a + " O exclusivo " + p.b;
    }
  }

  // Lo que se teclea en un cálculo: un entero sin signo de hasta 6 cifras, o null.
  function leerRespuesta(texto) {
    const limpio = String(texto === undefined || texto === null ? "" : texto).trim();
    if (!/^[0-9]{1,6}$/.test(limpio)) return null;
    return Number(limpio);
  }

  // "×2", "×4", "×1,5": el factor de la multa sin decimales sobrantes.
  function formatearMulta(factor) {
    const n = Number(factor);
    return "×" + (Number.isInteger(n) ? String(n) : n.toFixed(1).replace(".", ","));
  }

  // "15", "7,5", "3,75": los segundos sin ceros de más.
  function formatearSegundos(segundos) {
    return String(Math.round(Number(segundos) * 100) / 100).replace(".", ",");
  }

  // Lo que el jugador 2 lee de un `efecto` de Unity, en segunda persona (el
  // mismo mensaje lo lee el jugador 1 en su tablet como "tu compañero..."). Las
  // palabras se arman aquí, con el tipo y los datos, no con el `message` de
  // Unity, que está escrito para el jugador 1. "" = no hay nada que decirle.
  function textoDeEfecto(m, nombres) {
    const nombre = (nombres && nombres[m.animatronic]) || "un animatrónico";
    switch (m.tipo || "ralentizado") {
      case "ralentizado":
        return "Retrasaste a " + nombre + (m.segundos > 0 ? " por " + Math.round(m.segundos) + " s" : "");
      case "trampa_colocada":
        return "Trampa puesta";
      case "trampa_disparada":
        return "Trampa: " + nombre + " atrapado";
      case "puppet_rescatado":
        if (m.segundos_caja > 0)
          return "Rescataste a Puppet · la caja del jugador 1 ahora dura " + formatearSegundos(m.segundos_caja) + " s";
        return "Rescataste a Puppet" + (m.multa_factor > 1
          ? " · la caja del jugador 1 se descarga " + formatearMulta(m.multa_factor) + " más rápido" : "");
      case "operador_conectado":
      case "operador_perdido":
        return "";
    }
    return m.message || "";
  }

  // La ruta de un animatrónico como texto: "P01 → P03 → P07".
  function textoDeRuta(puntos) {
    return Array.isArray(puntos) ? puntos.join(" → ") : "";
  }

  // Hacia dónde mira la cámara del jugador 1: 0 = al frente, negativo = izquierda.
  function textoDeCamara(yaw) {
    if (typeof yaw !== "number" || !isFinite(yaw)) return "–";
    if (Math.abs(yaw) < 5) return "Al frente";
    return (yaw < 0 ? "Izquierda " : "Derecha ") + Math.round(Math.abs(yaw)) + "°";
  }

  // El plano del edificio que manda Unity (salas, pasillos, ductos, muros...):
  // solo pasan las piezas con un tipo conocido y cuatro números finitos.
  const TIPOS_DEL_PLANO = ["sala", "oficina", "pasillo", "ducto", "muro", "tarima", "mesa", "caja"];
  function piezasValidas(plano) {
    if (!Array.isArray(plano)) return [];
    return plano.filter((p) => p && TIPOS_DEL_PLANO.includes(p.tipo) &&
      [p.x0, p.z0, p.x1, p.z1].every((v) => typeof v === "number" && isFinite(v)));
  }

  // Lo que debe caber en el mapa: todo el edificio y todos los puntos, con un margen (metros).
  function limitesDelMapa(puntos, plano, margen) {
    const xs = [], zs = [];
    for (const p of puntos || []) { xs.push(p.x); zs.push(p.z); }
    for (const p of piezasValidas(plano)) { xs.push(p.x0, p.x1); zs.push(p.z0, p.z1); }
    if (!xs.length) return null;
    return { x0: Math.min(...xs) - margen, x1: Math.max(...xs) + margen, z0: Math.min(...zs) - margen, z1: Math.max(...zs) + margen };
  }

  // "Escenario → Comedor → Pasillo": la ruta contada por las zonas del plano en
  // vez de por los nombres de los puntos (P01, P03...), que al jugador 2 no le
  // dicen nada. Sin plano, quedan los nombres de los puntos.
  function rutaPorZonas(nombres, puntos, plano) {
    const zonas = piezasValidas(plano).filter((p) => ["sala", "oficina", "pasillo", "ducto"].includes(p.tipo));
    if (!zonas.length) return textoDeRuta(nombres);
    const porNombre = {};
    for (const p of puntos || []) porNombre[p.nombre] = p;
    const partes = [];
    for (const nombre of nombres || []) {
      const p = porNombre[nombre];
      if (!p) continue;
      const zona = zonas.find((z) => p.x >= z.x0 && p.x <= z.x1 && p.z >= z.z0 && p.z <= z.z1);
      if (!zona) continue;
      const texto = zona.tipo === "ducto" ? "Ventilación" : aTitulo(zona.etiqueta || zona.tipo);
      if (partes[partes.length - 1] !== texto) partes.push(texto);
    }
    return partes.join(" → ");
  }

  function aTitulo(texto) {
    const t = String(texto).toLowerCase();
    return t.charAt(0).toUpperCase() + t.slice(1);
  }

  return { rutaPorZonas, piezasValidas, limitesDelMapa, suavizar, crearMedidor, evaluarCircuito, circuitoResuelto, textoDePuerta, leerRespuesta, textoDeEfecto, formatearMulta, textoDeRuta, textoDeCamara, formatearSegundos };
});
