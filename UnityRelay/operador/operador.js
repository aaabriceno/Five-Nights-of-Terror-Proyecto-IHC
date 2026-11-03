// Página del jugador 2 (sala de control). Habla con Unity por el relay:
// manda operador_connect, ping_operador y accion_operador; recibe el mapa, las
// posiciones (20 por segundo), el estado del jugador 1 y los avisos.
// La lógica pura (suavizado, medidor) está en logica.js.
(function () {
  "use strict";

  const $ = (id) => document.getElementById(id);
  const COLORES = { freddy: "#6fcf6f", vixy: "#ff5a5a", puppet: "#d9a066" };
  const NOMBRES = { freddy: "Freddy", vixy: "Vixy", puppet: "Puppet" };
  const MOTIVOS = {
    sin_partida: "No hay una partida en curso.",
    enfriamiento: "Aún se está enfriando.",
    desconocido: "Animatrónico no válido.",
  };
  // Distancias (metros de la escena) a las que la señal del animatrónico falla.
  const SENAL_INESTABLE_M = 4;
  const SENAL_PERDIDA_M = 2;
  const MUESTRAS_POR_ANIMATRONICO = 8;
  const SEGUNDOS_ENTRE_LATIDOS = 2;

  const parametros = new URLSearchParams(location.search);
  $("host").value = parametros.get("host") ||
    (location.protocol.startsWith("http") && location.hostname ? location.hostname : "127.0.0.1");
  if (parametros.get("puerto")) $("puerto").value = parametros.get("puerto");

  const medidor = Logica.crearMedidor(200);
  let socket = null;
  let reintento = null;
  let quiereConexion = false;
  let latido = null;

  let puntos = [];
  let jugador = null;
  let limites = null;
  const muestras = {};                // nombre -> [{t, x, z}], t = hora local de llegada
  let estadoMapa = { animatronicos: [], recibidoEn: 0 };
  let estadoJugador = null;
  let avisoHasta = 0;

  // ------------------------------------------------------------- conexión
  function conectar() {
    clearTimeout(reintento);
    const direccion = `ws://${$("host").value}:${$("puerto").value}`;
    ponerEstado("Conectando…", "");
    socket = new WebSocket(direccion);
    socket.onopen = () => {
      socket.send(JSON.stringify({ type: "operador_connect" }));
      ponerEstado("Conectado", "ok");
      $("conectar").textContent = "Desconectar";
      enviarLatido();
      latido = setInterval(enviarLatido, SEGUNDOS_ENTRE_LATIDOS * 1000);
    };
    socket.onmessage = (evento) => recibir(evento.data);
    socket.onclose = () => {
      socket = null;
      clearInterval(latido);
      ponerEstado("Desconectado", "mal");
      $("conectar").textContent = "Conectar";
      // El juego puede abrirse después: se reintenta solo.
      if (quiereConexion) reintento = setTimeout(conectar, 3000);
    };
  }

  function ponerEstado(texto, clase) {
    $("estado-conexion").textContent = texto;
    $("estado-conexion").className = "estado " + clase;
  }

  function enviar(mensaje) {
    if (socket && socket.readyState === WebSocket.OPEN) socket.send(JSON.stringify(mensaje));
  }

  // El latido sirve para dos cosas: Unity sabe que sigues ahí y se mide la latencia.
  function enviarLatido() { enviar({ type: "ping_operador", t: Date.now() }); }

  $("conectar").onclick = () => {
    if (socket) { quiereConexion = false; socket.close(); }
    else { quiereConexion = true; conectar(); }
  };

  // --------------------------------------------------------- mensajes de Unity
  function recibir(crudo) {
    let m;
    try { m = JSON.parse(crudo); } catch (e) { return; }
    switch (m.type) {
      case "mapa_puntos":
        puntos = m.puntos || [];
        jugador = m.jugador || null;
        limites = null;
        $("mapa").dataset.estado = "listo";
        break;
      case "posiciones": {
        const ahora = performance.now();
        for (const a of m.animatronicos || []) {
          const lista = muestras[a.nombre] || (muestras[a.nombre] = []);
          lista.push({ t: ahora, x: a.x, z: a.z });
          if (lista.length > MUESTRAS_POR_ANIMATRONICO) lista.shift();
        }
        if (m.jugador) jugador = m.jugador;
        break;
      }
      case "estado_mapa":
        estadoMapa = { ...m, recibidoEn: performance.now() };
        $("noche").textContent = m.partida_activa ? m.noche : "–";
        $("hora").textContent = m.partida_activa ? m.hora : "sin partida";
        asegurarAmenazas();
        break;
      case "estado_jugador":
        estadoJugador = m;
        pintarJugador1();
        break;
      case "pong_operador":
        medidor.registrar(Math.max(0, Date.now() - m.t));
        pintarLatencia();
        break;
      case "efecto":
        avisar(`${m.message || (NOMBRES[m.animatronic] || "Un animatrónico") + " se ralentizó"}`);
        break;
      case "accion_rechazada":
        avisar(`${MOTIVOS[m.motivo] || "Acción rechazada."}${m.espera_s > 0 ? ` Faltan ${Math.ceil(m.espera_s)} s.` : ""}`);
        break;
    }
  }

  function avisar(texto) {
    const aviso = $("aviso");
    aviso.textContent = texto;
    aviso.classList.add("visible");
    avisoHasta = performance.now() + 4000;
  }

  // ----------------------------------------------------------- panel del jugador 1
  function pintarJugador1() {
    const e = estadoJugador;
    if (!e) return;
    const mirada = $("j1-mirada");
    mirada.textContent = e.mirando_pantalla ? "Mira la pantalla" : "No mira la pantalla";
    mirada.className = "valor " + (e.mirando_pantalla ? "" : "alerta");
    $("j1-tareas").textContent = String(e.tareas_pendientes);
    $("j1-tareas").className = "valor grande " + (e.tareas_pendientes >= 3 ? "peligro" : e.tareas_pendientes > 0 ? "alerta" : "");
    $("j1-caja").textContent = `${e.caja_pct} %${e.caja_en_peligro ? " · en peligro" : ""}`;
    $("j1-caja").className = "valor " + (e.caja_en_peligro ? "peligro" : "");
    $("j1-caja-barra").style.transform = `scaleX(${Math.max(0, Math.min(1, e.caja_pct / 100))})`;
    document.querySelector(".barra-caja").classList.toggle("peligro", !!e.caja_en_peligro);
  }

  function pintarLatencia() {
    const r = medidor.resumen();
    if (r.n === 0) return;
    $("latencia").textContent = `${r.ultimo} ms · p95 ${r.p95} ms`;
  }

  // --------------------------------------------------------------- amenazas
  function asegurarAmenazas() {
    const contenedor = $("lista-amenazas");
    for (const a of estadoMapa.animatronicos || []) {
      if ($("amenaza-" + a.nombre)) continue;
      const fila = document.createElement("div");
      fila.className = "amenaza";
      fila.id = "amenaza-" + a.nombre;
      fila.innerHTML =
        `<span class="nombre"><span class="punto" style="background:${COLORES[a.nombre] || "#ccc"}"></span>${NOMBRES[a.nombre] || a.nombre}</span>` +
        `<button type="button" id="boton-${a.nombre}">Retrasar</button>` +
        `<span class="detalle" id="detalle-${a.nombre}">–</span>`;
      contenedor.appendChild(fila);
      $("boton-" + a.nombre).onclick = () =>
        enviar({ type: "accion_operador", accion: "retrasar", objetivo: a.nombre });
    }
  }

  function posicionActual(nombre, ahora) {
    return Logica.suavizar(muestras[nombre] || [], ahora);
  }

  function distanciaAlJugador(p) {
    if (!p || !jugador) return null;
    return Math.hypot(p.x - jugador.x, p.z - jugador.z);
  }

  function pintarAmenazas(ahora) {
    for (const a of estadoMapa.animatronicos || []) {
      const fila = $("amenaza-" + a.nombre);
      if (!fila) continue;
      const pasado = (ahora - estadoMapa.recibidoEn) / 1000;
      const retraso = Math.max(0, a.retraso_s - pasado);
      const espera = Math.max(0, a.espera_s - pasado);
      const d = distanciaAlJugador(posicionActual(a.nombre, ahora));
      const texto =
        (d === null ? "" : `${d.toFixed(1)} m del jugador 1 · `) +
        (retraso > 0 ? `retrasado ${retraso.toFixed(0)} s` : espera > 0 ? `disponible en ${espera.toFixed(0)} s` : "listo");
      const detalle = $("detalle-" + a.nombre);
      if (detalle.textContent !== texto) detalle.textContent = texto;
      $("boton-" + a.nombre).disabled = !socket || espera > 0 || retraso > 0;
      fila.classList.toggle("sin-senal", d !== null && d < SENAL_PERDIDA_M);
    }
  }

  // ------------------------------------------------------------------ mapa
  const lienzo = $("mapa");
  const g = lienzo.getContext("2d");

  function ajustarLienzo() {
    const razon = window.devicePixelRatio || 1;
    const ancho = Math.round(lienzo.clientWidth * razon);
    const alto = Math.round(lienzo.clientHeight * razon);
    if (lienzo.width !== ancho || lienzo.height !== alto) { lienzo.width = ancho; lienzo.height = alto; }
    return razon;
  }

  function calcularLimites() {
    const xs = [], zs = [];
    for (const p of [...puntos, ...(jugador ? [jugador] : [])]) { xs.push(p.x); zs.push(p.z); }
    if (!xs.length) return null;
    const margen = 2;
    return { x0: Math.min(...xs) - margen, x1: Math.max(...xs) + margen, z0: Math.min(...zs) - margen, z1: Math.max(...zs) + margen };
  }

  function proyectar(p) {
    const L = limites, w = lienzo.width, h = lienzo.height;
    const escala = Math.min(w / (L.x1 - L.x0), h / (L.z1 - L.z0)) * 0.92;
    const dx = (w - escala * (L.x1 - L.x0)) / 2, dz = (h - escala * (L.z1 - L.z0)) / 2;
    return [dx + (p.x - L.x0) * escala, h - dz - (p.z - L.z0) * escala, escala];   // norte arriba
  }

  function dibujar(ahora) {
    const razon = ajustarLienzo();
    g.clearRect(0, 0, lienzo.width, lienzo.height);
    if (!limites) limites = calcularLimites();
    if (limites) {
      for (const nombre in muestras) {            // el mapa solo crece: no "baila" al moverse
        const p = posicionActual(nombre, ahora);
        if (!p) continue;
        limites.x0 = Math.min(limites.x0, p.x - 1); limites.x1 = Math.max(limites.x1, p.x + 1);
        limites.z0 = Math.min(limites.z0, p.z - 1); limites.z1 = Math.max(limites.z1, p.z + 1);
      }
    }
    dibujarCuadricula(razon);
    if (!limites || lienzo.dataset.estado !== "listo") {
      g.fillStyle = "#5f8f6d"; g.font = `${16 * razon}px ui-monospace, monospace`; g.textAlign = "center";
      g.fillText("ESPERANDO SEÑAL DEL JUEGO…", lienzo.width / 2, lienzo.height / 2);
      return;
    }
    g.font = `${11 * razon}px ui-monospace, monospace`;
    g.textAlign = "left";
    for (const p of puntos) {
      const [x, y] = proyectar(p);
      g.fillStyle = "#1f4a2d"; g.fillRect(x - 3 * razon, y - 3 * razon, 6 * razon, 6 * razon);
      g.fillStyle = "#3d7a50"; g.fillText(p.nombre, x + 7 * razon, y + 4 * razon);
    }
    if (jugador) {
      const [x, y, escala] = proyectar(jugador);
      g.strokeStyle = "rgba(125,255,154,.25)"; g.lineWidth = razon; g.setLineDash([4 * razon, 6 * razon]);
      for (const radio of [SENAL_PERDIDA_M, SENAL_INESTABLE_M]) {
        g.beginPath(); g.arc(x, y, radio * escala, 0, 7); g.stroke();
      }
      g.setLineDash([]);
      g.fillStyle = "#7dff9a"; g.fillRect(x - 8 * razon, y - 8 * razon, 16 * razon, 16 * razon);
      g.fillStyle = "#050807"; g.font = `bold ${10 * razon}px ui-monospace, monospace`; g.textAlign = "center";
      g.fillText("J1", x, y + 4 * razon);
    }
    g.textAlign = "left";
    for (const a of estadoMapa.animatronicos || []) {
      const p = posicionActual(a.nombre, ahora);
      if (!p) continue;
      dibujarAnimatronico(a, p, ahora, razon);
    }
  }

  function dibujarCuadricula(razon) {
    g.strokeStyle = "rgba(60,120,80,.12)"; g.lineWidth = razon;
    const paso = 48 * razon;
    g.beginPath();
    for (let x = 0; x < lienzo.width; x += paso) { g.moveTo(x, 0); g.lineTo(x, lienzo.height); }
    for (let y = 0; y < lienzo.height; y += paso) { g.moveTo(0, y); g.lineTo(lienzo.width, y); }
    g.stroke();
  }

  function dibujarAnimatronico(a, p, ahora, razon) {
    let [x, y] = proyectar(p);
    const d = distanciaAlJugador(p);
    const pasado = (ahora - estadoMapa.recibidoEn) / 1000;
    const lento = a.retraso_s - pasado > 0;
    const perdida = d !== null && d < SENAL_PERDIDA_M;
    const inestable = d !== null && d < SENAL_INESTABLE_M;
    if (inestable) {                               // la señal tiembla cuando está cerca
      x += (Math.random() - 0.5) * (perdida ? 10 : 4) * razon;
      y += (Math.random() - 0.5) * (perdida ? 10 : 4) * razon;
    }
    g.save();
    g.globalAlpha = perdida && Math.random() < 0.35 ? 0.15 : 1;
    g.shadowColor = COLORES[a.nombre] || "#ccc"; g.shadowBlur = 14 * razon;
    g.fillStyle = COLORES[a.nombre] || "#ccc";
    g.beginPath(); g.arc(x, y, 9 * razon, 0, 7); g.fill();
    g.shadowBlur = 0;
    if (lento) {
      g.strokeStyle = "#fff"; g.lineWidth = 2 * razon; g.setLineDash([4 * razon, 4 * razon]);
      g.beginPath(); g.arc(x, y, 15 * razon, 0, 7); g.stroke(); g.setLineDash([]);
    }
    g.fillStyle = "#e6ffe9"; g.font = `bold ${12 * razon}px ui-monospace, monospace`;
    g.fillText((NOMBRES[a.nombre] || a.nombre).toUpperCase() + (lento ? " · LENTO" : "") + (perdida ? " · SIN SEÑAL" : ""), x + 14 * razon, y + 4 * razon);
    g.restore();
  }

  // -------------------------------------------------------------- bucle
  function cuadro(ahora) {
    dibujar(ahora);
    pintarAmenazas(ahora);
    if (avisoHasta && ahora > avisoHasta) { $("aviso").classList.remove("visible"); avisoHasta = 0; }
    requestAnimationFrame(cuadro);
  }

  function pintarFecha() {
    const n = new Date();
    const dos = (v) => String(v).padStart(2, "0");
    $("fecha").textContent = `${dos(n.getDate())}/${dos(n.getMonth() + 1)} ${dos(n.getHours())}:${dos(n.getMinutes())}:${dos(n.getSeconds())}`;
  }
  setInterval(pintarFecha, 1000);
  pintarFecha();

  requestAnimationFrame(cuadro);
  if (parametros.get("auto")) $("conectar").click();
})();
