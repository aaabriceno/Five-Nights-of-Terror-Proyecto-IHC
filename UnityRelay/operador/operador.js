// Página del jugador 2 (sala de control). Habla con Unity por el relay:
// manda operador_connect, ping_operador y accion_operador; recibe el mapa, las
// posiciones (30 por segundo), el estado del jugador 1 y los avisos.
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
    sin_cargas: "No te quedan cargas: resuelve un cálculo en la terminal.",
    punto_ocupado: "Ya hay una trampa en ese punto.",
    punto_invalido: "Ese punto no existe en el mapa.",
    sin_habilidad: "Todavía no ganaste el rescate de Puppet.",
    sin_ventana: "No hay nada que rescatar ahora.",
    cargas_llenas: "Ya tienes todas las cargas: usa una trampa antes de recargar.",
    habilidad_lista: "El rescate de Puppet ya está desbloqueado.",
    bloqueado: "La terminal está bloqueada por un error.",
    tarea_desconocida: "Esa tarea no existe.",
    tarea_sin_pedir: "Esa tarea ya no está abierta: pídela de nuevo.",
    tarea_muy_rapida: "Demasiado rápido: lee bien el problema.",
  };
  // Distancias (metros de la escena) a las que la señal del animatrónico falla.
  const SENAL_INESTABLE_M = 4;
  const SENAL_PERDIDA_M = 2;
  const MUESTRAS_POR_ANIMATRONICO = 8;
  const SEGUNDOS_ENTRE_LATIDOS = 2;
  // Si se cae la conexión a mitad de la partida: 3 intentos, uno cada 5 s, en 15 s.
  // Pasado ese tiempo el relay tampoco lo deja entrar hasta que empiece otra partida.
  const VENTANA_PARA_VOLVER_MS = 15000;
  const MS_ENTRE_INTENTOS = 5000;
  const MAX_INTENTOS = 3;

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
  let rutas = [];                     // [{animatronico, puntos: [nombres]}]: la guía de cada animatrónico
  let plano = [];                     // el edificio visto desde arriba (lo manda Unity con la geometría real)
  let jugador = null;
  let camaraYaw = null;               // hacia dónde mira el jugador 1 (grados; 0 = al frente); null = no se sabe
  let limites = null;
  const muestras = {};                // nombre -> [{t, x, z}], t = hora local de llegada
  let estadoMapa = { animatronicos: [], recibidoEn: 0 };
  let estadoJugador = null;
  let estadoOperador = null;
  let bloqueadoHasta = 0;              // hasta cuándo (performance.now) está bloqueada la terminal
  let terminalBloqueada = false;
  let siguienteId = 1;
  let ventanaDeRescateHasta = 0;      // hora local (performance.now) en que se cierra la ventana
  let secuencia = [];
  let entrada = [];
  let temporizadoresDeSecuencia = [];
  const lienzo = $("mapa");
  const g = lienzo.getContext("2d");
  let avisoHasta = 0;
  let finDePartida = false;           // Unity mandó game_over: no hay nada más que hacer en esta partida
  let accionPrimaria = "esperar";     // qué hace el botón grande de la pantalla de cierre
  let estuvoConectado = false;        // ya hubo una conexión buena: una caída abre la ventana para volver
  let caidaEn = 0;                    // performance.now() de la caída; 0 si no hay una abierta
  let intentos = 0;
  let temporizadorDeVuelta = null;
  let temporizadorDeIntento = null;

  // ------------------------------------------------------------- conexión
  function conectar() {
    clearTimeout(reintento);
    clearTimeout(temporizadorDeIntento);
    if (socket) { const viejo = socket; socket = null; viejo.close(); }
    const direccion = `ws://${$("host").value}:${$("puerto").value}`;
    ponerEstado(caidaEn ? "Reconectando…" : "Conectando…", "");
    const esteSocket = new WebSocket(direccion);
    socket = esteSocket;
    // Un intento que no abre en 5 s se da por fallido.
    temporizadorDeIntento = setTimeout(() => {
      if (esteSocket.readyState !== WebSocket.OPEN) esteSocket.close();
    }, MS_ENTRE_INTENTOS);
    esteSocket.onopen = () => {
      if (socket !== esteSocket) { esteSocket.close(); return; }
      clearTimeout(temporizadorDeIntento);
      clearTimeout(temporizadorDeVuelta);
      const volvio = caidaEn > 0;
      caidaEn = 0; intentos = 0; estuvoConectado = true;
      $("reconectando").hidden = true;
      esteSocket.send(JSON.stringify({ type: "operador_connect" }));
      ponerEstado("Conectado", "ok");
      $("conectar").textContent = "Desconectar";
      actualizarBotones();
      enviarLatido();
      latido = setInterval(enviarLatido, SEGUNDOS_ENTRE_LATIDOS * 1000);
      if (volvio) avisar("Volviste a la partida");
    };
    esteSocket.onmessage = (evento) => { if (socket === esteSocket) recibir(evento.data); };
    esteSocket.onclose = () => {
      clearTimeout(temporizadorDeIntento);
      if (socket !== esteSocket) return;          // un intento viejo que ya se reemplazó
      socket = null;
      clearInterval(latido);
      ponerEstado("Desconectado", "mal");
      $("conectar").textContent = "Conectar";
      actualizarBotones();
      cerrarMiniTarea();
      salirDelModoTrampa();
      if (!quiereConexion) return;
      if (estuvoConectado) {
        // Se cayó a mitad de la partida: ventana de 15 s para volver.
        if (!caidaEn) { caidaEn = performance.now(); intentos = 0; }
        $("conectar").textContent = "Cancelar";
        siguientePasoDeVuelta();
      } else {
        // El juego puede abrirse después: se reintenta solo.
        reintento = setTimeout(conectar, 3000);
      }
    };
  }

  // Intento 1 al caerse, el 2 a los 5 s, el 3 a los 10 s; a los 15 s se rinde.
  function siguientePasoDeVuelta() {
    clearTimeout(temporizadorDeVuelta);
    if (!quiereConexion || !caidaEn) return;
    const transcurrido = performance.now() - caidaEn;
    if (transcurrido >= VENTANA_PARA_VOLVER_MS) { rendirse(); return; }
    if (intentos < MAX_INTENTOS && transcurrido >= intentos * MS_ENTRE_INTENTOS) {
      intentos++;
      conectar();
    }
    const proximo = intentos < MAX_INTENTOS ? intentos * MS_ENTRE_INTENTOS : VENTANA_PARA_VOLVER_MS;
    temporizadorDeVuelta = setTimeout(siguientePasoDeVuelta, Math.max(0, proximo - (performance.now() - caidaEn)));
  }

  function rendirse() {
    clearTimeout(temporizadorDeVuelta);
    clearTimeout(temporizadorDeIntento);
    caidaEn = 0; quiereConexion = false; estuvoConectado = false;
    if (socket) { const viejo = socket; socket = null; viejo.close(); }
    $("reconectando").hidden = true;
    $("conectar").textContent = "Conectar";
    ponerEstado("Desconectado", "mal");
    actualizarBotones();
    mostrarCierre("SIN CONEXIÓN CON EL JUEGO",
      "Pasaron 15 s sin poder volver. La partida siguió sin ti.", "", false, "reconectar");
  }

  // El jugador 2 se va a propósito: el relay no lo toma como una caída.
  function desconectarAPropósito() {
    quiereConexion = false; estuvoConectado = false; caidaEn = 0;
    clearTimeout(reintento); clearTimeout(temporizadorDeVuelta); clearTimeout(temporizadorDeIntento);
    $("reconectando").hidden = true;
    if (socket) { enviar({ type: "operador_salir" }); socket.close(); }
    else { $("conectar").textContent = "Conectar"; ponerEstado("Desconectado", "mal"); }
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
    if (socket || caidaEn) desconectarAPropósito();      // «Desconectar» o «Cancelar» la reconexión
    else { quiereConexion = true; conectar(); }
  };

  // --------------------------------------------------------- mensajes de Unity
  function recibir(crudo) {
    let m;
    try { m = JSON.parse(crudo); } catch (e) { return; }
    // Terminada la partida solo importa lo que empieza una nueva (mapa_puntos),
    // el eco de la latencia y un segundo game_over; el resto llega tarde.
    if (finDePartida && m.type !== "mapa_puntos" && m.type !== "pong_operador" && m.type !== "game_over") return;
    switch (m.type) {
      case "game_over":
        mostrarFin(m);
        break;
      case "jugador1_salio":
        if (m.motivo === "conexion_perdida") {
          // Se quedó sin Wi-Fi: la tablet reintenta sola y retoma la noche, así que
          // la partida sigue; solo se avisa (no se cierra nada).
          $("j1-sin-conexion").hidden = false;
          break;
        }
        mostrarCierre("EL JUGADOR 1 SE FUE DE LA PARTIDA",
          m.motivo === "salio" ? "Salió al menú." : "Perdió la conexión.",
          "Sin él la partida no continúa.", false, "esperar");
        break;
      case "jugador1_volvio":
        $("j1-sin-conexion").hidden = true;
        break;
      case "juego_cerrado":
        mostrarCierre("EL JUEGO SE CERRÓ", "Unity ya no está conectado.", "Sin juego no hay partida.", false, "esperar");
        break;
      case "operador_rechazado":
        quiereConexion = false; estuvoConectado = false;
        mostrarCierre("NO PUEDES UNIRTE AHORA",
          "Pasó el tiempo para volver a esta partida.", "Podrás entrar cuando empiece la siguiente noche.", false, "reconectar");
        break;
      case "mapa_puntos":
        if (finDePartida) reiniciarVista();          // empezó otra noche
        $("j1-sin-conexion").hidden = true;
        puntos = m.puntos || [];
        rutas = m.rutas || [];
        plano = Logica.piezasValidas(m.plano);
        $("mapa").dataset.plano = String(plano.length);
        jugador = m.jugador || null;
        limites = null;
        pintarRutas();
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
        if (typeof m.camara_yaw === "number") {
          camaraYaw = m.camara_yaw;
          const texto = Logica.textoDeCamara(camaraYaw);
          if ($("j1-camara").textContent !== texto) $("j1-camara").textContent = texto;
        }
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
      case "estado_operador":
        estadoOperador = m;
        bloqueadoHasta = performance.now() + (m.bloqueo_s || 0) * 1000;
        pintarJugador1();      // la multa de la caja vive en este mensaje
        ventanaDeRescateHasta = m.ventana_rescate_s > 0 ? performance.now() + m.ventana_rescate_s * 1000 : 0;
        pintarRecursos();
        break;
      case "pong_operador":
        medidor.registrar(Math.max(0, Date.now() - m.t));
        pintarLatencia();
        break;
      case "efecto": {
        // El texto se arma aquí, para el jugador 2 ("Retrasaste a Freddy"); el
        // `message` de Unity está escrito para el jugador 1.
        const texto = Logica.textoDeEfecto(m, NOMBRES);
        if (texto) avisar(texto);
        break;
      }
      case "accion_rechazada":
        avisar(`${MOTIVOS[m.motivo] || "Acción rechazada."}${m.espera_s > 0 ? ` Faltan ${Math.ceil(m.espera_s)} s.` : ""}`);
        // Demasiado rápido: la tarea sigue abierta. Cualquier otro rechazo de la terminal la cierra.
        if (m.objetivo === "tarea" && m.motivo !== "tarea_muy_rapida") cerrarMiniTarea();
        break;
      case "tarea":
        mostrarTarea(m);
        break;
      case "tarea_resultado":
        resultadoDeTarea(m);
        break;
    }
  }

  // ------------------------------------------------------- fin de la partida
  const TITULOS_FIN = {
    loss: ["EL JUGADOR 1 FUE ATRAPADO", false],
    win: ["EL JUGADOR 1 SOBREVIVIÓ LA NOCHE", true],
    final_victory: ["EL JUGADOR 1 SOBREVIVIÓ TODAS LAS NOCHES", true],
  };

  function mostrarFin(m) {
    const [texto, gano] = TITULOS_FIN[m.result] || ["LA PARTIDA TERMINÓ", false];
    const s = m.final_stats || {};
    const minutos = Math.floor((s.duration || 0) / 60), segundos = (s.duration || 0) % 60;
    mostrarCierre("FIN DE LA PARTIDA",
      `${texto}${m.night ? ` · NOCHE ${m.night}` : ""}`,
      `Duración ${minutos}:${String(segundos).padStart(2, "0")} · tareas hechas ${s.tasks_completed || 0} · falladas ${s.tasks_failed || 0}`,
      gano, "esperar");
  }

  // Pantalla que tapa el monitor cuando ya no hay partida que ayudar: fin de la
  // noche, el jugador 1 o el juego se fueron, o la conexión se perdió.
  function mostrarCierre(titulo, detalle, estadisticas, gano, accion) {
    finDePartida = true;
    $("j1-sin-conexion").hidden = true;
    accionPrimaria = accion;
    ventanaDeRescateHasta = 0;
    cerrarMiniTarea();
    salirDelModoTrampa();
    $("fin-partida").classList.toggle("gano", gano);
    $("fin-titulo").textContent = titulo;
    $("fin-detalle").textContent = detalle;
    $("fin-estadisticas").textContent = estadisticas;
    $("fin-esperar").textContent = accion === "reconectar" ? "Reconectar" : "Esperar otra partida";
    $("fin-partida").hidden = false;
    $("alerta-puppet").hidden = true;
    $("aviso").classList.remove("visible");
    actualizarBotones();
    pintarAmenazas(performance.now());       // sin esperar al siguiente cuadro
    pintarRescate(performance.now());
    $("fin-esperar").focus();
  }

  // Vuelve al estado de "esperando señal", como al abrir la página.
  function reiniciarVista() {
    finDePartida = false;
    $("fin-partida").hidden = true;
    for (const nombre in muestras) delete muestras[nombre];
    puntos = []; rutas = []; plano = []; jugador = null; camaraYaw = null; limites = null;
    $("j1-sin-conexion").hidden = true;
    estadoMapa = { animatronicos: [], recibidoEn: 0 };
    estadoJugador = null; estadoOperador = null;
    ventanaDeRescateHasta = 0;
    $("mapa").dataset.estado = "esperando";
    $("noche").textContent = "–"; $("hora").textContent = "–";
    for (const id of ["j1-mirada", "j1-camara", "j1-tareas", "j1-caja", "cargas", "habilidad"]) $(id).textContent = "–";
    for (const id of ["j1-mirada", "j1-camara", "j1-tareas", "j1-caja", "habilidad"]) $(id).className = "valor";
    $("j1-caja-barra").style.transform = "scaleX(0)";
    $("lista-amenazas").textContent = "";
    actualizarBotones();
  }

  $("fin-esperar").onclick = () => {
    const volver = accionPrimaria === "reconectar";
    reiniciarVista();
    if (volver) { quiereConexion = true; estuvoConectado = false; caidaEn = 0; conectar(); }
  };
  $("fin-salir").onclick = () => {
    reiniciarVista();
    desconectarAPropósito();
  };

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
    const multa = estadoOperador && estadoOperador.multa_factor > 1
      ? (estadoOperador.segundos_caja > 0
        ? ` · dura ${Logica.formatearSegundos(estadoOperador.segundos_caja)} s`
        : ` · se descarga ${Logica.formatearMulta(estadoOperador.multa_factor)} más rápido`) : "";
    $("j1-caja").textContent = `${e.caja_pct} %${e.caja_en_peligro ? " · en peligro" : ""}${multa}`;
    $("j1-caja").className = "valor " + (e.caja_en_peligro ? "peligro" : "");
    $("j1-caja-barra").style.transform = `scaleX(${Math.max(0, Math.min(1, e.caja_pct / 100))})`;
    document.querySelector(".barra-caja").classList.toggle("peligro", !!e.caja_en_peligro);
  }

  // ------------------------------------------------------ recursos y trampas
  function pintarRecursos() {
    const e = estadoOperador;
    if (!e) return;
    $("cargas").textContent = `${e.cargas} / ${e.max_cargas}`;
    $("habilidad").textContent = e.habilidad_puppet ? "Lista" : "Resuelve el circuito";
    $("habilidad").className = "valor " + (e.habilidad_puppet ? "alerta" : "");
    actualizarBotones();
    if (e.cargas <= 0) salirDelModoTrampa();
  }

  function actualizarBotones() {
    const e = estadoOperador;
    $("boton-trampa").disabled = !socket || finDePartida || !e || e.cargas <= 0;
    const bloqueada = performance.now() < bloqueadoHasta;
    $("boton-minitarea").disabled = !socket || finDePartida || bloqueada || !!(e && e.cargas >= e.max_cargas);
    $("boton-circuito").disabled = !socket || finDePartida || bloqueada || !!(e && e.habilidad_puppet);
  }

  // La terminal se bloquea unos segundos tras un error. La cuenta corre aquí
  // (Unity manda lo que falta en `bloqueo_s`) y los botones lo dicen.
  function pintarBloqueo(ahora) {
    const restante = Math.max(0, (bloqueadoHasta - ahora) / 1000);
    const bloqueada = restante > 0;
    if (bloqueada === terminalBloqueada) return;
    terminalBloqueada = bloqueada;
    $("boton-minitarea").textContent = bloqueada ? "Terminal bloqueada…" : "Recargar (cálculo)";
    $("boton-circuito").textContent = bloqueada ? "Terminal bloqueada…" : "Desbloquear rescate (circuito)";
    actualizarBotones();
  }

  // Cuando Puppet sale de la caja hay unos segundos para rescatarlo (si ya
  // ganaste la habilidad). La cuenta atrás corre aquí, sin esperar a Unity.
  function pintarRescate(ahora) {
    const restante = Math.max(0, (ventanaDeRescateHasta - ahora) / 1000);
    const alerta = $("alerta-puppet");
    const abierta = restante > 0;
    if (alerta.hidden === abierta) alerta.hidden = !abierta;
    const e = estadoOperador;
    if (abierta) {
      const texto = e && e.habilidad_puppet
        ? `Pulsa «Rescatar a Puppet»: te quedan ${restante.toFixed(1)} s`
        : "Aún no desbloqueaste el rescate: resuelve el circuito de la terminal";
      if ($("alerta-puppet-detalle").textContent !== texto) $("alerta-puppet-detalle").textContent = texto;
    }
    $("boton-rescate").disabled = !socket || finDePartida || !abierta || !(e && e.habilidad_puppet);
  }

  $("boton-rescate").onclick = () =>
    enviar({ type: "accion_operador", accion: "rescatar_puppet", id: siguienteId++ });

  function entrarAlModoTrampa() {
    lienzo.dataset.modo = "trampa";
    $("boton-trampa").classList.add("activo");
    $("boton-trampa").textContent = "Elige un punto del mapa (Esc para cancelar)";
  }

  function salirDelModoTrampa() {
    delete lienzo.dataset.modo;
    $("boton-trampa").classList.remove("activo");
    $("boton-trampa").textContent = "Colocar trampa";
  }

  $("boton-trampa").onclick = () => {
    if (lienzo.dataset.modo === "trampa") salirDelModoTrampa();
    else entrarAlModoTrampa();
  };

  document.addEventListener("keydown", (evento) => {
    if (evento.key !== "Escape") return;
    salirDelModoTrampa();
    cerrarMiniTarea();
  });

  // Un clic en el mapa en modo trampa elige el punto más cercano (a 28 px o menos).
  lienzo.addEventListener("click", (evento) => {
    if (lienzo.dataset.modo !== "trampa" || !limites) return;
    const caja = lienzo.getBoundingClientRect();
    const razon = lienzo.width / caja.width;
    const cx = (evento.clientX - caja.left) * razon, cy = (evento.clientY - caja.top) * razon;
    let mejor = null, mejorDistancia = 28 * razon;
    for (const p of puntos) {
      const [x, y] = proyectar(p);
      const d = Math.hypot(x - cx, y - cy);
      if (d < mejorDistancia) { mejor = p; mejorDistancia = d; }
    }
    if (!mejor) { avisar("Elige uno de los puntos del mapa."); return; }
    enviar({ type: "accion_operador", accion: "trampa", punto: mejor.nombre, id: siguienteId++ });
    salirDelModoTrampa();
  });

  // ------------------------------------------------- terminal: tareas que dan poderes
  // Unity inventa la tarea (`tarea`) y comprueba la respuesta (`tarea_resultado`);
  // aquí solo se muestra, se mueven los interruptores y se manda lo que el jugador 2 contesta.
  let tareaActual = null;
  let interruptores = [];

  function pedirTarea(tipo) {
    if (!socket || finDePartida) return;
    if (performance.now() < bloqueadoHasta) {
      avisar(`Terminal bloqueada: faltan ${Math.ceil((bloqueadoHasta - performance.now()) / 1000)} s.`);
      return;
    }
    cerrarMiniTarea();
    $("minitarea").hidden = false;
    $("mt-titulo").textContent = "Terminal";
    $("mt-enunciado").textContent = "Conectando con la terminal…";
    $("mt-mensaje").textContent = "";
    $("mt-calculo").hidden = true;
    $("mt-circuito").hidden = true;
    enviar({ type: "tarea_pedir", tipo });
  }

  function mostrarTarea(m) {
    if ($("minitarea").hidden) return;      // la cerró antes de que llegara
    tareaActual = m;
    $("mt-mensaje").textContent = "";
    const esCalculo = m.tipo === "calculo";
    $("mt-titulo").textContent = esCalculo ? "Terminal · recarga de trampa" : "Terminal · circuito del rescate";
    $("mt-calculo").hidden = !esCalculo;
    $("mt-circuito").hidden = esCalculo;
    if (esCalculo) {
      $("mt-enunciado").textContent = m.enunciado;
      $("mt-respuesta").value = "";
      $("mt-respuesta").focus();
      return;
    }
    $("mt-enunciado").textContent =
      `Mueve los interruptores hasta que la salida ${m.salida} quede en ${m.objetivo ? 1 : 0}.`;
    interruptores = m.entradas.map(() => 0);
    const lista = $("mt-puertas");
    lista.textContent = "";
    for (const p of m.puertas) {
      const fila = document.createElement("li");
      fila.dataset.id = p.id;
      const texto = document.createElement("span");
      texto.textContent = Logica.textoDePuerta(p);
      const valor = document.createElement("span");
      valor.className = "valor-puerta";
      fila.append(texto, valor);
      lista.append(fila);
    }
    const caja = $("mt-interruptores");
    caja.textContent = "";
    m.entradas.forEach((nombre, i) => {
      const boton = document.createElement("button");
      boton.type = "button";
      boton.setAttribute("role", "switch");
      boton.setAttribute("aria-checked", "false");
      boton.dataset.indice = String(i);
      boton.onclick = () => { interruptores[i] = interruptores[i] ? 0 : 1; pintarCircuito(); };
      caja.append(boton);
    });
    pintarCircuito();
  }

  // Cada puerta y el foco se actualizan al mover un interruptor: así se ve qué pasa.
  function pintarCircuito() {
    if (!tareaActual || tareaActual.tipo !== "circuito") return;
    const { valores, salida } = Logica.evaluarCircuito(tareaActual, interruptores);
    document.querySelectorAll("#mt-interruptores button").forEach((boton, i) => {
      boton.setAttribute("aria-checked", interruptores[i] ? "true" : "false");
      boton.textContent = `${tareaActual.entradas[i]} = ${interruptores[i]}`;
    });
    document.querySelectorAll("#mt-puertas li").forEach((fila) => {
      const v = valores[fila.dataset.id];
      fila.classList.toggle("encendida", !!v);
      fila.lastChild.textContent = v ? "1" : "0";
    });
    const resuelto = Logica.circuitoResuelto(tareaActual, interruptores);
    $("mt-foco").classList.toggle("resuelto", resuelto);
    $("mt-foco").textContent = resuelto
      ? `Salida ${tareaActual.salida} = ${salida ? 1 : 0}: es lo que se pide. Envíalo.`
      : `Salida ${tareaActual.salida} = ${salida ? 1 : 0}: se pide ${tareaActual.objetivo ? 1 : 0}.`;
  }

  function cerrarMiniTarea() {
    tareaActual = null;
    $("minitarea").hidden = true;
  }

  function resultadoDeTarea(m) {
    cerrarMiniTarea();
    if (m.correcto) {
      avisar(m.tipo === "calculo" ? "Correcto: +1 carga de trampa" : "Circuito resuelto: rescate de Puppet desbloqueado");
      return;
    }
    bloqueadoHasta = performance.now() + (m.bloqueo_s || 0) * 1000;
    avisar(`Incorrecto: la terminal se bloquea ${Math.ceil(m.bloqueo_s || 0)} s.`);
    pintarBloqueo(performance.now());
  }

  $("mt-calculo").onsubmit = (evento) => {
    evento.preventDefault();
    if (!tareaActual || tareaActual.tipo !== "calculo") return;
    const valor = Logica.leerRespuesta($("mt-respuesta").value);
    if (valor === null) { $("mt-mensaje").textContent = "Escribe solo un número entero."; return; }
    enviar({ type: "tarea_respuesta", id: tareaActual.id, valor });
  };

  $("mt-enviar-circuito").onclick = () => {
    if (!tareaActual || tareaActual.tipo !== "circuito") return;
    enviar({ type: "tarea_respuesta", id: tareaActual.id, interruptores: interruptores.slice() });
  };

  $("boton-minitarea").onclick = () => pedirTarea("calculo");
  $("boton-circuito").onclick = () => pedirTarea("circuito");
  $("mt-cerrar").onclick = cerrarMiniTarea;

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
      // Se arma con createElement y textContent, nunca con innerHTML: el nombre
      // viene por la red y un texto con etiquetas HTML se mostraría tal cual en
      // vez de ejecutarse en el navegador del jugador 2.
      const fila = document.createElement("div");
      fila.className = "amenaza";
      fila.id = "amenaza-" + a.nombre;
      const nombre = document.createElement("span");
      nombre.className = "nombre";
      const punto = document.createElement("span");
      punto.className = "punto";
      punto.style.background = COLORES[a.nombre] || "#ccc";
      nombre.append(punto, NOMBRES[a.nombre] || a.nombre);
      const boton = document.createElement("button");
      boton.type = "button";
      boton.id = "boton-" + a.nombre;
      boton.textContent = "Retrasar";
      boton.onclick = () => enviar({ type: "accion_operador", accion: "retrasar", objetivo: a.nombre });
      const detalle = document.createElement("span");
      detalle.className = "detalle";
      detalle.id = "detalle-" + a.nombre;
      detalle.textContent = "–";
      const ruta = document.createElement("span");
      ruta.className = "ruta";
      ruta.id = "ruta-" + a.nombre;
      fila.append(nombre, boton, detalle, ruta);
      contenedor.appendChild(fila);
    }
    pintarRutas();
  }

  // La guía: por dónde va cada animatrónico. El jugador 1 no la tiene; el
  // jugador 2 la usa para decidir dónde poner trampas y a quién retrasar. Los
  // desvíos ocasionales no salen aquí: son la sorpresa.
  function pintarRutas() {
    for (const r of rutas) {
      const fila = $("ruta-" + r.animatronico);
      if (fila) fila.textContent = "Ruta: " + Logica.rutaPorZonas(r.puntos, puntos, plano);
    }
  }

  function dibujarRutas(razon) {
    const porNombre = {};
    for (const p of puntos) porNombre[p.nombre] = p;
    g.save();
    g.lineWidth = 2 * razon; g.lineCap = "round";
    for (const r of rutas) {
      const color = COLORES[r.animatronico] || "#ccc";
      const camino = (r.puntos || []).map((n) => porNombre[n]).filter(Boolean);
      if (camino.length < 2) continue;
      g.strokeStyle = color; g.fillStyle = color; g.globalAlpha = 0.5;
      g.setLineDash([9 * razon, 6 * razon]);
      g.beginPath();
      camino.forEach((p, i) => { const [x, y] = proyectar(p); if (i === 0) g.moveTo(x, y); else g.lineTo(x, y); });
      g.stroke();
      g.setLineDash([]);
      for (let i = 1; i < camino.length; i++) {          // una flecha en medio de cada tramo
        const [x0, y0] = proyectar(camino[i - 1]), [x1, y1] = proyectar(camino[i]);
        const mx = (x0 + x1) / 2, my = (y0 + y1) / 2, ang = Math.atan2(y1 - y0, x1 - x0), t = 6 * razon;
        g.beginPath();
        g.moveTo(mx + Math.cos(ang) * t, my + Math.sin(ang) * t);
        g.lineTo(mx + Math.cos(ang + 2.5) * t, my + Math.sin(ang + 2.5) * t);
        g.lineTo(mx + Math.cos(ang - 2.5) * t, my + Math.sin(ang - 2.5) * t);
        g.closePath(); g.fill();
      }
      const [ex, ey] = proyectar(camino[camino.length - 1]);   // el penúltimo punto: por donde entra a atacar
      g.globalAlpha = 0.9; g.lineWidth = 2 * razon;
      g.beginPath(); g.arc(ex, ey, 11 * razon, 0, 7); g.stroke();
    }
    g.restore();
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
      $("boton-" + a.nombre).disabled = !socket || finDePartida || espera > 0 || retraso > 0;
      fila.classList.toggle("sin-senal", d !== null && d < SENAL_PERDIDA_M);
    }
  }

  // ------------------------------------------------------------------ mapa
  function ajustarLienzo() {
    const razon = window.devicePixelRatio || 1;
    const ancho = Math.round(lienzo.clientWidth * razon);
    const alto = Math.round(lienzo.clientHeight * razon);
    if (lienzo.width !== ancho || lienzo.height !== alto) { lienzo.width = ancho; lienzo.height = alto; }
    return razon;
  }

  function calcularLimites() {
    return Logica.limitesDelMapa([...puntos, ...(jugador ? [jugador] : [])], plano, plano.length ? 1 : 2);
  }

  // El plano del edificio en líneas, como el bosquejo del mapa: paredes (los
  // huecos son las puertas), ventilación punteada, tarima, mesas con sus sillas,
  // la caja de música y el nombre de cada zona. Sin rellenos fuertes: es la guía.
  const COLOR_DEL_PLANO = "rgba(125,255,154,.55)";
  const COLOR_DE_LOS_MUEBLES = "rgba(125,255,154,.3)";
  function dibujarPlano(razon) {
    if (!plano.length) return;
    g.save();
    for (const p of plano) {
      const [ax, ay, escala] = proyectar({ x: p.x0, z: p.z1 });
      const [bx, by] = proyectar({ x: p.x1, z: p.z0 });
      const ancho = bx - ax, alto = by - ay;
      switch (p.tipo) {
        case "muro":
          g.fillStyle = COLOR_DEL_PLANO;
          g.fillRect(ax, ay, Math.max(ancho, 2 * razon), Math.max(alto, 2 * razon));
          break;
        case "ducto":
          g.strokeStyle = COLOR_DE_LOS_MUEBLES; g.lineWidth = razon; g.setLineDash([5 * razon, 4 * razon]);
          g.strokeRect(ax, ay, ancho, alto);
          g.setLineDash([]);
          break;
        case "tarima":
          g.strokeStyle = COLOR_DE_LOS_MUEBLES; g.lineWidth = razon;
          g.strokeRect(ax, ay, ancho, alto);
          break;
        case "mesa": {
          const cx = ax + ancho / 2, cy = ay + alto / 2, radio = Math.min(ancho, alto) / 2;
          g.strokeStyle = COLOR_DE_LOS_MUEBLES; g.lineWidth = razon;
          g.beginPath(); g.arc(cx, cy, radio, 0, 7); g.stroke();
          g.fillStyle = COLOR_DE_LOS_MUEBLES;               // cuatro sillas alrededor
          for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
            g.beginPath(); g.arc(cx + dx * (radio + 0.45 * escala), cy + dy * (radio + 0.45 * escala), 0.22 * escala, 0, 7); g.fill();
          }
          break;
        }
        case "caja":
          g.strokeStyle = "rgba(217,160,102,.7)"; g.lineWidth = razon;
          g.strokeRect(ax, ay, ancho, alto);
          g.fillStyle = "rgba(217,160,102,.8)"; g.font = `${10 * razon}px ui-monospace, monospace`; g.textAlign = "center";
          g.fillText("♪ caja de música", ax + ancho / 2, by + 12 * razon);
          break;
      }
    }
    g.fillStyle = "rgba(125,255,154,.45)";
    g.font = `bold ${11 * razon}px ui-monospace, monospace`; g.textAlign = "center";
    for (const p of plano) {
      if (!p.etiqueta) continue;
      const [ax, ay] = proyectar({ x: p.x0, z: p.z1 });
      const [bx, by] = proyectar({ x: p.x1, z: p.z0 });
      const vertical = (by - ay) > 2.5 * (bx - ax);       // pasillos largos: el nombre de costado
      g.save();
      g.translate((ax + bx) / 2, vertical ? (ay + by) / 2 : ay + 14 * razon);
      if (vertical) g.rotate(-Math.PI / 2);
      g.fillText(p.etiqueta, 0, 0);
      g.restore();
    }
    g.restore();
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
    dibujarPlano(razon);
    dibujarRutas(razon);
    g.font = `${11 * razon}px ui-monospace, monospace`;
    g.textAlign = "left";
    // Los puntos de ruta (P01, V03...) no se muestran: al jugador 2 le sirve el
    // plano, no los nombres internos. Solo se marcan al elegir dónde va una trampa.
    if (!plano.length) {
      for (const p of puntos) {                       // sin plano (Unity viejo): como antes
        const [x, y] = proyectar(p);
        g.fillStyle = "#1f4a2d"; g.fillRect(x - 3 * razon, y - 3 * razon, 6 * razon, 6 * razon);
        g.fillStyle = "#3d7a50"; g.fillText(p.nombre, x + 7 * razon, y + 4 * razon);
      }
    }
    const trampas = (estadoOperador && estadoOperador.trampas) || [];
    const enModoTrampa = lienzo.dataset.modo === "trampa";
    for (const p of puntos) {
      const [x, y] = proyectar(p);
      if (enModoTrampa) {                              // todos los puntos se marcan como elegibles
        g.strokeStyle = "#ffd24a"; g.lineWidth = 1.5 * razon;
        g.beginPath(); g.arc(x, y, 12 * razon, 0, 7); g.stroke();
      }
      if (trampas.includes(p.nombre)) {                // trampa puesta: una cruz amarilla
        g.strokeStyle = "#ffd24a"; g.lineWidth = 3 * razon;
        g.beginPath();
        g.moveTo(x - 9 * razon, y - 9 * razon); g.lineTo(x + 9 * razon, y + 9 * razon);
        g.moveTo(x + 9 * razon, y - 9 * razon); g.lineTo(x - 9 * razon, y + 9 * razon);
        g.stroke();
        g.fillStyle = "#ffd24a"; g.fillText("TRAMPA", x + 12 * razon, y - 10 * razon);
      }
    }
    if (jugador) {
      const [x, y, escala] = proyectar(jugador);
      dibujarMirada(x, y, escala, razon);
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

  // Lo que ve el jugador 1: un cono desde su posición hacia donde apunta la
  // cámara (norte = arriba; el giro a la derecha va en sentido horario).
  const MEDIO_CAMPO_DE_VISION = 54 * Math.PI / 180;
  const ALCANCE_DE_LA_MIRADA_M = 9;
  function dibujarMirada(x, y, escala, razon) {
    if (camaraYaw === null) return;
    const centro = (camaraYaw - 90) * Math.PI / 180;     // el lienzo mide los ángulos desde el este, en horario
    const radio = ALCANCE_DE_LA_MIRADA_M * escala;
    g.save();
    g.beginPath();
    g.moveTo(x, y);
    g.arc(x, y, radio, centro - MEDIO_CAMPO_DE_VISION, centro + MEDIO_CAMPO_DE_VISION);
    g.closePath();
    g.fillStyle = "rgba(125,255,154,.09)"; g.fill();
    g.strokeStyle = "rgba(125,255,154,.45)"; g.lineWidth = razon; g.setLineDash([5 * razon, 5 * razon]); g.stroke();
    g.restore();
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
    pintarRescate(ahora);
    pintarBloqueo(ahora);
    if (caidaEn) {
      const quedan = Math.max(0, Math.ceil((VENTANA_PARA_VOLVER_MS - (performance.now() - caidaEn)) / 1000));
      const texto = `SIN SEÑAL · RECONECTANDO · INTENTO ${Math.min(Math.max(intentos, 1), MAX_INTENTOS)} DE ${MAX_INTENTOS} · QUEDAN ${quedan} s`;
      if ($("reconectando").hidden) $("reconectando").hidden = false;
      if ($("reconectando").textContent !== texto) $("reconectando").textContent = texto;
    }
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
