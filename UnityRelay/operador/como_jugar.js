// «Cómo jugar» del jugador 2 (2026-11-20): 7 diapositivas cortas y una
// práctica de mentira (practica.js) para aprender haciendo: conocer a los
// animatrónicos, leer el panel del jugador 1, Retrasar, poner una trampa y
// usar la terminal. No se manda nada al juego (solo se avisa que está abierto,
// ver «comojugar» en operador.js). Expone window.ComoJugar.
(function () {
  "use strict";
  const $ = (id) => document.getElementById(id);
  const SVG = "http://www.w3.org/2000/svg";
  const TOTAL = 8;
  const COLORES = { freddy: "#6fcf6f", vixy: "#ff5a5a", puppet: "#d9a066" };
  const NOMBRES = { freddy: "Freddy", vixy: "Vixy", puppet: "Puppet" };
  const FICHAS = {
    freddy: "Camina por los pasillos hasta la puerta de la oficina. La linterna del jugador 1 lo ahuyenta.",
    vixy: "Entra por el ducto izquierdo. El jugador 1 la frena cerrando esa compuerta.",
    puppet: "Duerme en su caja de música. Si la caja se descarga, sale por el ducto derecho: avísale al jugador 1 que le dé cuerda.",
  };
  // Dónde aparece cada uno en la diapositiva 2.
  const LUGARES = { freddy: { x: 44, y: 8.5 }, vixy: { x: 29, y: 61 }, puppet: { x: 80, y: 8.5 } };

  const estado = Practica.crear();
  let actual = 1;
  let abridor = null;
  let animando = false;
  let modoTrampa = false;
  const tareas = { calculo: null, ecuacion: null };   // la tarea abierta en cada terminal
  let puppetFuera = false;
  let estabaAtrapado = false;

  // ------------------------------------------------------------------ dibujo
  function nodo(tipo, atributos, padre) {
    const e = document.createElementNS(SVG, tipo);
    for (const k in atributos) e.setAttribute(k, atributos[k]);
    if (padre) padre.appendChild(e);
    return e;
  }

  function rotulo(padre, x, y, texto, clase) {
    const t = nodo("text", { x, y, class: clase || "cj-rotulo", "text-anchor": "middle" }, padre);
    t.textContent = texto;
    return t;
  }

  // El edificio visto desde arriba, simplificado: las mismas salas que el mapa real.
  const SALAS = [
    { x0: 32, y0: 2, x1: 68, y1: 13, nombre: "ESCENARIO" },
    { x0: 76, y0: 2, x1: 96, y1: 12, nombre: "PREMIOS" },
    { x0: 26, y0: 18, x1: 74, y1: 42, nombre: "COMEDOR" },
    { x0: 6, y0: 16, x1: 20, y1: 56, nombre: "" },
    { x0: 20, y0: 50, x1: 40, y1: 56, nombre: "" },
    { x0: 80, y0: 16, x1: 94, y1: 56, nombre: "" },
    { x0: 60, y0: 50, x1: 80, y1: 56, nombre: "" },
    { x0: 40, y0: 48, x1: 60, y1: 62, nombre: "OFICINA" },
  ];

  function dibujarEdificio(svg) {
    nodo("rect", { x: 0, y: 0, width: 100, height: 66, class: "cj-fondo" }, svg);
    for (const s of SALAS) {
      nodo("rect", { x: s.x0, y: s.y0, width: s.x1 - s.x0, height: s.y1 - s.y0, class: "cj-sala" }, svg);
      if (s.nombre) rotulo(svg, (s.x0 + s.x1) / 2, s.nombre === "OFICINA" ? s.y1 - 1.6 : s.y0 + 2.9, s.nombre);
    }
    rotulo(svg, 9.6, 41, "PASILLO IZQ.").setAttribute("transform", "rotate(-90 9.6 41)");
    rotulo(svg, 92.6, 41, "PASILLO DER.").setAttribute("transform", "rotate(-90 92.6 41)");
    nodo("line", { x1: 8, y1: 61, x2: 40, y2: 61, class: "cj-ducto" }, svg);
    nodo("line", { x1: 60, y1: 61, x2: 92, y2: 61, class: "cj-ducto" }, svg);
    rotulo(svg, 15, 59.6, "DUCTO IZQ.", "cj-rotulo cj-rotulo-chico");
    rotulo(svg, 85, 59.6, "DUCTO DER.", "cj-rotulo cj-rotulo-chico");
    const j1 = nodo("g", { class: "cj-j1", transform: "translate(50 55)" }, svg);
    nodo("rect", { x: -2.6, y: -2.6, width: 5.2, height: 5.2 }, j1);
    rotulo(j1, 0, 1.1, "J1", "cj-j1-texto");
  }

  // Un punto de color con su nombre, como en el mapa real.
  function animatronico(svg, nombre, x, y) {
    const g = nodo("g", { class: "cj-animatronico", transform: `translate(${x} ${y})` }, svg);
    g.style.setProperty("--color", COLORES[nombre]);
    const anillo = nodo("circle", { r: 4.4, class: "cj-anillo" }, g);
    nodo("circle", { r: 2.4, class: "cj-cuerpo" }, g);
    const etiqueta = nodo("text", { x: 5.2, y: 1.1, class: "cj-nombre" }, g);
    etiqueta.textContent = NOMBRES[nombre].toUpperCase();
    return { g, anillo, etiqueta };
  }

  function dibujarOficina(svg) {
    nodo("rect", { x: 0, y: 0, width: 100, height: 64, class: "cj-fondo" }, svg);
    nodo("rect", { x: 8, y: 4, width: 84, height: 56, class: "cj-sala" }, svg);
    rotulo(svg, 50, 9, "OFICINA");
    nodo("rect", { x: 33, y: 13, width: 34, height: 18, class: "cj-monitor" }, svg);
    rotulo(svg, 50, 23.5, "CAM 03", "cj-rotulo cj-rotulo-chico");
    nodo("line", { x1: 18, y1: 38, x2: 82, y2: 38, class: "cj-mesa" }, svg);
    nodo("rect", { x: 57, y: 41, width: 10, height: 7, rx: 1, class: "cj-tableta" }, svg);
    const j1 = nodo("g", { class: "cj-j1", transform: "translate(50 47)" }, svg);
    nodo("rect", { x: -2.6, y: -2.6, width: 5.2, height: 5.2 }, j1);
    rotulo(j1, 0, 1.1, "J1", "cj-j1-texto");
    nodo("line", { x1: 8, y1: 24, x2: 8, y2: 40, class: "cj-puerta" }, svg);
    nodo("line", { x1: 92, y1: 24, x2: 92, y2: 40, class: "cj-puerta" }, svg);
  }

  // Diapositiva 1
  dibujarOficina($("cj-dibujo-oficina"));
  dibujarEdificio($("cj-dibujo-edificio"));
  for (const n in LUGARES) animatronico($("cj-dibujo-edificio"), n, LUGARES[n].x, LUGARES[n].y);

  // Diapositiva 2: tocar a cada uno
  const mapaConocer = $("cj-mapa-conocer");
  dibujarEdificio(mapaConocer);
  for (const n in LUGARES) {
    const { g } = animatronico(mapaConocer, n, LUGARES[n].x, LUGARES[n].y);
    g.id = "cj-conocer-" + n;
    g.setAttribute("role", "button");
    g.setAttribute("tabindex", "0");
    g.setAttribute("aria-label", NOMBRES[n]);
    g.classList.add("cj-tocable");
    g.addEventListener("click", () => conocer(n));
  }

  function conocer(nombre) {
    const cuantos = Practica.conocer(estado, nombre);
    $("cj-conocer-" + nombre).classList.add("cj-conocido");
    $("cj-conocidos").textContent = `${cuantos} / 3`;
    const ficha = $("cj-ficha");
    ficha.textContent = "";
    const titulo = document.createElement("b");
    titulo.style.color = COLORES[nombre];
    titulo.textContent = NOMBRES[nombre].toUpperCase() + " · ";
    ficha.append(titulo, FICHAS[nombre]);
  }

  // Diapositivas 4 y 5: Freddy caminando
  const mapaRetrasar = $("cj-mapa-retrasar");
  const mapaTrampa = $("cj-mapa-trampa");
  dibujarEdificio(mapaRetrasar);
  dibujarEdificio(mapaTrampa);
  const marcaTrampa = nodo("g", { class: "cj-trampa" }, mapaTrampa);
  for (const d of ["M-2.6 -2.6 L2.6 2.6", "M2.6 -2.6 L-2.6 2.6"]) nodo("path", { d }, marcaTrampa);
  rotulo(marcaTrampa, 0, -4, "TRAMPA", "cj-trampa-texto");
  for (const p of Practica.PUNTOS) {
    const g = nodo("g", { id: "cj-punto-" + p.id, class: "cj-punto-mapa", transform: `translate(${p.x} ${p.y})`,
      role: "button", tabindex: "0", "aria-label": p.nombre }, mapaTrampa);
    nodo("circle", { r: 3.4, class: "cj-zona" }, g);
    nodo("circle", { r: 1, class: "cj-marca" }, g);
    g.addEventListener("click", () => elegirPunto(p.id));
  }
  const freddys = [animatronico(mapaRetrasar, "freddy", 0, 0), animatronico(mapaTrampa, "freddy", 0, 0)];

  function pintarFreddy(ahora) {
    const p = Practica.posicion(estado);
    const f = Practica.freddy(estado, ahora);
    const como = f.atrapado ? "atrapado" : f.lento ? "lento" : "normal";
    const texto = "FREDDY" + (f.atrapado ? " · ATRAPADO" : f.lento ? " · LENTO" : "");
    for (const fr of freddys) {
      fr.g.setAttribute("transform", `translate(${p.x.toFixed(2)} ${p.y.toFixed(2)})`);
      fr.g.dataset.como = como;
      if (fr.etiqueta.textContent !== texto) fr.etiqueta.textContent = texto;
    }
    mapaRetrasar.dataset.freddy = como;
    mapaTrampa.dataset.freddy = como;
    if (f.atrapado && !estabaAtrapado) mensaje("cj-mensaje-trampa", "¡ATRAPADO! Freddy se queda quieto 6 s.", "bien");
    estabaAtrapado = f.atrapado;
    const punto = estado.trampa && Practica.PUNTOS.find((x) => x.id === estado.trampa);
    marcaTrampa.style.display = punto ? "" : "none";
    if (punto) marcaTrampa.setAttribute("transform", `translate(${punto.x} ${punto.y})`);
    // Retrasar: como la fila de «Amenazas» de la sala.
    const detalle = f.retrasoRestante > 0 ? `retrasado ${Math.ceil(f.retrasoRestante)} s`
      : f.esperaRestante > 0 ? `disponible en ${Math.ceil(f.esperaRestante)} s` : "listo";
    if ($("cj-detalle-retrasar").textContent !== detalle) $("cj-detalle-retrasar").textContent = detalle;
    $("cj-retrasar").disabled = f.retrasoRestante > 0 || f.esperaRestante > 0;
    const cargas = `${estado.cargas} / ${Practica.VALORES.maxCargas}`;
    if ($("cj-cargas").textContent !== cargas) $("cj-cargas").textContent = cargas;
    if ($("cj-cargas-terminal").textContent !== cargas) $("cj-cargas-terminal").textContent = cargas;
    for (const id of ["cj-bolsillo-trampa", "cj-bolsillo-terminal"]) $(id).dataset.cargas = String(estado.cargas);
  }

  function mensaje(id, texto, tono) {
    const e = $(id);
    e.textContent = texto;
    e.dataset.tono = tono || "";
  }

  $("cj-retrasar").addEventListener("click", () => {
    const ahora = performance.now() / 1000;
    const r = Practica.retrasar(estado, ahora);
    mensaje("cj-mensaje-retrasar", r.ok ? "¡Bien! Freddy va lento 8 s." : `Todavía no: disponible en ${Math.ceil(r.esperaRestante)} s.`,
      r.ok ? "bien" : "");
    pintarFreddy(ahora);
  });

  function ponerModoTrampa(activo) {
    modoTrampa = activo;
    if (activo) mapaTrampa.dataset.modo = "trampa";
    else delete mapaTrampa.dataset.modo;
    $("cj-poner-trampa").classList.toggle("activo", activo);
    $("cj-poner-trampa").textContent = activo ? "Elige un punto del mapa" : "Colocar trampa";
  }

  const SIN_CARGAS = "Sin cargas: resuelve un cálculo en la terminal (diapositiva 6).";
  $("cj-poner-trampa").addEventListener("click", () => {
    if (modoTrampa) { ponerModoTrampa(false); return; }
    if (estado.cargas <= 0) { mensaje("cj-mensaje-trampa", SIN_CARGAS); return; }
    ponerModoTrampa(true);
    mensaje("cj-mensaje-trampa", "Toca un punto del mapa por donde creas que pasará Freddy.");
  });

  function elegirPunto(id) {
    if (!modoTrampa) {
      mensaje("cj-mensaje-trampa", estado.cargas <= 0 ? SIN_CARGAS : "Primero aprieta «Colocar trampa».");
      return;
    }
    ponerModoTrampa(false);
    const r = Practica.ponerTrampa(estado, id);
    const punto = Practica.PUNTOS.find((p) => p.id === id);
    if (r.ok) mensaje("cj-mensaje-trampa", `Trampa puesta en el ${punto.nombre.toLowerCase()}. Espera a que Freddy pase por ahí.`, "bien");
    else if (r.motivo === "fuera_de_ruta") mensaje("cj-mensaje-trampa", "Freddy no pasa por ahí. Mira por dónde camina y prueba otra vez.", "mal");
    else if (r.motivo === "sin_cargas") mensaje("cj-mensaje-trampa", SIN_CARGAS);
    pintarFreddy(performance.now() / 1000);
  }

  // ------------------------------------------------------- diapositiva 3: panel
  const FILAS = ["mirada", "camara", "tareas", "caja", "bateria"];
  for (const fila of FILAS) $("cj-fila-" + fila).addEventListener("click", () => tocarFila(fila));

  function pintarPregunta() {
    const p = Practica.preguntaDelPanel(estado);
    $("cj-pregunta-panel").textContent = p ? p.texto : "¡Listo! Ya sabes leer el panel del jugador 1.";
  }
  pintarPregunta();

  function tocarFila(fila) {
    const pregunta = Practica.preguntaDelPanel(estado);
    if (!pregunta) { mensaje("cj-mensaje-panel", "Ya lo tienes: sigue con «Siguiente»."); return; }
    const r = Practica.tocarFila(estado, fila);
    if (!r.correcto) {
      mensaje("cj-mensaje-panel", pregunta.fila === "mirada" ? "Esa no. Busca la fila «Mirada»." : "Esa no. Busca «Caja de Puppet».", "mal");
      return;
    }
    $("cj-fila-" + fila).classList.add("cj-acierto");
    mensaje("cj-mensaje-panel", r.terminado
      ? "¡Bien! Si la caja baja mucho, avísale que le dé cuerda."
      : "¡Eso! «Mirada» te dice si está mirando la pantalla.", "bien");
    pintarPregunta();
  }

  // Valores que cambian solos, como en una partida.
  function pintarPanel(ahora) {
    const paso = Math.floor(ahora / 1.6);
    const valores = {
      mirada: paso % 2 ? "No mira la pantalla" : "Mira la pantalla",
      camara: ["Al frente", "Izquierda 30°", "Al frente", "Derecha 45°"][paso % 4],
      tareas: String(1 + (paso % 3)),
      caja: `${80 - (paso % 8) * 9} %`,
      bateria: `${100 - (paso % 6) * 6} %`,
    };
    for (const fila of FILAS) {
      const e = $("cj-valor-" + fila);
      if (e.textContent !== valores[fila]) e.textContent = valores[fila];
    }
    $("cj-valor-mirada").dataset.mal = String(paso % 2 === 1);
  }

  // ------------------------------------------- diapositivas 6 y 7: la terminal
  // Dos terminales con las mismas reglas que la partida (un error bloquea las
  // dos 3 s): la del cálculo (+1 trampa y +20 % de batería, con cupo) y la de
  // la ecuación (desbloquea el rescate de Puppet).
  const TERMINALES = {
    calculo: { boton: "cj-calculo", enunciado: "cj-enunciado", formulario: "cj-formulario", respuesta: "cj-respuesta",
      mensaje: "cj-mensaje-terminal", inicial: "Aprieta «Resolver cálculo».",
      porque: "Resuélvelo: +1 trampa a tu bolsillo y +20 % de batería al jugador 1." },
    ecuacion: { boton: "cj-ecuacion", enunciado: "cj-enunciado-ecuacion", formulario: "cj-formulario-ecuacion",
      respuesta: "cj-respuesta-ecuacion", mensaje: "cj-mensaje-puppet", inicial: "Aprieta «Resolver ecuación».",
      porque: "Resuélvela para desbloquear el rescate de Puppet." },
  };

  function pedirTarea(tipo) {
    const T = TERMINALES[tipo];
    const ahora = performance.now() / 1000;
    if (Practica.bloqueoRestante(estado, ahora) > 0) return;
    if (tipo === "calculo" && estado.calculosRestantes <= 0) {
      mensaje(T.mensaje, "Ya no te quedan cálculos: en la partida el cupo se llena de nuevo cada noche.");
      return;
    }
    tareas[tipo] = Practica.crearTarea(tipo);
    $(T.enunciado).textContent = tareas[tipo].enunciado;
    $(T.formulario).hidden = false;
    $(T.respuesta).value = "";
    $(T.respuesta).focus();
    mensaje(T.mensaje, T.porque);
  }

  function responderTarea(tipo) {
    const T = TERMINALES[tipo];
    const tarea = tareas[tipo];
    if (!tarea) return;
    const valor = Logica.leerRespuesta($(T.respuesta).value);
    if (valor === null) { mensaje(T.mensaje, "Escribe solo un número entero.", "mal"); return; }
    const ahora = performance.now() / 1000;
    const bateriaAntes = estado.bateriaJ1;
    const r = Practica.responder(estado, tarea, valor, ahora);
    if (r.bloqueado) { mensaje(T.mensaje, `Terminal bloqueada: faltan ${Math.ceil(r.bloqueoS)} s.`); return; }
    tareas[tipo] = null;
    $(T.formulario).hidden = true;
    $(T.enunciado).textContent = T.inicial;
    if (r.sinCupo) {
      mensaje(T.mensaje, "Ya no te quedan cálculos: en la partida el cupo se llena de nuevo cada noche.");
    } else if (!r.correcto) {
      mensaje(T.mensaje, `Incorrecto: la terminal se bloquea ${Practica.VALORES.bloqueoS} s. No gastaste cupo.`, "mal");
    } else if (tipo === "calculo") {
      const ganoBateria = estado.bateriaJ1 > bateriaAntes;
      mensaje(T.mensaje, "¡Correcto! +1 trampa a tu bolsillo" +
        (ganoBateria ? " y +20 % de batería al jugador 1." : " (la batería ya estaba llena)."), "bien");
      animarPremio(ganoBateria);
    } else {
      puppetFuera = true;
      mensaje(T.mensaje, "¡Rescate desbloqueado! Puppet salió de su caja: rescátalo antes de que llegue.", "bien");
    }
    pintarTerminal(ahora);
    pintarFreddy(ahora);
  }

  for (const tipo of Object.keys(TERMINALES)) {
    $(TERMINALES[tipo].boton).addEventListener("click", () => pedirTarea(tipo));
    $(TERMINALES[tipo].formulario).addEventListener("submit", (evento) => {
      evento.preventDefault();
      responderTarea(tipo);
    });
  }

  // «Resuelves esto y te da esto»: una ficha «+1 TRAMPA» vuela de la terminal
  // al bolsillo, la batería del jugador 1 sube con un «+20 %» que flota y el
  // contador del cupo late. Con «reducir movimiento» la ficha no se mueve.
  function animarPremio(ganoBateria) {
    const escena = $("cj-escena-calculo");
    const caja = escena.getBoundingClientRect();
    const desde = $("cj-calculo").getBoundingClientRect();   // el formulario ya se ocultó
    const hasta = $("cj-bolsillo-terminal").getBoundingClientRect();
    const ficha = document.createElement("span");
    ficha.className = "cj-ficha-volando";
    ficha.textContent = "+1 TRAMPA";
    ficha.style.left = `${desde.left - caja.left}px`;
    ficha.style.top = `${desde.top - caja.top}px`;
    ficha.style.setProperty("--dx", `${hasta.left - desde.left}px`);
    ficha.style.setProperty("--dy", `${hasta.top - desde.top}px`);
    escena.appendChild(ficha);
    setTimeout(() => ficha.remove(), 1400);
    reiniciarAnimacion($("cj-premio-bolsillo"), "cj-gano");
    reiniciarAnimacion($("cj-cupo"), "cj-late");
    if (ganoBateria) {
      reiniciarAnimacion($("cj-premio-bateria"), "cj-gano");
      const mas = document.createElement("span");
      mas.className = "cj-flota";
      mas.textContent = "+20 %";
      $("cj-premio-bateria").appendChild(mas);
      setTimeout(() => mas.remove(), 1400);
    }
  }

  function reiniciarAnimacion(elemento, clase) {
    elemento.classList.remove(clase);
    void elemento.offsetWidth;   // fuerza a que la animación vuelva a empezar
    elemento.classList.add(clase);
  }

  $("cj-rescatar").addEventListener("click", () => {
    if (!puppetFuera || !Practica.rescatar(estado)) return;
    puppetFuera = false;
    mensaje("cj-mensaje-puppet", `¡Rescataste a Puppet! Volvió a su caja, pero ahora dura ${Logica.formatearSegundos(estado.segundosDeLaCaja)} s.`, "bien");
    pintarTerminal(performance.now() / 1000);
  });

  function pintarTerminal(ahora) {
    const bloqueo = Practica.bloqueoRestante(estado, ahora);
    const sinCupo = estado.calculosRestantes <= 0;
    const textos = {
      calculo: bloqueo > 0 ? `Terminal bloqueada… ${Math.ceil(bloqueo)} s` : sinCupo ? "Sin cálculos esta noche" : "Resolver cálculo",
      ecuacion: bloqueo > 0 ? `Terminal bloqueada… ${Math.ceil(bloqueo)} s` : "Resolver ecuación",
    };
    for (const tipo of Object.keys(TERMINALES)) {
      const boton = $(TERMINALES[tipo].boton);
      if (boton.textContent !== textos[tipo]) boton.textContent = textos[tipo];
    }
    $("cj-calculo").disabled = bloqueo > 0 || sinCupo;
    $("cj-ecuacion").disabled = bloqueo > 0;
    $("cj-rescatar").disabled = !puppetFuera;
    $("cj-alerta-puppet").hidden = !puppetFuera;
    $("cj-caja-puppet").dataset.puppet = puppetFuera ? "fuera" : "dentro";
    const cupo = `${estado.calculosRestantes} / ${Practica.VALORES.calculosDeLaNoche}`;
    if ($("cj-cupo").textContent !== cupo) $("cj-cupo").textContent = cupo;
    const bateria = `${estado.bateriaJ1} %`;
    if ($("cj-bateria-j1").textContent !== bateria) $("cj-bateria-j1").textContent = bateria;
    $("cj-barra-bateria").style.width = `${estado.bateriaJ1}%`;
    const segundos = `${Logica.formatearSegundos(estado.segundosDeLaCaja)} s`;
    if ($("cj-segundos-caja").textContent !== segundos) $("cj-segundos-caja").textContent = segundos;
    $("cj-barra-caja").style.width = `${(estado.segundosDeLaCaja / Practica.VALORES.segundosDeLaCaja) * 100}%`;
  }

  // ------------------------------------------------------ diapositiva 8: lista
  function pintarLista() {
    const lista = $("cj-lista");
    lista.textContent = "";
    for (const paso of Practica.PASOS) {
      const hecho = !Practica.pendientes(estado).includes(paso.id);
      const li = document.createElement("li");
      li.dataset.paso = paso.id;
      li.dataset.hecho = String(hecho);
      const marca = document.createElement("span");
      marca.className = "cj-marca-paso";
      marca.textContent = hecho ? "●" : "○";
      li.append(marca, " " + paso.texto);
      if (!hecho) {
        const ir = document.createElement("button");
        ir.type = "button";
        ir.textContent = "Practicar";
        ir.addEventListener("click", () => irA(paso.diapositiva));
        li.append(" ", ir);
      }
      lista.appendChild(li);
    }
  }

  // ------------------------------------------------------------ navegación
  for (let n = 1; n <= TOTAL; n++) {
    const b = document.createElement("button");
    b.type = "button";
    b.className = "cj-punto-avance";
    b.setAttribute("aria-label", `Diapositiva ${n}`);
    b.addEventListener("click", () => irA(n));
    $("cj-puntos").appendChild(b);
  }

  function irA(n) {
    n = Math.max(1, Math.min(TOTAL, n));
    if (actual === 5 && n !== 5) ponerModoTrampa(false);
    actual = n;
    $("como-jugar").dataset.diapositiva = String(n);
    for (const s of document.querySelectorAll("#como-jugar .cj-diapositiva")) s.hidden = s.dataset.n !== String(n);
    $("cj-contador").textContent = `${n} / ${TOTAL}`;
    $("cj-anterior").disabled = n === 1;
    const ultima = n === TOTAL;
    const enfocadoSiguiente = document.activeElement === $("cj-siguiente");
    $("cj-siguiente").hidden = ultima;
    $("cj-entendido").hidden = !ultima;
    if (ultima && enfocadoSiguiente) $("cj-entendido").focus();
    document.querySelectorAll("#cj-puntos .cj-punto-avance").forEach((b, i) => {
      if (i + 1 === n) b.setAttribute("aria-current", "step");
      else b.removeAttribute("aria-current");
    });
    if (ultima) pintarLista();
    const ahora = performance.now() / 1000;
    pintarFreddy(ahora);
    pintarTerminal(ahora);
  }

  $("cj-anterior").addEventListener("click", () => irA(actual - 1));
  $("cj-siguiente").addEventListener("click", () => irA(actual + 1));
  $("cj-entendido").addEventListener("click", () => cerrar());
  $("cj-cerrar").addEventListener("click", () => cerrar());

  document.addEventListener("keydown", (e) => {
    if ($("como-jugar").hidden) return;
    const escribiendo = e.target && /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName);
    if (e.key === "Escape") { cerrar(); return; }
    if (escribiendo) return;
    if (e.key === "ArrowRight") irA(actual + 1);
    else if (e.key === "ArrowLeft") irA(actual - 1);
    else if ((e.key === "Enter" || e.key === " ") && e.target.getAttribute && e.target.getAttribute("role") === "button"
             && e.target.namespaceURI === SVG) {
      e.preventDefault();
      e.target.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    }
  });

  // ------------------------------------------------------------------ bucle
  function cuadro(ms) {
    if ($("como-jugar").hidden) { animando = false; return; }
    const ahora = ms / 1000;
    Practica.avanzar(estado, ahora);
    pintarFreddy(ahora);
    if (actual === 3) pintarPanel(ahora);
    if (actual === 6 || actual === 7) pintarTerminal(ahora);
    requestAnimationFrame(cuadro);
  }

  // La sala avisa al relay cuándo está abierto (2026-11-22): mientras lo esté,
  // el jugador 1 no puede empezar una noche.
  function anunciar(abierto) {
    document.dispatchEvent(new CustomEvent("comojugar", { detail: { abierto } }));
  }

  function abrir(desde) {
    abridor = desde || document.activeElement;
    const estabaCerrado = $("como-jugar").hidden;
    $("como-jugar").hidden = false;
    if (estabaCerrado) anunciar(true);
    irA(1);
    $("cj-siguiente").focus();
    if (!animando) { animando = true; requestAnimationFrame(cuadro); }
  }

  function cerrar() {
    const estabaAbierto = !$("como-jugar").hidden;
    $("como-jugar").hidden = true;
    if (estabaAbierto) anunciar(false);
    ponerModoTrampa(false);
    if (abridor && abridor.offsetParent !== null) abridor.focus();
  }

  window.ComoJugar = { abrir, cerrar, abierto: () => !$("como-jugar").hidden };
})();
