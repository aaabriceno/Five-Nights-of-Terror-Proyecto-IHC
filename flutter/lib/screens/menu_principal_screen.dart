import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'configurar_servidor_screen.dart';
import 'splash_screen.dart';

/// Pantalla de menú principal: fondo con el título y las opciones
/// "Iniciar/Opciones/Salir" ya dibujados en la imagen
/// (assets/images/FondoJuegoFNT.jpeg). Como el texto es parte del JPEG
/// (no widgets de Flutter), esta pantalla superpone zonas táctiles
/// invisibles en las coordenadas relativas donde cada palabra aparece
/// dibujada, en vez de dibujar botones propios encima.
///
/// La imagen se muestra completa (BoxFit.contain, sin recortes) para que
/// el título y las tres palabras sean siempre visibles sin importar la
/// relación de aspecto de la pantalla. Como `contain` puede dejar franjas
/// negras arriba/abajo o a los costados (la imagen es más "cuadrada" que
/// una tablet en horizontal), las zonas táctiles se calculan sobre el
/// rectángulo real donde queda dibujada la imagen, no sobre toda la
/// pantalla — de lo contrario las zonas quedan desplazadas del texto.
class MenuPrincipalScreen extends StatelessWidget {
  const MenuPrincipalScreen({super.key});

  // Dimensiones reales de assets/images/FondoJuegoFNT.jpeg.
  static const double _anchoImagenOriginal = 2390;
  static const double _altoImagenOriginal = 1792;

  // Posición relativa (fracción del ancho/alto DE LA IMAGEN, no de la
  // pantalla) del centro de cada palabra en FondoJuegoFNT.jpeg. Si se
  // cambia la imagen de fondo, estos valores deben recalibrarse a mano.
  static const double _xOpciones = 0.226;
  static const double _anchoZona = 0.30;
  static const double _altoZona = 0.07;

  static const double _yIniciar = 0.653;
  static const double _yOpciones = 0.727;
  static const double _ySalir = 0.800;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: LayoutBuilder(
        builder: (context, restricciones) {
          final Rect rectangulo = _calcularRectanguloImagen(
            restricciones.maxWidth,
            restricciones.maxHeight,
          );
          return Stack(
            children: [
              Positioned.fill(
                child: Image.asset(
                  'assets/images/FondoJuegoFNT.jpeg',
                  fit: BoxFit.contain,
                ),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _yIniciar,
                onTap: () => _alIniciar(context),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _yOpciones,
                onTap: () => _alTocarOpciones(context),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _ySalir,
                onTap: () => _alSalir(),
              ),
            ],
          );
        },
      ),
    );
  }

  /// Calcula dónde queda dibujada la imagen dentro del área disponible
  /// cuando se usa BoxFit.contain: la imagen se escala para caber entera,
  /// quedando centrada con franjas vacías en el eje que sobre.
  Rect _calcularRectanguloImagen(double anchoDisponible, double altoDisponible) {
    final double escala = (anchoDisponible / _anchoImagenOriginal) <
            (altoDisponible / _altoImagenOriginal)
        ? anchoDisponible / _anchoImagenOriginal
        : altoDisponible / _altoImagenOriginal;

    final double anchoDibujado = _anchoImagenOriginal * escala;
    final double altoDibujado = _altoImagenOriginal * escala;
    final double desplazamientoX = (anchoDisponible - anchoDibujado) / 2;
    final double desplazamientoY = (altoDisponible - altoDibujado) / 2;

    return Rect.fromLTWH(
      desplazamientoX,
      desplazamientoY,
      anchoDibujado,
      altoDibujado,
    );
  }

  Widget _zonaTactil({
    required Rect rectangulo,
    required double yRelativo,
    required VoidCallback onTap,
  }) {
    return Positioned(
      left: rectangulo.left + (_xOpciones - _anchoZona / 2) * rectangulo.width,
      top: rectangulo.top + (yRelativo - _altoZona / 2) * rectangulo.height,
      width: _anchoZona * rectangulo.width,
      height: _altoZona * rectangulo.height,
      child: Material(
        color: Colors.transparent,
        child: InkWell(onTap: onTap),
      ),
    );
  }

  void _alIniciar(BuildContext context) {
    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(builder: (_) => const SplashScreen()),
    );
  }

  void _alTocarOpciones(BuildContext context) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => const ConfigurarServidorScreen()),
    );
  }

  void _alSalir() {
    SystemNavigator.pop();
  }
}
