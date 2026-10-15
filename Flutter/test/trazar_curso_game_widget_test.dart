import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/trazar_curso_game_widget.dart';

void main() {
  group('colocarPuntosSinSolaparse', () {
    test(
      'siempre devuelve todos los puntos, dentro del área y sin encimarse',
      () {
        const List<Size> areas = <Size>[
          Size(1568, 520),
          Size(1000, 400),
          Size(700, 330),
        ];
        for (final Size area in areas) {
          for (int semilla = 0; semilla < 200; semilla++) {
            const double radio = 40;
            final List<Offset> p = colocarPuntosSinSolaparse(
              6,
              area,
              radio,
              semilla,
            );
            expect(p.length, 6, reason: '$area semilla $semilla');
            for (int i = 0; i < p.length; i++) {
              expect(p[i].dx, inInclusiveRange(radio, area.width - radio));
              expect(p[i].dy, inInclusiveRange(radio, area.height - radio));
              for (int j = i + 1; j < p.length; j++) {
                expect(
                  (p[i] - p[j]).distance,
                  greaterThanOrEqualTo(2 * radio + 8),
                  reason: 'puntos $i y $j encimados ($area, semilla $semilla)',
                );
              }
            }
          }
        }
      },
    );

    test('la misma semilla da la misma colocación (no salta al redibujar)', () {
      final List<Offset> a = colocarPuntosSinSolaparse(
        6,
        const Size(1000, 400),
        40,
        7,
      );
      final List<Offset> b = colocarPuntosSinSolaparse(
        6,
        const Size(1000, 400),
        40,
        7,
      );
      expect(a, b);
    });
  });

  group('widget', () {
    Task tarea() => Task(
      taskId: 1,
      taskType: 'trazar_curso',
      duration: 20,
      description: 'Trazar curso',
      difficulty: 1,
      createdAt: DateTime(2026),
      params: const {},
    );

    bool? exito;

    Future<void> montar(WidgetTester tester, Size area) async {
      exito = null;
      tester.view.physicalSize = area;
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: TrazarCursoGameWidget(
              task: tarea(),
              onComplete: (ok, _) => exito = ok,
            ),
          ),
        ),
      );
    }

    Finder circulo(int numero) => find.ancestor(
      of: find.text('$numero'),
      matching: find.byType(GestureDetector),
    );

    testWidgets(
      'los círculos son grandes (≥ 80 px) en una tablet y (≥ 70) en una chica',
      (tester) async {
        for (final Size area in const <Size>[
          Size(1568, 700),
          Size(1000, 520),
        ]) {
          await montar(tester, area);
          for (int n = 1; n <= 6; n++) {
            final Size t = tester.getSize(circulo(n));
            expect(
              t.width,
              greaterThanOrEqualTo(area.width > 1200 ? 80 : 70),
              reason: 'círculo $n en $area',
            );
          }
          await tester.pumpWidget(const SizedBox());
        }
      },
    );

    testWidgets(
      'los números dentro de los círculos se leen grandes (≥ 32 px)',
      (tester) async {
        await montar(tester, const Size(1568, 700));
        for (int n = 1; n <= 6; n++) {
          final TextStyle estilo = tester.widget<Text>(find.text('$n')).style!;
          expect(
            estilo.fontSize,
            greaterThanOrEqualTo(32),
            reason: 'número $n',
          );
        }
        await tester.pumpWidget(const SizedBox());
      },
    );

    testWidgets('los 6 círculos no se encimen y quedan dentro de la pantalla', (
      tester,
    ) async {
      for (int i = 0; i < 25; i++) {
        await montar(tester, const Size(1568, 700));
        final List<Rect> r = <Rect>[
          for (int n = 1; n <= 6; n++) tester.getRect(circulo(n)),
        ];
        for (int a = 0; a < r.length; a++) {
          expect(r[a].left, greaterThanOrEqualTo(0));
          expect(r[a].right, lessThanOrEqualTo(1568));
          expect(r[a].bottom, lessThanOrEqualTo(700));
          for (int b = a + 1; b < r.length; b++) {
            expect(
              (r[a].center - r[b].center).distance,
              greaterThanOrEqualTo(r[a].width),
              reason: 'círculos ${a + 1} y ${b + 1} se encimen',
            );
          }
        }
        await tester.pumpWidget(const SizedBox());
      }
    });

    testWidgets(
      'tocarlos en orden 1 a 6 completa la tarea; en desorden no avanza',
      (tester) async {
        await montar(tester, const Size(1568, 700));
        await tester.tap(circulo(3));
        await tester.pump();
        expect(find.text('Conectados: 0/6'), findsOneWidget);
        for (int n = 1; n <= 6; n++) {
          await tester.tap(circulo(n));
          await tester.pump();
        }
        expect(exito, isTrue);
        await tester.pumpWidget(const SizedBox());
      },
    );
  });
}
