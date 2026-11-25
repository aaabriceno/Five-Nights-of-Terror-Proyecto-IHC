// Susto del jugador 2 (2026-11-24): cuando atrapan al jugador 1, esta página
// también muestra el jumpscare, con el video del animatrónico que atacó y el
// mismo grito que suena en la PC. Después queda la tarjeta de fin, como en
// Unity: primero el susto, luego el resultado.
//
// Los medios vienen de las carpetas de Unity (servir_pagina.py los sirve bajo
// /sustos/) y se precargan como blobs al abrir la página, para que el susto
// salga al instante. En el navegador queda como window.Susto.
(function () {
  "use strict";

  const VIDEOS = ["jumpscare_freddy.webm", "jumpscare_vixy.webm", "jumpscare_puppet.webm"];
  const IMAGEN = "desktop_jumpscare.jpg";
  const GRITO = "desktop_jumpscare.ogg";

  const urls = {};          // archivo -> URL blob: (solo los que cargaron)
  const duraciones = {};    // archivo -> segundos (videos y grito)
  let base = "sustos/";
  let precargado = false;
  let grito = null;         // <audio> con el grito, cuando cargó
  let enPantalla = false;
  let temporizador = null;

  const $ = (id) => document.getElementById(id);

  // Cuánto dura un video o audio ya cargado; 0 si el navegador no lo sabe.
  function medir(elemento, url) {
    return new Promise((resolver) => {
      const listo = () => resolver(Number.isFinite(elemento.duration) ? elemento.duration : 0);
      elemento.addEventListener("loadedmetadata", listo, { once: true });
      elemento.addEventListener("error", () => resolver(0), { once: true });
      elemento.preload = "metadata";
      elemento.src = url;
    });
  }

  function cargar(archivo) {
    return fetch(base + archivo)
      .then((respuesta) => {
        if (!respuesta.ok) throw new Error(`${archivo}: ${respuesta.status}`);
        return respuesta.blob();
      })
      .then((blob) => {
        urls[archivo] = URL.createObjectURL(blob);
        if (archivo === IMAGEN) return;
        const elemento = archivo === GRITO ? new Audio() : document.createElement("video");
        if (archivo === GRITO) { grito = elemento; grito.preload = "auto"; }
        return medir(elemento, urls[archivo]).then((segundos) => { duraciones[archivo] = segundos; });
      })
      .catch(() => { /* sin ese archivo: imagen fija o susto sin grito */ });
  }

  function precargar(carpeta) {
    if (carpeta !== undefined) base = carpeta;
    return Promise.all(VIDEOS.concat([IMAGEN, GRITO]).map(cargar)).then(() => { precargado = true; });
  }

  // Algunos navegadores (Safari) solo dejan sonar un audio si se tocó dentro
  // de un gesto: en el primer clic o tecla con el grito ya cargado, se
  // reproduce en silencio y se para. Hasta entonces se sigue escuchando.
  function desbloquearGrito() {
    if (!grito || enPantalla) return;
    document.removeEventListener("pointerdown", desbloquearGrito, true);
    document.removeEventListener("keydown", desbloquearGrito, true);
    grito.muted = true;
    const intento = grito.play();
    const parar = () => {
      if (enPantalla) return;          // llegó un ataque mientras tanto: que suene
      grito.pause();
      grito.currentTime = 0;
      grito.muted = false;
    };
    if (intento && intento.then) intento.then(parar, () => { grito.muted = false; });
    else parar();
  }
  document.addEventListener("pointerdown", desbloquearGrito, true);
  document.addEventListener("keydown", desbloquearGrito, true);

  function sonar(capa) {
    if (!grito) { capa.dataset.sonido = "sin-archivo"; return; }
    grito.muted = false;
    grito.currentTime = 0;
    capa.dataset.sonido = "intentando";
    let intento;
    try { intento = grito.play(); } catch (e) { capa.dataset.sonido = "bloqueado"; return; }
    if (intento && intento.then) {
      intento.then(() => { capa.dataset.sonido = "sonando"; }, () => { capa.dataset.sonido = "bloqueado"; });
    } else {
      capa.dataset.sonido = "sonando";
    }
  }

  function verImagen() {
    $("susto").dataset.modo = "imagen";
    $("susto-video").hidden = true;
    $("susto-imagen").hidden = false;
  }

  // Muestra el susto del animatrónico (como lo nombra Unity en `attack`).
  // Devuelve false si ya hay un susto en pantalla: no se repite ni se alarga.
  function mostrar(animatronico) {
    if (enPantalla) return false;
    enPantalla = true;
    const capa = $("susto"), video = $("susto-video"), imagen = $("susto-imagen");
    const archivo = Logica.archivoDeSusto(animatronico);
    const conVideo = !!(archivo && urls[archivo]);

    capa.dataset.animatronico = typeof animatronico === "string" ? animatronico.trim().toLowerCase() : "";
    imagen.src = urls[IMAGEN] || base + IMAGEN;
    if (conVideo) {
      // Con video el fondo es negro, igual que en Unity: la imagen fija no se asoma al terminar el clip.
      capa.dataset.modo = "video";
      imagen.hidden = true;
      video.hidden = false;
      video.onerror = verImagen;
      video.src = urls[archivo];
      video.currentTime = 0;
      const intento = video.play();
      if (intento && intento.catch) intento.catch(verImagen);
    } else {
      verImagen();
    }
    sonar(capa);
    // Sin barras de desplazamiento mientras dura: la capa llena la ventana entera.
    document.documentElement.classList.add("con-susto");
    capa.hidden = false;

    const segundos = Logica.duracionDelSusto(conVideo ? duraciones[archivo] : 0, grito ? duraciones[GRITO] : 0);
    clearTimeout(temporizador);
    temporizador = setTimeout(ocultar, segundos * 1000);
    return true;
  }

  function ocultar() {
    clearTimeout(temporizador);
    temporizador = null;
    enPantalla = false;
    $("susto-video").pause();
    $("susto").hidden = true;
    document.documentElement.classList.remove("con-susto");
  }

  window.Susto = {
    precargar,
    mostrar,
    visible: () => enPantalla,
    listo: () => precargado,
  };
})();
