import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/widgets/menu_de_tareas.dart';

/// Una tarea que se pone roja no puede avisar solo por el color: para quien no
/// distingue rojo de ámbar o de gris tiene que cambiar también la forma, un
/// texto y el grosor del borde.
void main() {
  Task tarea(int segundosPendiente) => Task(
        taskId: 1,
        taskType: 'cables',
        duration: 30,
        description: 'x',
        difficulty: 1,
        createdAt: DateTime.now().subtract(Duration(seconds: segundosPendiente)),
        params: const {},
      );

  Future<void> montar(WidgetTester tester, int segundosPendiente) async {
    tester.view.physicalSize = const Size(1600, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: MenuDeTareas(
          tareas: <Task>[tarea(segundosPendiente)],
          alElegirTarea: (_) {},
          puppetEnPeligro: false,
          alAbrirCajaDePuppet: () {},
        ),
      ),
    ));
    await tester.pump();
  }

  Future<void> desmontar(WidgetTester tester) => tester.pumpWidget(const SizedBox());

  testWidgets('tarea normal (5 s): sin icono ni texto de urgencia', (tester) async {
    await montar(tester, 5);
    expect(find.byIcon(Icons.priority_high), findsNothing);
    expect(find.byIcon(Icons.schedule), findsNothing);
    expect(find.text('¡URGENTE!'), findsNothing);
    await desmontar(tester);
  });

  testWidgets('tarea en ámbar (25 s): icono de reloj, sin texto de urgente', (tester) async {
    await montar(tester, 25);
    expect(find.byIcon(Icons.schedule), findsOneWidget);
    expect(find.text('¡URGENTE!'), findsNothing);
    await desmontar(tester);
  });

  testWidgets('tarea en rojo (50 s): texto "¡URGENTE!", icono grande de exclamación y borde grueso', (tester) async {
    await montar(tester, 50);
    expect(find.text('¡URGENTE!'), findsOneWidget);
    final Finder icono = find.byIcon(Icons.priority_high);
    expect(icono, findsOneWidget);
    expect(tester.getSize(icono).width, greaterThanOrEqualTo(28), reason: 'el icono se ve');

    final Container tarjeta = tester.widget<Container>(
      find.ancestor(of: find.text('Conectar Cables'), matching: find.byType(Container)).last,
    );
    final BoxDecoration decoracion = tarjeta.decoration! as BoxDecoration;
    expect((decoracion.border! as Border).top.width, greaterThanOrEqualTo(4), reason: 'borde más grueso');
    await desmontar(tester);
  });
}
