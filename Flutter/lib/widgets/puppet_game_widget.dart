import 'dart:math';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../utils/colors.dart';
import 'encabezado_de_minijuego.dart';

/// Pantalla dedicada de la caja de música de Puppet, en el mismo estilo
/// de pantalla completa que los demás minijuegos (Cables, Perillas,
/// etc). A diferencia de esos, no tiene tiempo límite propio ni se
/// "completa" — el jugador entra, sostiene el control mientras quiera,
/// y sale cuando quiere; la caja se sigue drenando en segundo plano
/// aunque el jugador no esté en esta pantalla.
///
/// El nivel de la caja se dibuja como un "pastel" (arco relleno tipo
/// gráfico circular, no un anillo delgado): 100% = círculo completo,
/// 0% = nada — igual que el medidor visible del juego original. El
/// valor y el estado de peligro llegan tal cual del servidor
/// (`estado_puppet`, protocolo ya acordado); esta pantalla solo dibuja,
/// nunca decide umbrales por su cuenta.
///
/// Mecánica de interacción: mantener presionado (no toques repetidos),
/// mismo protocolo ya implementado (`dar_cuerda_inicio`/
/// `dar_cuerda_fin`) — el servidor sube la caja mientras el botón esté
/// sostenido.
class PuppetGameWidget extends StatefulWidget {
  final bool enPeligro;
  final int valorCajaPorcentaje;
  final VoidCallback alEmpezarASostener;
  final VoidCallback alSoltar;

  const PuppetGameWidget({
    super.key,
    required this.enPeligro,
    required this.valorCajaPorcentaje,
    required this.alEmpezarASostener,
    required this.alSoltar,
  });

  @override
  State<PuppetGameWidget> createState() => _PuppetGameWidgetState();
}

class _PuppetGameWidgetState extends State<PuppetGameWidget>
    with WidgetsBindingObserver {
  static const String _rutaSonidoCuerda =
      'sounds/FNaF_2_-_Dándole_cuerda_a_la_caja_de_música.ogg';
  // Tamaños máximos; en pantallas con poco alto se reducen (ver build) para
  // que nada desborde. El área que responde al toque es más grande que el
  // botón dibujado: un dedo que cae un poco al lado también cuenta.
  static const double _diametroBotonMaximo = 180;
  static const double _diametroPastelMaximo = 240;

  final AudioPlayer _reproductor = AudioPlayer();
  bool _sosteniendo = false;
  // Solo el primer dedo manda: un segundo toque (la palma, otro dedo) no
  // debe soltar ni reiniciar la cuerda.
  int? _punteroActivo;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _reproductor.dispose();
    super.dispose();
  }

  // Si la app pasa a segundo plano con el dedo apoyado, nunca llegará el
  // "dedo arriba": se suelta aquí para que Unity no se quede con la cuerda.
  @override
  void didChangeAppLifecycleState(AppLifecycleState estado) {
    if (estado != AppLifecycleState.resumed) {
      _punteroActivo = null;
      _alSoltar();
    }
  }

  void _alBajarDedo(PointerDownEvent evento) {
    if (_punteroActivo != null) return;
    _punteroActivo = evento.pointer;
    _alPresionar();
  }

  void _alSubirODeshacerDedo(PointerEvent evento) {
    if (evento.pointer != _punteroActivo) return;
    _punteroActivo = null;
    _alSoltar();
  }

  Future<void> _alPresionar() async {
    if (_sosteniendo) return;
    setState(() => _sosteniendo = true);
    HapticFeedback.mediumImpact();
    widget.alEmpezarASostener();
    // Un fallo de audio no debe impedir darle cuerda a la caja.
    try {
      await _reproductor.setReleaseMode(ReleaseMode.loop);
      await _reproductor.play(AssetSource(_rutaSonidoCuerda));
    } catch (_) {}
  }

  Future<void> _alSoltar() async {
    if (!_sosteniendo) return;
    setState(() => _sosteniendo = false);
    widget.alSoltar();
    try {
      await _reproductor.stop();
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final double fraccionCaja = widget.valorCajaPorcentaje.clamp(0, 100) / 100;

    return LayoutBuilder(
      builder: (context, restricciones) {
        final double alto = restricciones.maxHeight.isFinite
            ? restricciones.maxHeight
            : 600;
        final double escala = MediaQuery.textScalerOf(context).scale(1);
        // Lo que sobra tras los textos (que crecen con la escala del sistema)
        // se reparte entre el pastel y el botón.
        final double disponible = alto - 270 * escala;
        final double diametroPastel = (disponible * 0.55).clamp(
          90.0,
          _diametroPastelMaximo,
        );
        final double diametroBoton = (disponible * 0.45).clamp(
          80.0,
          _diametroBotonMaximo,
        );
        final double margenTactil = diametroBoton * 0.17;

        return Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Text('Caja Musical', style: EstilosDeMinijuego.titulo),
            const SizedBox(height: 8),
            if (widget.enPeligro) ...[
              Image.asset(
                'assets/images/puppet_advertencia_roja.webp',
                height: 40,
              ),
              const SizedBox(height: 8),
            ] else
              const Text(
                'Mantén la caja sonando',
                style: EstilosDeMinijuego.estado,
              ),
            const SizedBox(height: 16),
            SizedBox(
              width: diametroPastel,
              height: diametroPastel,
              child: CustomPaint(
                painter: _PintorPastelCaja(
                  fraccionLlena: fraccionCaja,
                  enPeligro: widget.enPeligro,
                ),
              ),
            ),
            const SizedBox(height: 8),
            // Listener y no GestureDetector: este último cancela el toque (y
            // la cuerda se soltaba) apenas el dedo se desliza unos píxeles.
            // Aquí solo importa "dedo abajo" y "dedo arriba".
            Listener(
              behavior: HitTestBehavior.opaque,
              onPointerDown: _alBajarDedo,
              onPointerUp: _alSubirODeshacerDedo,
              onPointerCancel: _alSubirODeshacerDedo,
              child: Padding(
                padding: EdgeInsets.all(margenTactil),
                child: AnimatedContainer(
                  duration: const Duration(milliseconds: 120),
                  width: diametroBoton,
                  height: diametroBoton,
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    color: _sosteniendo ? AppColors.acento : AppColors.panel,
                    border: Border.all(
                      color: _sosteniendo
                          ? AppColors.textoPrimario
                          : AppColors.panelBorde,
                      width: _sosteniendo ? 6 : 2,
                    ),
                  ),
                  child: Icon(
                    Icons.music_note,
                    color: _sosteniendo ? AppColors.fondo : AppColors.acento,
                    size: diametroBoton * 0.44,
                  ),
                ),
              ),
            ),
            Text(
              _sosteniendo
                  ? 'DANDO CUERDA…'
                  : 'Mantén presionado para darle cuerda',
              style: EstilosDeMinijuego.estado.copyWith(
                fontWeight: _sosteniendo ? FontWeight.bold : FontWeight.w500,
              ),
            ),
          ],
        );
      },
    );
  }
}

/// Dibuja el "pastel" (gráfico circular relleno, no un anillo delgado)
/// que representa el nivel de la caja: empieza arriba (12 en punto) y
/// se dibuja en sentido horario, cubriendo `fraccionLlena * 360°`.
class _PintorPastelCaja extends CustomPainter {
  final double fraccionLlena;
  final bool enPeligro;

  const _PintorPastelCaja({
    required this.fraccionLlena,
    required this.enPeligro,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final Offset centro = Offset(size.width / 2, size.height / 2);
    final double radio = size.width / 2;

    final Paint pintorFondo = Paint()
      ..color = AppColors.panel
      ..style = PaintingStyle.fill;
    canvas.drawCircle(centro, radio, pintorFondo);

    final Paint pintorBorde = Paint()
      ..color = AppColors.panelBorde
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2;
    canvas.drawCircle(centro, radio, pintorBorde);

    if (fraccionLlena <= 0) return;

    final Paint pintorRelleno = Paint()
      ..color = enPeligro ? AppColors.peligro : AppColors.acento
      ..style = PaintingStyle.fill;

    // Un barrido de exactamente 2*pi colapsa (Skia no dibuja nada cuando el
    // ángulo de inicio y fin coinciden) — se limita a un pelo menos de
    // vuelta completa para que el 100% se vea como círculo lleno.
    final double anguloBarrido = min(2 * pi * fraccionLlena, 2 * pi - 0.001);
    final Path pastel = Path()
      ..moveTo(centro.dx, centro.dy)
      ..arcTo(
        Rect.fromCircle(center: centro, radius: radio),
        -pi / 2, // empieza arriba (12 en punto)
        anguloBarrido,
        false,
      )
      ..close();
    canvas.drawPath(pastel, pintorRelleno);
  }

  @override
  bool shouldRepaint(covariant _PintorPastelCaja oldDelegate) {
    return oldDelegate.fraccionLlena != fraccionLlena ||
        oldDelegate.enPeligro != enPeligro;
  }
}
