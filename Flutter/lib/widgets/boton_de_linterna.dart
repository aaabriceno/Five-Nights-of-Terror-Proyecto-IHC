import 'dart:async';

import 'package:flutter/material.dart';

import '../utils/colors.dart';

/// El botón de la linterna del jugador 1, entre los dos botones de cámara. Se
/// mantiene apretado: mientras el dedo siga apoyado la linterna alumbra hacia
/// donde mira la cámara (alumbrar a Freddy 2 s lo hace retroceder). Muestra la
/// batería, que es la misma de las compuertas: verde con más de la mitad,
/// ámbar hasta el 20 % y roja por debajo; en 0 no se prende.
///
/// Como la cámara, responde al apoyar el dedo (`Listener`) y repite la orden
/// cada [repeticion]: si se pierde el «soltar» por la red, Unity la apaga sola
/// a los 0,5 s sin órdenes.
class BotonDeLinterna extends StatefulWidget {
  final int bateria;

  /// Lo que dice Unity (`estado_oficina.linterna`).
  final bool encendida;

  /// true = mantenerla prendida (se repite), false = soltó.
  final void Function(bool encender) alCambiar;
  final Duration repeticion;

  const BotonDeLinterna({
    super.key,
    required this.bateria,
    required this.encendida,
    required this.alCambiar,
    this.repeticion = const Duration(milliseconds: 150),
  });

  static const double ancho = 176;
  static const double alto = 96;

  @override
  State<BotonDeLinterna> createState() => _BotonDeLinternaState();
}

class _BotonDeLinternaState extends State<BotonDeLinterna> {
  final Set<int> _punteros = <int>{};
  Timer? _repetidor;

  bool get _apoyada => _repetidor != null;
  bool get _sinBateria => widget.bateria <= 0;

  void _apoyar(int puntero) {
    if (_sinBateria) return;
    _punteros.add(puntero);
    if (_apoyada) return;
    setState(() {
      _repetidor = Timer.periodic(widget.repeticion, (_) => widget.alCambiar(true));
    });
    widget.alCambiar(true);
  }

  void _soltar(int puntero) {
    _punteros.remove(puntero);
    if (_punteros.isEmpty) _apagar();
  }

  void _apagar() {
    if (!_apoyada) return;
    _repetidor!.cancel();
    _punteros.clear();
    if (mounted) setState(() => _repetidor = null);
    widget.alCambiar(false);
  }

  @override
  void didUpdateWidget(BotonDeLinterna anterior) {
    super.didUpdateWidget(anterior);
    // Se acabó la batería con el dedo apoyado: deja de pedirla.
    if (_sinBateria && _apoyada) {
      _repetidor!.cancel();
      _repetidor = null;
      _punteros.clear();
      widget.alCambiar(false);
    }
  }

  @override
  void dispose() {
    if (_apoyada) {
      _repetidor!.cancel();
      widget.alCambiar(false);
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final bool prendida = !_sinBateria && (_apoyada || widget.encendida);
    final Color colorBateria = widget.bateria > 50
        ? AppColors.exito
        : widget.bateria > 20
            ? AppColors.advertencia
            : AppColors.peligro;
    final Color borde = prendida ? AppColors.acento : (_sinBateria ? AppColors.panelBorde : AppColors.textoSecundario);
    return Semantics(
      label: 'Linterna: mantén apretado para alumbrar. Batería: ${widget.bateria} %',
      button: true,
      enabled: !_sinBateria,
      excludeSemantics: true,
      child: Listener(
        key: const Key('linterna'),
        behavior: HitTestBehavior.opaque,
        onPointerDown: (PointerDownEvent e) => _apoyar(e.pointer),
        onPointerUp: (PointerUpEvent e) => _soltar(e.pointer),
        onPointerCancel: (PointerCancelEvent e) => _soltar(e.pointer),
        child: Container(
          width: BotonDeLinterna.ancho,
          height: BotonDeLinterna.alto,
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: prendida ? AppColors.acentoOscuro : AppColors.panel,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: borde, width: prendida ? 3 : 2),
          ),
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: <Widget>[
                Icon(prendida ? Icons.flashlight_on : Icons.flashlight_off, size: 36, color: borde),
                const SizedBox(width: 10),
                Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    const Text('Linterna', style: TextStyle(fontSize: 16, color: AppColors.textoSecundario)),
                    Text('${widget.bateria} %',
                        style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: colorBateria)),
                    if (_sinBateria)
                      const Text('SIN BATERÍA',
                          style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: AppColors.peligro)),
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
