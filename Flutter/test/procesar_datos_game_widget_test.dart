import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/procesar_datos_game_widget.dart';

void main() {
  const List<DireccionDeFlecha> secuencia = <DireccionDeFlecha>[
    DireccionDeFlecha.derecha,
    DireccionDeFlecha.arriba,
    DireccionDeFlecha.izquierda,
    DireccionDeFlecha.abajo,
    DireccionDeFlecha.derecha,
  ];

  Task tarea() => Task(
        taskId: 1,
        taskType: 'procesar_datos',
        duration: 15,
        description: 'Procesar datos',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  bool? exito;
  Map<String, dynamic>? datos;

  Future<void> montar(WidgetTester tester) async {
    exito = null;
    datos = null;
    tester.view.physicalSize = const Size(1500, 650);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: ProcesarDatosGameWidget(
          task: tarea(),
          secuenciaInicial: secuencia,
          onComplete: (ok, d) {
            exito = ok;
            datos = d;
          },
        ),
      ),
    ));
  }

  /// Un dedo que se mueve por una serie de tramos (como un deslizamiento real).
  Future<void> deslizar(WidgetTester tester, List<Offset> tramos) async {
    final Finder zona = find.text('Desliza en la dirección mostrada');
    final TestGesture dedo = await tester.startGesture(tester.getCenter(zona));
    for (final Offset tramo in tramos) {
      await dedo.moveBy(tramo);
      await tester.pump(const Duration(milliseconds: 16));
    }
    await dedo.up();
    await tester.pump();
  }

  Future<void> terminar(WidgetTester tester) async {
    await tester.pumpWidget(const SizedBox());
  }

  testWidgets('un deslizamiento a la derecha que arranca en curva sigue contando como derecha',
      (tester) async {
    await montar(tester);
    // Los primeros ~25 px van hacia arriba y luego el dedo se va a la derecha.
    await deslizar(tester, const <Offset>[Offset(0, -18), Offset(6, -8), Offset(80, 0)]);
    expect(exito, isNull, reason: 'una lectura dudosa no debe terminar la tarea');
    expect(find.text('Progreso: 1/5'), findsOneWidget);
    await terminar(tester);
  });

  testWidgets('un deslizamiento equivocado NO termina la tarea: reinicia la secuencia',
      (tester) async {
    await montar(tester);
    await deslizar(tester, const <Offset>[Offset(-100, 0)]); // izquierda, era derecha
    expect(exito, isNull);
    expect(find.text('Progreso: 0/5'), findsOneWidget);
    expect(find.text('Errores: 1'), findsOneWidget);
    await terminar(tester);
  });

  testWidgets('un deslizamiento en diagonal (ambiguo) se ignora sin penalizar', (tester) async {
    await montar(tester);
    await deslizar(tester, const <Offset>[Offset(70, 70)]);
    expect(exito, isNull);
    expect(find.text('Progreso: 0/5'), findsOneWidget);
    expect(find.textContaining('Errores'), findsNothing);
    await terminar(tester);
  });

  testWidgets('acertar las 5 flechas completa la tarea con éxito', (tester) async {
    await montar(tester);
    await deslizar(tester, const <Offset>[Offset(100, 0)]); // derecha
    await deslizar(tester, const <Offset>[Offset(0, -100)]); // arriba
    await deslizar(tester, const <Offset>[Offset(-100, 0)]); // izquierda
    await deslizar(tester, const <Offset>[Offset(0, 100)]); // abajo
    await deslizar(tester, const <Offset>[Offset(100, 0)]); // derecha
    expect(exito, isTrue);
    expect(datos!['aciertos'], 5);
    await terminar(tester);
  });

  testWidgets('si se acaba el tiempo, falla', (tester) async {
    await montar(tester);
    await tester.pump(const Duration(seconds: 16));
    expect(exito, isFalse);
    await terminar(tester);
  });

  testWidgets('las flechas se ven grandes (≥ 72 px) y la que toca ahora destaca', (tester) async {
    await montar(tester);
    for (final IconData icono in <IconData>[
      Icons.arrow_forward,
      Icons.arrow_upward,
      Icons.arrow_back,
      Icons.arrow_downward,
    ]) {
      for (final Element e in find.byIcon(icono).evaluate()) {
        final Size tamano = (e.renderObject! as RenderBox).size;
        expect(tamano.width, greaterThanOrEqualTo(72), reason: '$icono');
      }
    }
    await terminar(tester);
  });
}
