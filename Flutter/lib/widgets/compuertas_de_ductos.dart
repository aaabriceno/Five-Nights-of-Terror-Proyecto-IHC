import 'package:flutter/material.dart';

import '../utils/colors.dart';

/// Botón de la compuerta de un ducto: un toque la cierra y otro la abre. Va en
/// la misma fila que los botones de cámara, en el extremo de su lado (la
/// compuerta izquierda a la izquierda de todo), grande para acertarlo sin
/// mirar. Responde al apoyar el dedo (`Listener`, como la cámara), no al
/// soltarlo: cerrar a tiempo es lo que salva al jugador.
///
/// Solo pide el cambio; el estado real lo decide Unity (`estado_oficina`). Sin
/// batería no se puede cerrar.
class BotonDeCompuerta extends StatelessWidget {
  /// 'izquierda' o 'derecha'.
  final String lado;
  final bool cerrada;
  final int bateria;

  /// true = pide cerrarla, false = pide abrirla.
  final void Function(bool cerrar) alCambiar;

  const BotonDeCompuerta({
    super.key,
    required this.lado,
    required this.cerrada,
    required this.bateria,
    required this.alCambiar,
  });

  static const double ancho = 190;
  static const double alto = 96;

  bool get _sinBateria => !cerrada && bateria <= 0;

  @override
  Widget build(BuildContext context) {
    final Color color = cerrada
        ? AppColors.peligro
        : _sinBateria
            ? AppColors.panelBorde
            : AppColors.textoSecundario;
    final String estado = cerrada ? 'CERRADA' : (_sinBateria ? 'SIN BATERÍA' : 'ABIERTA');
    return Semantics(
      label: '${cerrada ? 'Abrir' : 'Cerrar'} la compuerta $lado',
      button: true,
      enabled: !_sinBateria,
      excludeSemantics: true,
      child: Listener(
        key: Key('compuerta-$lado'),
        behavior: HitTestBehavior.opaque,
        onPointerDown: (_) {
          if (!_sinBateria) alCambiar(!cerrada);
        },
        child: Container(
          width: ancho,
          height: alto,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: cerrada ? const Color(0xFF3A1414) : AppColors.panel,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: color, width: cerrada ? 3 : 2),
          ),
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(cerrada ? Icons.lock : Icons.lock_open, size: 36, color: color),
                const SizedBox(width: 10),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text('Compuerta $lado',
                        style: const TextStyle(fontSize: 16, color: AppColors.textoSecundario)),
                    Text(estado, style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: color)),
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
