import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/cable_game_widget.dart';

void main() {
  Task tarea() => Task(
        taskId: 1,
        taskType: 'cables',
        duration: 90,
        description: 'Conecta los cables',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  // Superficie parecida a la zona de juego de la tablet.
  Future<Rect> montar(WidgetTester tester) async {
    tester.view.physicalSize = const Size(800, 700);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: CableGameWidget(task: tarea(), onComplete: (_, __) {}),
      ),
    ));
    final Finder lienzo = find.descendant(
      of: find.byType(GestureDetector),
      matching: find.byType(CustomPaint),
    );
    return tester.getRect(lienzo.first);
  }

  // Misma geometría que el widget: nodos al 15 % y 85 % del ancho.
  Offset origen(Rect area, int i) =>
      area.topLeft + Offset(area.width * 0.15, area.height / 5 * (i + 1));
  Offset destino(Rect area, int i) =>
      area.topLeft + Offset(area.width * 0.85, area.height / 5 * (i + 1));

  testWidgets('soltar un poco al lado del conector (50 px) igual conecta', (tester) async {
    final Rect area = await montar(tester);
    final TestGesture dedo = await tester.startGesture(origen(area, 0));
    await dedo.moveTo(destino(area, 2) + const Offset(-50, 0));
    await dedo.up();
    await tester.pump();
    expect(find.text('Conectados: 1/4'), findsOneWidget);
  });

  testWidgets('empezar el arrastre a 50 px del cable también agarra', (tester) async {
    final Rect area = await montar(tester);
    final TestGesture dedo =
        await tester.startGesture(origen(area, 1) + const Offset(0, 40));
    await dedo.moveTo(destino(area, 3));
    await dedo.up();
    await tester.pump();
    expect(find.text('Conectados: 1/4'), findsOneWidget);
  });

  testWidgets('soltar lejos de todo (200 px) no conecta nada', (tester) async {
    final Rect area = await montar(tester);
    final TestGesture dedo = await tester.startGesture(origen(area, 0));
    await dedo.moveTo(destino(area, 2) + const Offset(-250, 0));
    await dedo.up();
    await tester.pump();
    expect(find.text('Conectados: 0/4'), findsOneWidget);
  });
}
