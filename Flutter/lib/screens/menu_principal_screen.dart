import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import '../config/server_config.dart';
import '../providers/connection_provider.dart';
import '../utils/colors.dart';
import 'configurar_servidor_screen.dart';
import 'splash_screen.dart';
import 'tutorial_screen.dart';

/// Pantalla de menú principal: fondo con el título y las opciones
/// "Tutorial/Nuevo Juego/Continuar/Opciones/Salir" ya dibujadas en la imagen
/// (assets/images/FondoJuego.jpeg). Como el texto es parte del JPEG
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
/// Abajo a la derecha dice con qué PC va a jugar la tablet (o que está en
/// modo de prueba, sin PC): el error más común al probar era tocar «Nuevo
/// juego» con el modo de prueba encendido y no entender por qué la PC no
/// hacía nada. Tocarlo abre Opciones.
///
/// También reproduce música ambiente en loop mientras esta pantalla está
/// visible (única pantalla de Flutter con audio — el resto del sonido de
/// gameplay pertenece a Unity, ver PROGRESS.md). Se detiene al navegar a
/// SplashScreen (Nuevo Juego/Continuar) para no superponerse con el
/// juego; sigue sonando si el jugador solo abre Opciones y vuelve.
class MenuPrincipalScreen extends StatefulWidget {
  /// false en las pruebas: ahí no hay reproductor de audio.
  final bool musica;

  const MenuPrincipalScreen({super.key, this.musica = true});

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
  static const double _xOpciones = 0.16;
  static const double _anchoZona = 0.28;
  static const double _altoZona = 0.085;

  static const double _yTutorial = 0.408;
  static const double _yNuevoJuego = 0.518;
  static const double _yContinuar = 0.625;
  static const double _yOpciones = 0.733;
  static const double _ySalir = 0.839;

  static const String _rutaMusicaMenu =
      'sounds/FNaF_2_-_Música_del_menú.ogg';

  AudioPlayer? _reproductor;

  @override
  void initState() {
    super.initState();
    if (widget.musica) {
      _reproductor = AudioPlayer();
      _reproducirMusicaDeMenu();
    }
  }

  Future<void> _reproducirMusicaDeMenu() async {
    final AudioPlayer? reproductor = _reproductor;
    if (reproductor == null) return;
    await reproductor.setReleaseMode(ReleaseMode.loop);
    await reproductor.play(AssetSource(_rutaMusicaMenu));
  }

  @override
  void dispose() {
    _reproductor?.dispose();
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
                  'assets/images/FondoJuego.jpeg',
                  fit: BoxFit.cover,
                ),
              ),
              _zonaTactil(
                rectangulo: rectangulo,
                yRelativo: _yTutorial,
                onTap: () => _alTocarTutorial(context),
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
              Positioned(
                right: 24,
                bottom: 24,
                child: SafeArea(child: _indicadorDeConexion(context)),
              ),
            ],
          );
        },
      ),
    );
  }

  /// "PC: 192.168.1.100:8000" o "Modo de prueba · sin PC". Va a la derecha,
  /// lejos de las opciones dibujadas a la izquierda, y abre Opciones al tocarlo.
  Widget _indicadorDeConexion(BuildContext context) {
    final bool prueba = ServerConfig.useMock;
    final Color color = prueba ? AppColors.advertencia : AppColors.textoSecundario;
    return Material(
      color: Colors.black.withValues(alpha: 0.55),
      borderRadius: BorderRadius.circular(14),
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => _alTocarOpciones(context),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(prueba ? Icons.science_outlined : Icons.computer, size: 30, color: color),
              const SizedBox(width: 12),
              Text(
                prueba ? 'Modo de prueba · sin PC' : 'PC: ${ServerConfig.direccion}',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: color),
              ),
              const SizedBox(width: 10),
              Icon(Icons.settings, size: 26, color: color),
            ],
          ),
        ),
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

  /// Zona táctil invisible sobre cada palabra del menú. Al tocarla se ve un
  /// destello suave (antes no había ninguna respuesta visual al toque).
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
        child: InkWell(
          borderRadius: BorderRadius.circular(12),
          splashColor: AppColors.peligro.withValues(alpha: 0.25),
          highlightColor: AppColors.peligro.withValues(alpha: 0.15),
          onTap: onTap,
        ),
      ),
    );
  }

  void _alIniciar(BuildContext context, {required String modo}) {
    _reproductor?.stop();
    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(builder: (_) => SplashScreen(modo: modo)),
    );
  }

  void _alTocarTutorial(BuildContext context) {
    _reproductor?.stop();
    Navigator.of(context).pushReplacement(
      MaterialPageRoute<void>(builder: (_) => const TutorialScreen()),
    );
  }

  Future<void> _alTocarOpciones(BuildContext context) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => const ConfigurarServidorScreen()),
    );
    // Al volver, el indicador muestra lo que se acaba de guardar.
    if (mounted) setState(() {});
  }

  /// Le avisa a Unity (con un `connect` de modo "salir") para que cierre el
  /// juego de la PC y después cierra la app de la tablet. Sin servidor real
  /// (mock) o sin conexión solo se cierra la tablet.
  Future<void> _alSalir() async {
    _reproductor?.stop();
    if (!ServerConfig.useMock) {
      final ConnectionProvider conexion = context.read<ConnectionProvider>();
      conexion.connect(modo: 'salir');
      // El mensaje sale por un socket recién abierto: darle un instante antes
      // de cerrarlo para que llegue a Unity.
      await Future<void>.delayed(const Duration(milliseconds: 400));
      conexion.disconnect();
    }
    await SystemNavigator.pop();
  }
}
