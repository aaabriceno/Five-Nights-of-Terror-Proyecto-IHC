/// Animatrónicos que tienen video de jumpscare. Es la misma lista y la misma
/// regla de nombres que usa Unity (`RutaDeJumpscare.cs`).
const List<String> animatronicosConVideo = <String>['freddy', 'vixy', 'puppet'];

/// Ruta del video del jumpscare de [animatronico] dentro de los assets, o
/// `null` si no hay (nombre ausente o desconocido): en ese caso la pantalla
/// muestra la imagen fija de siempre.
///
/// El nombre es el que manda Unity en el mensaje `attack`. Solo se aceptan
/// los nombres conocidos, así que un nombre raro nunca arma una ruta.
String? rutaDeVideoDeJumpscare(String? animatronico) {
  final String limpio = (animatronico ?? '').trim().toLowerCase();
  if (!animatronicosConVideo.contains(limpio)) return null;
  return 'assets/videos/jumpscare_$limpio.webm';
}

/// Ruta de la cara de [animatronico] dentro de los assets (2026-11-24): el
/// cuadro de su video con la cara llena. Se ve al instante, antes de que
/// cargue el video, y queda si el video falla. Para un nombre ausente o
/// desconocido, la imagen general (también la cara de Freddy).
String rutaDeImagenDeJumpscare(String? animatronico) {
  final String limpio = (animatronico ?? '').trim().toLowerCase();
  if (!animatronicosConVideo.contains(limpio)) return 'assets/images/jumpscare.jpg';
  return 'assets/images/jumpscare_$limpio.jpg';
}
