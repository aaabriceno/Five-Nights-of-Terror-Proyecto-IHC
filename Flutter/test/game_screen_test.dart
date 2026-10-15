import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:provider/provider.dart';

/// La tablet (Lenovo TB350XU) dibuja la app bajo la barra de gestos del
/// sistema: abajo hay unos 48 px que el contenido no puede usar, y la barra
/// superior debía poder tocarse con el dedo sin apuntar.
void main() {
  const double altoDeLaBarraDelSistema = 48;

  Task cables() => Task(
        taskId: 1,
        taskType: 'cables',
        duration: 90,
        description: 'Conecta los cables',
        difficulty: 1,
        createdAt: DateTime(2026),
        params: const {},
      );

  Future<GameProvider> montar(WidgetTester tester, {required bool conTareaAbierta}) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    tester.view.padding = const FakeViewPadding(bottom: altoDeLaBarraDelSistema);
    tester.view.viewPadding = const FakeViewPadding(bottom: altoDeLaBarraDelSistema);
    addTearDown(tester.view.reset);

    final GameProvider juego = GameProvider();
    if (conTareaAbierta) juego.session.currentTask = cables();
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ConnectionProvider()),
        ChangeNotifierProvider.value(value: juego),
      ],
      child: const MaterialApp(home: GameScreen()),
    ));
    await tester.pump();
    return juego;
  }

  testWidgets('el botón Enviar y "Conectados" quedan sobre la barra del sistema', (tester) async {
    await montar(tester, conTareaAbierta: true);
    final double limite = 960 - altoDeLaBarraDelSistema;
    final Rect enviar = tester.getRect(find.widgetWithText(ElevatedButton, 'Enviar'));
    final Rect conectados = tester.getRect(find.textContaining('Conectados'));
    expect(enviar.bottom, lessThanOrEqualTo(limite), reason: 'Enviar se ve completo');
    expect(conectados.bottom, lessThanOrEqualTo(limite));
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('la flecha de volver es grande (≥ 72 px de zona táctil, icono ≥ 48)', (tester) async {
    await montar(tester, conTareaAbierta: true);
    final Finder flecha = find.widgetWithIcon(IconButton, Icons.arrow_back);
    expect(flecha, findsOneWidget);
    final Size zona = tester.getSize(flecha);
    expect(zona.width, greaterThanOrEqualTo(72));
    expect(zona.height, greaterThanOrEqualTo(72));
    expect(tester.getSize(find.byIcon(Icons.arrow_back)).width, greaterThanOrEqualTo(48));
    await tester.pumpWidget(const SizedBox());
  });

  testWidgets('la X de salir también es grande', (tester) async {
    await montar(tester, conTareaAbierta: false);
    final Finder equis = find.widgetWithIcon(IconButton, Icons.close);
    final Size zona = tester.getSize(equis);
    expect(zona.width, greaterThanOrEqualTo(72));
    expect(zona.height, greaterThanOrEqualTo(72));
    expect(tester.getSize(find.byIcon(Icons.close)).width, greaterThanOrEqualTo(48));
  });

  testWidgets('la barra superior es alta y el estado de conexión se lee', (tester) async {
    await montar(tester, conTareaAbierta: false);
    expect(tester.getSize(find.byType(AppBar)).height, greaterThanOrEqualTo(80));
    final TextStyle estilo = tester.widget<Text>(find.text('Desconectado')).style!;
    expect(estilo.fontSize, greaterThanOrEqualTo(24));
  });

  testWidgets('con tarea abierta, tocar la flecha vuelve al menú', (tester) async {
    final GameProvider juego = await montar(tester, conTareaAbierta: true);
    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pump();
    expect(juego.session.currentTask, isNull);
    await tester.pumpWidget(const SizedBox());
  });
}
