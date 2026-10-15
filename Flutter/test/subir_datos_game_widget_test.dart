import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/subir_datos_game_widget.dart';

void main() {
  Task tarea() => Task(
        taskId: 1,
        taskType: 'subir_datos',
        duration: 12,
        description: 'Sube el reporte',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  Future<bool?> mantener(
    WidgetTester tester, {
    required Offset desplazamiento,
    required Duration tiempo,
  }) async {
    bool? exito;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: SubirDatosGameWidget(
          task: tarea(),
          onComplete: (ok, _) => exito = ok,
        ),
      ),
    ));
    final TestGesture dedo =
        await tester.startGesture(tester.getCenter(find.byIcon(Icons.cloud_upload)));
    await tester.pump(const Duration(milliseconds: 100));
    if (desplazamiento != Offset.zero) await dedo.moveBy(desplazamiento);
    await tester.pump(tiempo);
    await dedo.up();
    await tester.pump(const Duration(seconds: 13)); // agota el contador
    return exito;
  }

  testWidgets('mantener 3.5 s con el dedo quieto completa la subida', (tester) async {
    expect(
      await mantener(tester,
          desplazamiento: Offset.zero, tiempo: const Duration(milliseconds: 3500)),
      isTrue,
    );
  });

  testWidgets('un dedo que se desliza unos píxeles no reinicia la subida', (tester) async {
    expect(
      await mantener(tester,
          desplazamiento: const Offset(40, 25), tiempo: const Duration(milliseconds: 3500)),
      isTrue,
    );
  });
}
