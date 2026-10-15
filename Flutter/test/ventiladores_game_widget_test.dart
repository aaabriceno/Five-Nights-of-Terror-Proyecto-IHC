import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/ventiladores_game_widget.dart';

void main() {
  Task tarea() => Task(
        taskId: 1,
        taskType: 'ventiladores',
        duration: 90,
        description: 'Ingresa el código',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  Future<void> montar(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1500, 650);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(body: VentiladoresGameWidget(task: tarea(), onComplete: (_, __) {})),
    ));
  }

  Finder boton(String texto) =>
      find.widgetWithText(ElevatedButton, texto);

  testWidgets('teclado en cuadrícula 3×4: borrar queda a la izquierda del 0', (tester) async {
    await montar(tester);
    final Offset cero = tester.getCenter(boton('0'));
    final Offset borrar = tester.getCenter(find.byIcon(Icons.backspace));
    expect((borrar.dy - cero.dy).abs(), lessThan(5), reason: 'misma fila que el 0');
    expect(borrar.dx, lessThan(cero.dx), reason: 'a la izquierda del 0');
    // Filas: 1-2-3 / 4-5-6 / 7-8-9 / borrar-0-…
    final double y1 = tester.getCenter(boton('1')).dy;
    expect((tester.getCenter(boton('2')).dy - y1).abs(), lessThan(5));
    expect((tester.getCenter(boton('3')).dy - y1).abs(), lessThan(5));
    expect(tester.getCenter(boton('4')).dy, greaterThan(y1 + 20));
    expect(tester.getCenter(boton('7')).dx, closeTo(tester.getCenter(boton('1')).dx, 5));
  });

  testWidgets('los botones del teclado son grandes (≥ 88 px)', (tester) async {
    await montar(tester);
    for (final Finder f in <Finder>[boton('5'), boton('0'), find.ancestor(of: find.byIcon(Icons.backspace), matching: find.byType(OutlinedButton))]) {
      final Size t = tester.getSize(f);
      expect(t.width, greaterThanOrEqualTo(88));
      expect(t.height, greaterThanOrEqualTo(88));
    }
  });

  testWidgets('borrar quita el último dígito ingresado', (tester) async {
    await montar(tester);
    await tester.tap(boton('7'));
    await tester.tap(boton('3'));
    await tester.pump();
    expect(find.text('7 3 _ _'), findsOneWidget);
    await tester.tap(find.byIcon(Icons.backspace));
    await tester.pump();
    expect(find.text('7 _ _ _'), findsOneWidget);
  });
}
