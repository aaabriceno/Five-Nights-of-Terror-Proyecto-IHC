import 'package:flutter/material.dart' hide ConnectionState;
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/tutorial_screen.dart';
import 'package:fn_attention_defense_tablet/widgets/boton_de_linterna.dart';
import 'package:fn_attention_defense_tablet/widgets/botones_de_camara.dart';
import 'package:fn_attention_defense_tablet/widgets/cable_game_widget.dart';
import 'package:fn_attention_defense_tablet/widgets/compuertas_de_ductos.dart';
import 'package:fn_attention_defense_tablet/widgets/puppet_game_widget.dart';
import 'package:provider/provider.dart';

import 'ayudas/servicio_falso.dart';

/// El tutorial termina con dos prácticas: primero los controles de la partida
/// (con los mismos botones, en el mismo lugar), después una tarea y la mirada.
/// Unity marca cada paso y la tablet muestra la misma lista (2026-11-17).
void main() {
  late WebSocketFalso ws;
  late ConnectionProvider conexion;

  setUp(() {
    ServerConfig.useMock = false;
    ws = WebSocketFalso();
    conexion = ConnectionProvider(wsService: ws);
  });

  tearDown(() => ServerConfig.useMock = true);

  /// Lo que manda el «Unity» falso llega en una microtarea, después del cuadro
  /// que dibuja `pump`: hace falta otro para verlo en pantalla.
  Future<void> asentar(WidgetTester tester) async {
    await tester.pump();
    await tester.pump();
  }

  Future<void> irA(WidgetTester tester, int indice,
      {Size pantalla = const Size(1600, 960), double escalaDeTexto = 1}) async {
    tester.view.physicalSize = pantalla;
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: conexion),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: MaterialApp(
        builder: (context, hijo) => MediaQuery(
          data: MediaQuery.of(context).copyWith(textScaler: TextScaler.linear(escalaDeTexto)),
          child: hijo!,
        ),
        home: const TutorialScreen(),
      ),
    ));
    await tester.pump();
    ws.llega(<String, dynamic>{'type': 'tutorial_estado', 'indice': indice, 'total': 7});
    await asentar(tester);
  }

  void confirma(Map<String, dynamic> pasos) => ws.llega(<String, dynamic>{
        'type': 'tutorial_prueba_estado',
        'mirando': false,
        'mirada_completada': false,
        'tarea_completada': false,
        'camara_disponible': true,
        'camara': false,
        'linterna': false,
        'compuerta': false,
        'cuerda': false,
        ...pasos,
      });

  bool mando(Map<String, dynamic> orden) =>
      ws.enviados.any((m) => orden.entries.every((e) => m[e.key] == e.value));

  testWidgets('la práctica de controles trae los botones de la partida y la caja de Puppet', (tester) async {
    await irA(tester, 5);
    expect(find.byType(BotonesDeCamara), findsOneWidget);
    expect(find.byType(BotonDeLinterna), findsOneWidget);
    expect(find.byType(BotonDeCompuerta), findsNWidgets(2));
    expect(find.byType(PuppetGameWidget), findsOneWidget);
    expect(find.byType(CableGameWidget), findsNothing, reason: 'los cables son de la otra práctica');
  });

  testWidgets('los botones mandan las mismas órdenes que en una partida', (tester) async {
    await irA(tester, 5);
    final TestGesture camara = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-izquierda'))));
    await tester.pump(const Duration(milliseconds: 200));
    await camara.up();
    final TestGesture linterna = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
    await tester.pump(const Duration(milliseconds: 200));
    await linterna.up();
    await tester.tap(find.byKey(const Key('compuerta-izquierda')));
    await tester.pump();
    final TestGesture cuerda = await tester.startGesture(tester.getCenter(find.byIcon(Icons.music_note)));
    await tester.pump(const Duration(milliseconds: 300));
    await cuerda.up();
    await tester.pump();

    expect(mando({'type': 'camara', 'direccion': -1}), isTrue);
    expect(mando({'type': 'linterna', 'encendida': true}), isTrue);
    expect(mando({'type': 'compuerta', 'lado': 'izquierda', 'cerrar': true}), isTrue);
    expect(mando({'type': 'dar_cuerda_inicio'}), isTrue);
    expect(mando({'type': 'dar_cuerda_fin'}), isTrue);
  });

  testWidgets('la lista marca lo que Unity confirma', (tester) async {
    await irA(tester, 5);
    expect(find.text('✓ Girar la cámara'), findsNothing);
    expect(find.text('○ Girar la cámara'), findsOneWidget);
    confirma({'camara': true, 'compuerta': true});
    await asentar(tester);
    expect(find.text('✓ Girar la cámara'), findsOneWidget);
    expect(find.text('✓ Cerrar una compuerta'), findsOneWidget);
    expect(find.text('○ Prender la linterna'), findsOneWidget);
    expect(find.text('○ Dar cuerda a la caja'), findsOneWidget);
  });

  testWidgets('si Unity reinicia la práctica, la lista de la tablet también vuelve a cero', (tester) async {
    await irA(tester, 5);
    confirma({'camara': true, 'linterna': true});
    await asentar(tester);
    expect(find.text('✓ Prender la linterna'), findsOneWidget);
    confirma({}); // tutorial_abrir otra vez (por ejemplo, al reintentar la conexión)
    await asentar(tester);
    expect(find.text('○ Prender la linterna'), findsOneWidget);
    expect(find.text('✓ Girar la cámara'), findsNothing);
  });

  testWidgets('Terminar se habilita solo con las dos prácticas completas', (tester) async {
    await irA(tester, 6);
    expect(find.byType(CableGameWidget), findsOneWidget);
    ElevatedButton terminar() =>
        tester.widget<ElevatedButton>(find.ancestor(of: find.text('Terminar'), matching: find.byWidgetPredicate((w) => w is ElevatedButton)));

    confirma({'mirada_completada': true, 'tarea_completada': true});
    await asentar(tester);
    expect(terminar().onPressed, isNull, reason: 'faltan los controles');
    expect(find.textContaining('Práctica 1'), findsOneWidget, reason: 'dice qué falta');

    confirma({'mirada_completada': true, 'tarea_completada': true, 'camara': true, 'linterna': true, 'compuerta': true, 'cuerda': true});
    await asentar(tester);
    expect(terminar().onPressed, isNotNull);
  });

  // La tablet (Lenovo TB350XU) tiene el texto del sistema en 1.15; otras más
  // chicas o con el texto más grande no deben desbordar la práctica.
  for (final int indice in <int>[5, 6]) {
    testWidgets('la práctica ${indice - 4} cabe en una tablet chica con el texto agrandado', (tester) async {
      await irA(tester, indice, pantalla: const Size(1280, 800), escalaDeTexto: 1.3);
      expect(tester.takeException(), isNull);
      expect(find.text('○ Girar la cámara'), indice == 5 ? findsOneWidget : findsNothing);
    });
  }
}
