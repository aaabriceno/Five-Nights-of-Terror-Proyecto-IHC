import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/widgets/botones_de_camara.dart';
import 'package:provider/provider.dart';

/// El jugador 1 mueve la cámara del juego desde la tablet: dos botones grandes
/// que se mantienen apretados (izquierda y derecha). Mientras el dedo sigue
/// apoyado la tablet repite la orden; al soltar manda 0 y Unity vuelve al frente.
class JuegoEspia extends GameProvider {
  final List<int> miradas = <int>[];

  @override
  void mirarHacia(int direccion) => miradas.add(direccion);
}

void main() {
  group('BotonesDeCamara', () {
    late List<int> ordenes;

    Future<void> montar(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1600, 960);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      ordenes = <int>[];
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(body: Align(alignment: Alignment.bottomCenter, child: BotonesDeCamara(alMirar: ordenes.add))),
      ));
    }

    testWidgets('apoyar el dedo en la izquierda manda -1 al instante y la repite mientras siga apoyado', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-izquierda'))));
      expect(ordenes, <int>[-1]);
      await tester.pump(const Duration(milliseconds: 460));
      expect(ordenes.where((int o) => o == -1).length, greaterThanOrEqualTo(4), reason: 'cada ~150 ms');
      expect(ordenes.every((int o) => o == -1), isTrue);
      await dedo.up();
    });

    testWidgets('soltar manda 0 y deja de repetir', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-derecha'))));
      await tester.pump(const Duration(milliseconds: 200));
      await dedo.up();
      expect(ordenes.last, 0);
      final int cuantas = ordenes.length;
      await tester.pump(const Duration(seconds: 1));
      expect(ordenes.length, cuantas, reason: 'ya no manda nada');
    });

    testWidgets('el botón derecho manda +1', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-derecha'))));
      expect(ordenes.first, 1);
      await dedo.up();
    });

    testWidgets('si el sistema cancela el toque también se suelta', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-izquierda'))));
      await dedo.cancel();
      expect(ordenes.last, 0);
    });

    testWidgets('si la pantalla se cierra con el dedo apoyado, manda 0 (no deja la cámara girando)', (tester) async {
      await montar(tester);
      await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-izquierda'))));
      await tester.pumpWidget(const SizedBox());
      expect(ordenes.last, 0);
    });

    testWidgets('los botones son grandes (se aciertan sin mirar) y dicen qué hacen', (tester) async {
      await montar(tester);
      for (final String clave in <String>['camara-izquierda', 'camara-derecha']) {
        final Size tamano = tester.getSize(find.byKey(Key(clave)));
        expect(tamano.height, greaterThanOrEqualTo(88), reason: clave);
        expect(tamano.width, greaterThanOrEqualTo(240), reason: clave);
      }
      expect(find.bySemanticsLabel('Mirar a la izquierda'), findsOneWidget);
      expect(find.bySemanticsLabel('Mirar a la derecha'), findsOneWidget);
    });
  });

  testWidgets('GameProvider.mirarHacia manda el mensaje camara a Unity', (tester) async {
    final GameProvider juego = GameProvider();
    final List<Map<String, dynamic>> enviados = <Map<String, dynamic>>[];
    juego.sendToServer = enviados.add;
    juego.mirarHacia(-1);
    juego.mirarHacia(0);
    expect(enviados, <Map<String, dynamic>>[
      <String, dynamic>{'type': 'camara', 'direccion': -1},
      <String, dynamic>{'type': 'camara', 'direccion': 0},
    ]);
  });

  group('GameScreen', () {
    const double altoDeLaBarraDelSistema = 48;

    Future<JuegoEspia> montar(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1600, 960);
      tester.view.devicePixelRatio = 1;
      tester.view.padding = const FakeViewPadding(bottom: altoDeLaBarraDelSistema);
      tester.view.viewPadding = const FakeViewPadding(bottom: altoDeLaBarraDelSistema);
      addTearDown(tester.view.reset);
      final JuegoEspia juego = JuegoEspia();
      await tester.pumpWidget(MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => ConnectionProvider()),
          ChangeNotifierProvider<GameProvider>.value(value: juego),
        ],
        child: const MaterialApp(home: GameScreen()),
      ));
      await tester.pump();
      return juego;
    }

    testWidgets('los botones de cámara están siempre en la pantalla de juego, sobre la barra del sistema', (tester) async {
      await montar(tester);
      final Rect izquierda = tester.getRect(find.byKey(const Key('camara-izquierda')));
      final Rect derecha = tester.getRect(find.byKey(const Key('camara-derecha')));
      expect(izquierda.bottom, lessThanOrEqualTo(960 - altoDeLaBarraDelSistema));
      expect(derecha.bottom, lessThanOrEqualTo(960 - altoDeLaBarraDelSistema));
      expect(izquierda.right, lessThanOrEqualTo(derecha.left), reason: 'izquierda a la izquierda, derecha a la derecha');
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('mantener el botón de la izquierda manda la orden a Unity por GameProvider', (tester) async {
      final JuegoEspia juego = await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('camara-izquierda'))));
      await tester.pump(const Duration(milliseconds: 300));
      await dedo.up();
      expect(juego.miradas.first, -1);
      expect(juego.miradas.last, 0);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
