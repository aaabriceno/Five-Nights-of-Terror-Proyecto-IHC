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
