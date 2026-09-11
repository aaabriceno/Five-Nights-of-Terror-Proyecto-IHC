import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'splash_screen.dart';

/// Pantalla de menú principal: fondo con el título y las opciones
/// "Iniciar/Opciones/Salir" ya dibujados en la imagen
/// (assets/images/FondoJuegoFNT.jpeg). Como el texto es parte del JPEG
/// (no widgets de Flutter), esta pantalla superpone zonas táctiles
/// invisibles en las coordenadas relativas donde cada palabra aparece
/// dibujada, en vez de dibujar botones propios encima.
class MenuPrincipalScreen extends StatelessWidget {
  const MenuPrincipalScreen({super.key});

  // Posición relativa (fracción del ancho/alto de la imagen) del centro
  // de cada palabra en FondoJuegoFNT.jpeg. Si se cambia la imagen de
  // fondo, estos valores deben recalibrarse a mano.
  static const double _xOpciones = 0.226;
  static const double _anchoZona = 0.30;
  static const double _altoZona = 0.07;

  static const double _yIniciar = 0.653;
  static const double _yOpciones = 0.727;
  static const double _ySalir = 0.800;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: LayoutBuilder(
        builder: (context, restricciones) {
          final double ancho = restricciones.maxWidth;
          final double alto = restricciones.maxHeight;
          return Stack(
            children: [
              Positioned.fill(
                child: Image.asset(
                  'assets/images/FondoJuegoFNT.jpeg',
                  fit: BoxFit.cover,
                ),
              ),
              _zonaTactil(
                context: context,
                ancho: ancho,
                alto: alto,
                yRelativo: _yIniciar,
                onTap: () => _alIniciar(context),
              ),
              _zonaTactil(
                context: context,
                ancho: ancho,
                alto: alto,
                yRelativo: _yOpciones,
                onTap: () => _alTocarOpciones(context),
              ),
              _zonaTactil(
                context: context,
                ancho: ancho,
                alto: alto,
                yRelativo: _ySalir,
                onTap: () => _alSalir(),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _zonaTactil({
    required BuildContext context,
    required double ancho,
    required double alto,
    required double yRelativo,
    required VoidCallback onTap,
  }) {
    return Positioned(
      left: (_xOpciones - _anchoZona / 2) * ancho,
      top: (yRelativo - _altoZona / 2) * alto,
      width: _anchoZona * ancho,
      height: _altoZona * alto,
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
    showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Opciones'),
        content: const Text('Próximamente disponible.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Cerrar'),
          ),
        ],
      ),
    );
  }

  void _alSalir() {
    SystemNavigator.pop();
  }
}
