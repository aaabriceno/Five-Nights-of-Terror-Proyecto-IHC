import 'dart:math';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import '../utils/colors.dart';

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

class _PuppetGameWidgetState extends State<PuppetGameWidget> {
  static const String _rutaSonidoCuerda =
      'sounds/FNaF_2_-_Dándole_cuerda_a_la_caja_de_música.ogg';

  final AudioPlayer _reproductor = AudioPlayer();
  bool _sosteniendo = false;

  @override
  void dispose() {
    _reproductor.dispose();
    super.dispose();
  }

  Future<void> _alPresionar() async {
    if (_sosteniendo) return;
    setState(() => _sosteniendo = true);
    widget.alEmpezarASostener();
    await _reproductor.setReleaseMode(ReleaseMode.loop);
    await _reproductor.play(AssetSource(_rutaSonidoCuerda));
  }

  Future<void> _alSoltar() async {
    if (!_sosteniendo) return;
    setState(() => _sosteniendo = false);
    widget.alSoltar();
    await _reproductor.stop();
  }

  @override
  Widget build(BuildContext context) {
    final double fraccionCaja = widget.valorCajaPorcentaje.clamp(0, 100) / 100;

    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text('Caja Musical', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 8),
        if (widget.enPeligro) ...[
          Image.asset('assets/images/puppet_advertencia_roja.webp', height: 40),
          const SizedBox(height: 8),
        ] else
          const Text('Mantén la caja sonando'),
        const SizedBox(height: 32),
        SizedBox(
          width: 240,
          height: 240,
          child: CustomPaint(
            painter: _PintorPastelCaja(
              fraccionLlena: fraccionCaja,
              enPeligro: widget.enPeligro,
            ),
          ),
        ),
        const SizedBox(height: 40),
        GestureDetector(
          onTapDown: (_) => _alPresionar(),
          onTapUp: (_) => _alSoltar(),
          onTapCancel: _alSoltar,
          child: Container(
            width: 120,
            height: 120,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: _sosteniendo ? AppColors.acento : AppColors.panel,
              border: Border.all(color: AppColors.panelBorde, width: 2),
            ),
            child: Icon(
              Icons.music_note,
              color: _sosteniendo ? AppColors.fondo : AppColors.acento,
              size: 48,
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          'Mantén presionado para darle cuerda',
          style: Theme.of(context).textTheme.bodySmall,
        ),
      ],
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
