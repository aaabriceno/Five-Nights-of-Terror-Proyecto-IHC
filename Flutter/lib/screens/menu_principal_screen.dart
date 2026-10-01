import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'configurar_servidor_screen.dart';
import 'splash_screen.dart';

/// Pantalla de menú principal: fondo con el título y las opciones
/// "Nuevo Juego/Continuar/Opciones/Salir" ya dibujados en la imagen
/// (assets/images/FondoJuegoFNT.jpeg). Como el texto es parte del JPEG
/// (no widgets de Flutter), esta pantalla superpone zonas táctiles
/// invisibles en las coordenadas relativas donde cada palabra aparece
/// dibujada, en vez de dibujar botones propios encima.
///
/// La imagen es panorámica (2752x1536, ratio ≈16:9) y se muestra con
/// BoxFit.cover para llenar toda la pantalla sin franjas negras — el
/// ratio de la imagen ya es muy cercano al de una tablet en horizontal,
/// así que el recorte de `cover` es mínimo. Las zonas táctiles se
/// calculan sobre el rectángulo real donde queda dibujada la imagen tras
/// escalarla y recortarla, no sobre el tamaño de la imagen original, para
/// que sigan alineadas con el texto en cualquier tamaño de pantalla.
///
/// También reproduce música ambiente en loop mientras esta pantalla está
/// visible (única pantalla de Flutter con audio — el resto del sonido de
/// gameplay pertenece a Unity, ver PROGRESS.md). Se detiene al navegar a
/// SplashScreen (Nuevo Juego/Continuar) para no superponerse con el
/// juego; sigue sonando si el jugador solo abre Opciones y vuelve.
class MenuPrincipalScreen extends StatefulWidget {
  const MenuPrincipalScreen({super.key});

  @override
  State<MenuPrincipalScreen> createState() => _MenuPrincipalScreenState();
}

class _MenuPrincipalScreenState extends State<MenuPrincipalScreen> {
  // Dimensiones reales de assets/images/FondoJuegoFNT.jpeg.
  static const double _anchoImagenOriginal = 2752;
  static const double _altoImagenOriginal = 1536;

  // Posición relativa (fracción del ancho/alto DE LA IMAGEN, no de la
  // pantalla) del centro de cada palabra en FondoJuegoFNT.jpeg. Si se
  // cambia la imagen de fondo, estos valores deben recalibrarse a mano.
  static const double _xOpciones = 0.155;
  static const double _anchoZona = 0.28;
  static const double _altoZona = 0.09;

  static const double _yNuevoJuego = 0.464;
  static const double _yContinuar = 0.573;
  static const double _yOpciones = 0.681;
  static const double _ySalir = 0.789;

  static const String _rutaMusicaMenu =
      'sounds/FNaF_2_-_Música_del_menú.ogg';

  final AudioPlayer _reproductor = AudioPlayer();

  @override
  void initState() {
    super.initState();
    _reproducirMusicaDeMenu();
  }

  Future<void> _reproducirMusicaDeMenu() async {
    await _reproductor.setReleaseMode(ReleaseMode.loop);
    await _reproductor.play(AssetSource(_rutaMusicaMenu));
  }

  @override
  void dispose() {
    _reproductor.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: LayoutBuilder(
        builder: (context, restricciones) {
          final Rect rectangulo = _calcularRectanguloImagenCover(
            restricciones.maxWidth,
            restricciones.maxHeight,
          );
          return Stack(
            children: [
              Positioned.fill(
                child: Image.asset(
                  'assets/images/FondoJuegoFNT.jpeg',
                  fit: BoxFit.cover,
                ),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _yNuevoJuego,
                onTap: () => _alIniciar(context, modo: 'nuevo'),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _yContinuar,
                onTap: () => _alIniciar(context, modo: 'continuar'),
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
  /// cuando se usa BoxFit.cover: la imagen se escala para llenar todo el
  /// espacio (el lado que sobra queda fuera de pantalla, recortado), así
  /// que el rectángulo resultante puede tener bordes negativos o mayores
  /// al área visible — se usa igual como referencia para las zonas
  /// táctiles relativas.
  Rect _calcularRectanguloImagenCover(double anchoDisponible, double altoDisponible) {
    final double escala = (anchoDisponible / _anchoImagenOriginal) >
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

  void _alIniciar(BuildContext context, {required String modo}) {
    _reproductor.stop();
    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(builder: (_) => SplashScreen(modo: modo)),
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
