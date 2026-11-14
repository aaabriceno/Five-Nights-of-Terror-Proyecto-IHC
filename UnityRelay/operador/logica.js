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
  // Unity inventa la tarea y comprueba la respuesta; aquí solo se dice qué es y
  // para qué sirve, porque el jugador 2 tiene que saber por qué la resuelve.
  const TEXTOS_DE_TAREA = {
    calculo: { titulo: "Terminal · recarga de trampa", porque: "Resuélvelo y ganas 1 carga de trampa." },
    ecuacion: {
      titulo: "Terminal · rescate de Puppet",
      porque: "Resuélvela para reiniciar la caja de música de Puppet: así salvas al jugador 1.",
    },
  };

  function textoDeTarea(tipo) {
    return TEXTOS_DE_TAREA[tipo] || { titulo: "Terminal", porque: "" };
  }

  // ------------------------------------------------------------ ductos
  // Copia del semáforo que el jugador 1 ve en la oficina: el color va también
  // en palabras, para no depender solo del color.
  const LUCES = { verde: "Verde", amarillo: "Amarillo", rojo: "Rojo" };

  function textoDeDucto(luz, cerrada) {
    if (!LUCES[luz]) return "–";
    return LUCES[luz] + (cerrada ? " · compuerta cerrada" : " · abierta");
  }

  // Un cálculo da 1 carga de trampa y 20 % de batería a las compuertas: solo
  // sobra con las dos cosas llenas. Sin dato de batería (Unity viejo) cuentan
  // solo las cargas; sin estado de recursos todavía no se bloquea.
  function puedeRecargar(estadoOperador, bateria) {
    if (!estadoOperador) return true;
    if (estadoOperador.cargas < estadoOperador.max_cargas) return true;
    return typeof bateria === "number" && bateria < 100;
  }

  // El aviso de un cálculo bien resuelto, según lo que había para ganar
  // (estado antes de que Unity lo sume).
  function textoDeRecarga(estadoOperador, bateria) {
    const partes = [];
    const cargasPorLlenar = !estadoOperador || estadoOperador.cargas < estadoOperador.max_cargas;
    const bateriaPorLlenar = typeof bateria === "number" && bateria < 100;
    if (cargasPorLlenar || !bateriaPorLlenar) partes.push("+1 carga de trampa");
    if (bateriaPorLlenar) partes.push("+20 % de batería");
    return "Correcto: " + partes.join(" y ");
  }

  // Lo que se teclea como respuesta: un entero sin signo de hasta 6 cifras, o null.
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

  return { textoDeDucto, puedeRecargar, textoDeRecarga, piezasValidas, limitesDelMapa, suavizar, crearMedidor, textoDeTarea, leerRespuesta, textoDeEfecto, formatearMulta, textoDeCamara, formatearSegundos };
});
