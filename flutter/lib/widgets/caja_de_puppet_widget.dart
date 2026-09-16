import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import '../utils/colors.dart';

/// Zona táctil fija que representa la manivela de la caja de música de
/// Puppet: el jugador debe mantenerla presionada para evitar que la
/// caja se vacíe y Puppet salga a atacar (mecánica de FNAF2 real).
///
/// Se superpone sobre TODA la pantalla del juego (menú de tareas y
/// cualquier minijuego en curso), porque en el original esta amenaza
/// compite por la atención del jugador con todo lo demás — no es una
/// tarea más de la lista.
///
/// Solo manda dos mensajes al servidor (`dar_cuerda_inicio`/
/// `dar_cuerda_fin`) al empezar/terminar el gesto, nunca en cada frame
/// — el servidor decide cuánto sube la caja mientras se mantiene
/// presionado. La alerta visual (`enPeligro`) también la decide el
/// servidor (umbral de 20%), acá solo se refleja con un ícono
/// discreto de advertencia parpadeante, nunca texto literal.
class CajaDePuppetWidget extends StatefulWidget {
  final bool enPeligro;
  final VoidCallback alEmpezarASostener;
  final VoidCallback alSoltar;

  const CajaDePuppetWidget({
    super.key,
    required this.enPeligro,
    required this.alEmpezarASostener,
    required this.alSoltar,
  });

  @override
  State<CajaDePuppetWidget> createState() => _CajaDePuppetWidgetState();
}

class _CajaDePuppetWidgetState extends State<CajaDePuppetWidget>
    with SingleTickerProviderStateMixin {
  static const String _rutaSonidoCuerda =
      'sounds/FNaF_2_-_Dándole_cuerda_a_la_caja_de_música.ogg';

  final AudioPlayer _reproductor = AudioPlayer();
  late final AnimationController _controladorParpadeo;
  bool _sosteniendo = false;

  @override
  void initState() {
    super.initState();
    _controladorParpadeo = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 600),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _reproductor.dispose();
    _controladorParpadeo.dispose();
    super.dispose();
  }

  Future<void> _alPresionar() async {
    if (_sosteniendo) return;
    _sosteniendo = true;
    widget.alEmpezarASostener();
    await _reproductor.setReleaseMode(ReleaseMode.loop);
    await _reproductor.play(AssetSource(_rutaSonidoCuerda));
  }

  Future<void> _alSoltar() async {
    if (!_sosteniendo) return;
    _sosteniendo = false;
    widget.alSoltar();
    await _reproductor.stop();
  }

  @override
  Widget build(BuildContext context) {
    return Positioned(
      right: 16,
      bottom: 16,
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (widget.enPeligro) ...[
            FadeTransition(
              opacity: _controladorParpadeo,
              child: const Icon(
                Icons.warning_amber_rounded,
                color: AppColors.peligro,
                size: 28,
              ),
            ),
            const SizedBox(width: 8),
          ],
          GestureDetector(
            onTapDown: (_) => _alPresionar(),
            onTapUp: (_) => _alSoltar(),
            onTapCancel: _alSoltar,
            child: Container(
              width: 64,
              height: 64,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: _sosteniendo
                    ? AppColors.acento
                    : AppColors.panel,
                border: Border.all(
                  color: widget.enPeligro
                      ? AppColors.peligro
                      : AppColors.panelBorde,
                  width: 2,
                ),
              ),
              child: Icon(
                Icons.music_note,
                color: _sosteniendo ? AppColors.fondo : AppColors.acento,
                size: 28,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
