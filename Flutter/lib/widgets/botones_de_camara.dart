import 'dart:async';

import 'package:flutter/material.dart';

import '../utils/colors.dart';

/// Los dos botones con los que el jugador 1 mueve la cámara del juego desde la
/// tablet. Se mantienen apretados: mientras el dedo siga apoyado la cámara gira
/// hacia ese lado; al soltar, vuelve sola al frente. Mirar un ducto cuesta
/// atención (hay que seguir apretando), que es justo la tensión del juego.
///
/// Son grandes y siempre están en el mismo lugar, para acertarlos sin mirar la
/// tablet. Usan `Listener` (no `GestureDetector`) para que un toque no compita
/// con los gestos de las tareas de arriba.
///
/// La tablet repite la orden cada [repeticion] mientras el dedo sigue apoyado:
/// si se pierde el «soltar» por la red, Unity da el botón por soltado a los
/// 0,5 s sin órdenes.
class BotonesDeCamara extends StatefulWidget {
  /// -1 = izquierda, 1 = derecha, 0 = soltó.
  final void Function(int direccion) alMirar;
  final Duration repeticion;

  /// Lo que va entre los dos botones (la batería de las compuertas), o nada.
  final Widget? centro;

  const BotonesDeCamara({
    super.key,
    required this.alMirar,
    this.repeticion = const Duration(milliseconds: 150),
    this.centro,
  });

  @override
  State<BotonesDeCamara> createState() => _BotonesDeCamaraState();
}

class _BotonesDeCamaraState extends State<BotonesDeCamara> {
  static const double alto = 96;

  int _direccion = 0;
  final Set<int> _punteros = <int>{};
  Timer? _repetidor;

  void _apoyar(int direccion, int puntero) {
    _punteros.add(puntero);
    setState(() => _direccion = direccion);
    widget.alMirar(direccion);
    _repetidor?.cancel();
    _repetidor = Timer.periodic(widget.repeticion, (_) => widget.alMirar(_direccion));
  }

  void _soltar(int direccion, int puntero) {
    _punteros.remove(puntero);
    // Si hay otro dedo apoyado en el otro botón, ese sigue mandando.
    if (_direccion != direccion) return;
    _repetidor?.cancel();
    _repetidor = null;
    if (mounted) setState(() => _direccion = 0);
    widget.alMirar(0);
  }

  @override
  void dispose() {
    _repetidor?.cancel();
    if (_direccion != 0) widget.alMirar(0);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Row(
      children: <Widget>[
        Expanded(child: _boton(clave: 'camara-izquierda', direccion: -1, etiqueta: 'Mirar a la izquierda', icono: Icons.arrow_back_ios_new)),
        const SizedBox(width: 16),
        if (widget.centro != null) ...<Widget>[widget.centro!, const SizedBox(width: 16)],
        Expanded(child: _boton(clave: 'camara-derecha', direccion: 1, etiqueta: 'Mirar a la derecha', icono: Icons.arrow_forward_ios)),
      ],
    );
  }

  Widget _boton({
    required String clave,
    required int direccion,
    required String etiqueta,
    required IconData icono,
  }) {
    final bool apretado = _direccion == direccion;
    final Color color = apretado ? AppColors.acento : AppColors.textoSecundario;
    return Semantics(
      label: etiqueta,
      button: true,
      excludeSemantics: true,
      child: Listener(
        key: Key(clave),
        behavior: HitTestBehavior.opaque,
        onPointerDown: (PointerDownEvent e) => _apoyar(direccion, e.pointer),
        onPointerUp: (PointerUpEvent e) => _soltar(direccion, e.pointer),
        onPointerCancel: (PointerCancelEvent e) => _soltar(direccion, e.pointer),
        child: Container(
          height: alto,
          decoration: BoxDecoration(
            color: apretado ? AppColors.acentoOscuro : AppColors.panel,
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: color, width: apretado ? 3 : 2),
          ),
          // En una tablet más angosta (con las compuertas al lado) el texto se
          // achica en vez de desbordarse.
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  if (direccion < 0) Icon(icono, size: 40, color: color),
                  if (direccion < 0) const SizedBox(width: 16),
                  Text(etiqueta, style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold, color: color)),
                  if (direccion > 0) const SizedBox(width: 16),
                  if (direccion > 0) Icon(icono, size: 40, color: color),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
