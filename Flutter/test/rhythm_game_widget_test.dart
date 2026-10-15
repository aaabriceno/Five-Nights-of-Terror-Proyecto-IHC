import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/rhythm_game_widget.dart';

void main() {
  testWidgets('los botones del ritmo son grandes (≥100 px) en una tablet', (tester) async {
    tester.view.physicalSize = const Size(800, 700);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: RhythmGameWidget(
          task: Task(
            taskId: 1,
            taskType: 'rhythm',
            duration: 15,
            description: 'Sigue el ritmo',
            difficulty: 1,
            createdAt: DateTime(2026),
            params: const {},
          ),
          onComplete: (_, __) {},
        ),
      ),
    ));
    final Iterable<Element> botones = find.byType(ElevatedButton).evaluate();
    expect(botones, isNotEmpty);
    for (final Element boton in botones) {
      final Size tamano = (boton.renderObject! as RenderBox).size;
      expect(tamano.width, greaterThanOrEqualTo(100));
      expect(tamano.height, greaterThanOrEqualTo(100));
    }
    // Deja correr el contador para que no queden temporizadores pendientes.
    await tester.pump(const Duration(seconds: 20));
  });
}
