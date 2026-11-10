import 'dart:async';

import 'package:flutter/material.dart';

import '../models/aviso_de_companero.dart';
import '../utils/colors.dart';

/// Lo que el jugador 1 debe saber del jugador 2, siempre a la vista bajo el
/// reloj de la noche:
///
/// * **El aviso** de lo último que hizo el compañero ("Tu compañero puso una
///   trampa", "Tu compañero te salvó de Puppet"...). Grande, para leerlo de
///   reojo; se quita solo a los [duracionDelAviso].
/// * **Dos datos que no caducan**: si el compañero está conectado (con él los
///   animatrónicos van más rápido) y cuánto más rápido se descarga la caja de
///   Puppet tras un rescate. Son lo que cambia las cuentas del jugador 1, así
///   que se quedan visibles mientras valgan.
///
/// Puramente presentacional: GameScreen le pasa lo que lee de
/// `GameProvider.session`.
class FranjaDelCompanero extends StatefulWidget {
  static const Duration duracionDelAviso = Duration(seconds: 5);

  final AvisoDeCompanero? aviso;
  final bool? companeroConectado;
  final double multaDeCaja;

  /// Cuánto dura ahora la caja de Puppet; null = Unity no lo dice (se usa el factor).
  final double? segundosDeCaja;

  const FranjaDelCompanero({
    super.key,
    this.aviso,
    this.companeroConectado,
    this.multaDeCaja = 1,
    this.segundosDeCaja,
  });

  @override
  State<FranjaDelCompanero> createState() => _FranjaDelCompaneroState();
}

class _FranjaDelCompaneroState extends State<FranjaDelCompanero> {
  Timer? _temporizador;
  bool _avisoVisible = false;

  @override
  void initState() {
    super.initState();
    _mostrarAviso();
  }

  @override
  void didUpdateWidget(FranjaDelCompanero anterior) {
    super.didUpdateWidget(anterior);
    // Un aviso nuevo (otro objeto) reinicia la cuenta de los 5 s.
    if (!identical(anterior.aviso, widget.aviso)) _mostrarAviso();
  }

  void _mostrarAviso() {
    _temporizador?.cancel();
    _avisoVisible = widget.aviso != null;
    if (!_avisoVisible) return;
    _temporizador = Timer(FranjaDelCompanero.duracionDelAviso, () {
      if (mounted) setState(() => _avisoVisible = false);
    });
  }

  @override
  void dispose() {
    _temporizador?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final AvisoDeCompanero? aviso = _avisoVisible ? widget.aviso : null;
    final List<Widget> chips = <Widget>[
      if (widget.companeroConectado != null)
        _Chip(
          icono: widget.companeroConectado! ? Icons.people : Icons.person_off,
          texto: widget.companeroConectado! ? 'Compañero conectado' : 'Compañero desconectado',
          color: widget.companeroConectado! ? AppColors.exito : AppColors.peligro,
        ),
      if (widget.multaDeCaja > 1)
        _Chip(
          icono: Icons.timer,
          texto: widget.segundosDeCaja != null
              ? 'Caja de Puppet dura ${AvisoDeCompanero.formatearSegundos(widget.segundosDeCaja!)} s'
              : 'Caja de Puppet ${AvisoDeCompanero.formatearMulta(widget.multaDeCaja)} más rápida',
          color: AppColors.advertencia,
        ),
    ];
    if (aviso == null && chips.isEmpty) return const SizedBox.shrink();

    return Padding(
      padding: const EdgeInsets.only(top: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          if (aviso != null) _Aviso(aviso: aviso),
          if (aviso != null && chips.isNotEmpty) const SizedBox(height: 8),
          if (chips.isNotEmpty) Wrap(spacing: 12, runSpacing: 8, children: chips),
        ],
      ),
    );
  }
}

class _Aviso extends StatelessWidget {
  final AvisoDeCompanero aviso;
  const _Aviso({required this.aviso});

  @override
  Widget build(BuildContext context) {
    final Color color = aviso.esMalaNoticia ? AppColors.peligro : AppColors.exito;
    return Semantics(
      liveRegion: true,
      container: true,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
        decoration: BoxDecoration(
          color: AppColors.panel,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: color, width: 2),
        ),
        child: Row(
          children: <Widget>[
            Icon(aviso.esMalaNoticia ? Icons.person_off : Icons.people, size: 36, color: color),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    aviso.titulo,
                    style: const TextStyle(fontSize: 26, fontWeight: FontWeight.bold),
                  ),
                  if (aviso.detalle != null)
                    Text(
                      aviso.detalle!,
                      style: const TextStyle(fontSize: 22, color: AppColors.textoSecundario),
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  final IconData icono;
  final String texto;
  final Color color;
  const _Chip({required this.icono, required this.texto, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
      decoration: BoxDecoration(
        color: AppColors.panel,
        borderRadius: BorderRadius.circular(24),
        border: Border.all(color: color),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          Icon(icono, size: 24, color: color),
          const SizedBox(width: 8),
          Text(texto, style: const TextStyle(fontSize: 20)),
        ],
      ),
    );
  }
}
