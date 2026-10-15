import 'dart:async';
import 'package:flutter/material.dart';
import '../models/task.dart';
import 'encabezado_de_minijuego.dart';

/// Minijuego "Subir Datos": el jugador mantiene el dedo apoyado en la
/// pantalla; mientras lo mantiene, una barra de progreso avanza. Si
/// suelta antes de completarse, el progreso se reinicia.
class SubirDatosGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool exito, Map<String, dynamic> datos) onComplete;

  const SubirDatosGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<SubirDatosGameWidget> createState() => _SubirDatosGameWidgetState();
}

class _SubirDatosGameWidgetState extends State<SubirDatosGameWidget> {
  static const Duration _duracionSubida = Duration(seconds: 3);

  int _segundosRestantes = 12;
  Timer? _contadorCuentaRegresiva;
  Timer? _temporizadorSubida;
  double _progreso = 0;
  bool _finalizado = false;
  // Solo el primer dedo manda: un segundo toque no debe reiniciar la barra.
  int? _punteroActivo;

  @override
  void initState() {
    super.initState();
    _segundosRestantes = widget.task.duration;
    _contadorCuentaRegresiva = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_finalizado) return;
      setState(() => _segundosRestantes--);
      if (_segundosRestantes <= 0) {
        _finalizar(exito: false);
      }
    });
  }

  @override
  void dispose() {
    _contadorCuentaRegresiva?.cancel();
    _temporizadorSubida?.cancel();
    super.dispose();
  }

  void _finalizar({required bool exito}) {
    if (_finalizado) return;
    _finalizado = true;
    _contadorCuentaRegresiva?.cancel();
    _temporizadorSubida?.cancel();
    widget.onComplete(exito, {'progreso_final': _progreso});
  }

  void _alBajarDedo(PointerDownEvent evento) {
    if (_punteroActivo != null || _finalizado) return;
    setState(() => _punteroActivo = evento.pointer);
    _alMantenerPresionado();
  }

  void _alSubirODeshacerDedo(PointerEvent evento) {
    if (evento.pointer != _punteroActivo) return;
    setState(() => _punteroActivo = null);
    _alSoltar();
  }

  void _alMantenerPresionado() {
    const int pasosTotales = 60;
    int pasoActual = (_progreso * pasosTotales).round();
    _temporizadorSubida = Timer.periodic(_duracionSubida ~/ pasosTotales, (
      temporizador,
    ) {
      pasoActual++;
      setState(() => _progreso = pasoActual / pasosTotales);
      if (_progreso >= 1) {
        temporizador.cancel();
        _finalizar(exito: true);
      }
    });
  }

  void _alSoltar() {
    _temporizadorSubida?.cancel();
    setState(() => _progreso = 0);
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, restricciones) {
        final double escala = MediaQuery.textScalerOf(context).scale(1);
        final double alto = restricciones.maxHeight.isFinite
            ? restricciones.maxHeight
            : 600;
        // El botón crece hasta donde deja el resto de la pantalla.
        final double diametro = (alto - 260 * escala).clamp(90.0, 200.0);
        return _cuerpo(diametro);
      },
    );
  }

  Widget _cuerpo(double diametro) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        EncabezadoDeMinijuego(
          titulo: 'Subir Datos',
          segundosRestantes: _segundosRestantes,
        ),
        const SizedBox(height: 32),
        const Text(
          'Mantén presionado para subir el reporte',
          style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 16),
        // Listener y no GestureDetector: este último cancela el toque (y la
        // barra volvía a cero) apenas el dedo se desliza unos píxeles.
        Listener(
          behavior: HitTestBehavior.opaque,
          onPointerDown: _alBajarDedo,
          onPointerUp: _alSubirODeshacerDedo,
          onPointerCancel: _alSubirODeshacerDedo,
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Container(
              width: diametro,
              height: diametro,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: _punteroActivo != null
                    ? Colors.indigo.shade300
                    : Colors.indigo,
                border: Border.all(
                  color: Colors.white,
                  width: _punteroActivo != null ? 6 : 0,
                ),
              ),
              child: const Icon(
                Icons.cloud_upload,
                size: 88,
                color: Colors.white,
              ),
            ),
          ),
        ),
        const SizedBox(height: 16),
        LinearProgressIndicator(value: _progreso, minHeight: 14),
      ],
    );
  }
}
