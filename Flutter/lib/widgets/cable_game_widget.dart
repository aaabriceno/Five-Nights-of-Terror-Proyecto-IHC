import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';
import '../models/task.dart';

/// Formas que identifican cada cable además de su color. Sin esto el
/// minijuego sería injugable para quien no distingue rojo de verde (el par
/// más común de daltonismo), porque emparejar cable y conector dependería
/// solo del color — ver WCAG 1.4.1 "Uso del color".
enum FormaDeCable { circulo, triangulo, cuadrado, rombo }

/// Minijuego "Conectar Cables": arrastra cada cable hasta su conector
/// correspondiente, emparejando por forma y color. Los conectores se
/// mezclan aleatoriamente.
class CableGameWidget extends StatefulWidget {
  final Task task;
  final void Function(bool success, List<Map<String, dynamic>> connections)
      onComplete;

  const CableGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  State<CableGameWidget> createState() => _CableGameWidgetState();
}

class _CableGameWidgetState extends State<CableGameWidget> {
  static const List<Color> _cableColors = [
    Colors.red,
    Colors.blue,
    Colors.green,
    Colors.amber,
  ];
  static const List<String> _cableNames = ['red', 'blue', 'green', 'yellow'];
  static const List<FormaDeCable> _cableFormas = [
    FormaDeCable.circulo,
    FormaDeCable.triangulo,
    FormaDeCable.cuadrado,
    FormaDeCable.rombo,
  ];

  late List<int> _connectorOrder;
  final Map<int, int> _connections = {}; // índice cable -> índice conector
  int? _draggingCableIndex;
  Offset? _dragPosition;
  int _remainingSeconds = 30;
  Timer? _countdownTimer;
  bool _finished = false;

  @override
  void initState() {
    super.initState();
    _connectorOrder = List<int>.generate(_cableColors.length, (i) => i)
      ..shuffle(Random());
    _remainingSeconds = widget.task.duration;
    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (_finished) return;
      setState(() {
        _remainingSeconds--;
      });
      if (_remainingSeconds <= 0) {
        _finish(success: false);
      }
    });
  }

  @override
  void dispose() {
    _countdownTimer?.cancel();
    super.dispose();
  }

  void _finish({required bool success}) {
    if (_finished) return;
    _finished = true;
    _countdownTimer?.cancel();

    final List<Map<String, dynamic>> connections = [];
    for (int cableIndex = 0; cableIndex < _cableColors.length; cableIndex++) {
      final int? connectorIndex = _connections[cableIndex];
      connections.add({
        'cable': _cableNames[cableIndex],
        'connector':
            connectorIndex != null ? _cableNames[connectorIndex] : null,
        'correct': connectorIndex == cableIndex,
      });
    }

    widget.onComplete(success, connections);
  }

  bool get _allConnectedCorrectly =>
      _connections.length == _cableColors.length &&
      _connections.entries.every((entry) => entry.key == entry.value);

  List<Offset> _nodePositions(Size size, {required bool left}) {
    final double x = left ? size.width * 0.15 : size.width * 0.85;
    final double spacing = size.height / (_cableColors.length + 1);
    return List<Offset>.generate(
      _cableColors.length,
      (i) => Offset(x, spacing * (i + 1)),
    );
  }

  int? _connectorAt(Offset position, List<Offset> connectorPositions) {
    const double hitRadius = 30;
    for (int i = 0; i < connectorPositions.length; i++) {
      if ((connectorPositions[i] - position).distance <= hitRadius) {
        return i;
      }
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          'Conectar Cables',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text('Tiempo restante: $_remainingSeconds s'),
        const SizedBox(height: 8),
        Expanded(
          child: LayoutBuilder(
            builder: (context, constraints) {
              final Size size =
                  Size(constraints.maxWidth, constraints.maxHeight);
              final List<Offset> cablePositions =
                  _nodePositions(size, left: true);
              final List<Offset> connectorPositions =
                  _nodePositions(size, left: false);

              return GestureDetector(
                onPanStart: (details) {
                  final int? cableIndex =
                      _connectorAt(details.localPosition, cablePositions);
                  if (cableIndex != null) {
                    setState(() {
                      _draggingCableIndex = cableIndex;
                      _dragPosition = details.localPosition;
                    });
                  }
                },
                onPanUpdate: (details) {
                  if (_draggingCableIndex == null) return;
                  setState(() {
                    _dragPosition = details.localPosition;
                  });
                },
                onPanEnd: (details) {
                  if (_draggingCableIndex == null || _dragPosition == null) {
                    return;
                  }
                  final int? connectorSlot =
                      _connectorAt(_dragPosition!, connectorPositions);
                  if (connectorSlot != null) {
                    final int connectorIndex = _connectorOrder[connectorSlot];
                    setState(() {
                      _connections[_draggingCableIndex!] = connectorIndex;
                    });
                  }
                  setState(() {
                    _draggingCableIndex = null;
                    _dragPosition = null;
                  });
                },
                child: CustomPaint(
                  size: size,
                  painter: _CableGamePainter(
                    cableColors: _cableColors,
                    cableFormas: _cableFormas,
                    cablePositions: cablePositions,
                    connectorPositions: connectorPositions,
                    connectorOrder: _connectorOrder,
                    connections: _connections,
                    draggingCableIndex: _draggingCableIndex,
                    dragPosition: _dragPosition,
                  ),
                ),
              );
            },
          ),
        ),
        const SizedBox(height: 8),
        Text('Conectados: ${_connections.length}/${_cableColors.length}'),
        const SizedBox(height: 8),
        ElevatedButton(
          onPressed: _allConnectedCorrectly
              ? () => _finish(success: true)
              : null,
          child: const Text('Enviar'),
        ),
      ],
    );
  }
}

class _CableGamePainter extends CustomPainter {
  final List<Color> cableColors;
  final List<FormaDeCable> cableFormas;
  final List<Offset> cablePositions;
  final List<Offset> connectorPositions;
  final List<int> connectorOrder;
  final Map<int, int> connections;
  final int? draggingCableIndex;
  final Offset? dragPosition;

  _CableGamePainter({
    required this.cableColors,
    required this.cableFormas,
    required this.cablePositions,
    required this.connectorPositions,
    required this.connectorOrder,
    required this.connections,
    required this.draggingCableIndex,
    required this.dragPosition,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final Paint nodePaint = Paint()..style = PaintingStyle.fill;
    final Paint linePaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 6;

    // Cables (origen, izquierda).
    for (int i = 0; i < cablePositions.length; i++) {
      nodePaint.color = cableColors[i];
      _dibujarForma(canvas, cablePositions[i], 18, cableFormas[i], nodePaint);
    }

    // Conectores (destino, derecha) — la forma y el color mostrados son los
    // del cable que le corresponde según connectorOrder, para que el jugador
    // vea a qué cable pertenece cada conector.
    for (int slot = 0; slot < connectorPositions.length; slot++) {
      final int cableIndex = connectorOrder[slot];
      final FormaDeCable forma = cableFormas[cableIndex];
      nodePaint.color = cableColors[cableIndex].withValues(alpha: 0.3);
      _dibujarForma(canvas, connectorPositions[slot], 22, forma, nodePaint);
      final Paint borderPaint = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3
        ..color = cableColors[cableIndex];
      _dibujarForma(canvas, connectorPositions[slot], 22, forma, borderPaint);
    }

    // Conexiones ya hechas. Una conexión incorrecta se dibuja punteada
    // además de cambiar de color: el acierto no debe depender de distinguir
    // rojo de verde.
    connections.forEach((cableIndex, connectorIndex) {
      final int slot = connectorOrder.indexOf(connectorIndex);
      final bool correct = cableIndex == connectorIndex;
      linePaint.color =
          correct ? cableColors[cableIndex] : Colors.red.withValues(alpha: 0.6);
      if (correct) {
        canvas.drawLine(
          cablePositions[cableIndex],
          connectorPositions[slot],
          linePaint,
        );
      } else {
        _dibujarLineaPunteada(
          canvas,
          cablePositions[cableIndex],
          connectorPositions[slot],
          linePaint,
        );
      }
    });

    // Cable en arrastre.
    if (draggingCableIndex != null && dragPosition != null) {
      linePaint.color = cableColors[draggingCableIndex!];
      canvas.drawLine(
        cablePositions[draggingCableIndex!],
        dragPosition!,
        linePaint,
      );
    }
  }

  /// Dibuja la forma del cable centrada en [centro], inscrita en un círculo
  /// de radio [radio] para que todas ocupen un área similar.
  void _dibujarForma(
    Canvas canvas,
    Offset centro,
    double radio,
    FormaDeCable forma,
    Paint paint,
  ) {
    switch (forma) {
      case FormaDeCable.circulo:
        canvas.drawCircle(centro, radio, paint);
      case FormaDeCable.cuadrado:
        final double lado = radio * 1.6;
        canvas.drawRect(
          Rect.fromCenter(center: centro, width: lado, height: lado),
          paint,
        );
      case FormaDeCable.triangulo:
        canvas.drawPath(
          _poligono(centro, radio, lados: 3, rotacion: -pi / 2),
          paint,
        );
      case FormaDeCable.rombo:
        canvas.drawPath(
          _poligono(centro, radio, lados: 4, rotacion: -pi / 2),
          paint,
        );
    }
  }

  Path _poligono(
    Offset centro,
    double radio, {
    required int lados,
    required double rotacion,
  }) {
    final Path path = Path();
    for (int i = 0; i < lados; i++) {
      final double angulo = rotacion + i * 2 * pi / lados;
      final Offset punto = Offset(
        centro.dx + radio * cos(angulo),
        centro.dy + radio * sin(angulo),
      );
      if (i == 0) {
        path.moveTo(punto.dx, punto.dy);
      } else {
        path.lineTo(punto.dx, punto.dy);
      }
    }
    return path..close();
  }

  void _dibujarLineaPunteada(Canvas canvas, Offset desde, Offset hasta, Paint paint) {
    const double largoTrazo = 10;
    const double largoHueco = 8;
    final double distancia = (hasta - desde).distance;
    if (distancia == 0) return;
    final Offset direccion = (hasta - desde) / distancia;

    double recorrido = 0;
    while (recorrido < distancia) {
      final double fin = min(recorrido + largoTrazo, distancia);
      canvas.drawLine(
        desde + direccion * recorrido,
        desde + direccion * fin,
        paint,
      );
      recorrido = fin + largoHueco;
    }
  }

  @override
  bool shouldRepaint(covariant _CableGamePainter oldDelegate) {
    return oldDelegate.connections.length != connections.length ||
        oldDelegate.draggingCableIndex != draggingCableIndex ||
        oldDelegate.dragPosition != dragPosition;
  }
}
