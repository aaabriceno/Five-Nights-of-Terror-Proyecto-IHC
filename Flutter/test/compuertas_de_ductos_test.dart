import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/widgets/compuertas_de_ductos.dart';
import 'package:provider/provider.dart';

/// Las compuertas de los ductos: el jugador 1 las cierra desde la tablet y
/// gastan batería. Unity manda el estado (`estado_oficina`) y la tablet solo
/// lo muestra y pide cambios (`compuerta`).
class JuegoEspia extends GameProvider {
  final List<String> pedidos = <String>[];

  @override
  void cambiarCompuerta(String lado, bool cerrar) => pedidos.add('$lado:$cerrar');
}

Map<String, dynamic> estadoOficina({int bateria = 76, bool izquierda = false, bool derecha = false}) =>
    <String, dynamic>{
      'type': 'estado_oficina',
      'bateria': bateria,
      'compuerta_izquierda': izquierda,
      'compuerta_derecha': derecha,
      'luz_izquierda': 'verde',
      'luz_derecha': 'rojo',
    };

void main() {
  group('GameProvider y estado_oficina', () {
    test('guarda la batería y qué compuertas están cerradas', () {
      final GameProvider juego = GameProvider();
      expect(juego.session.bateria, isNull, reason: 'sin estado de Unity no hay compuertas que mostrar');
      juego.handleMessage(estadoOficina(bateria: 76, izquierda: true));
      expect(juego.session.bateria, 76);
      expect(juego.session.compuertaIzquierdaCerrada, isTrue);
      expect(juego.session.compuertaDerechaCerrada, isFalse);
    });

    test('la batería queda entre 0 y 100', () {
      final GameProvider juego = GameProvider();
      juego.handleMessage(estadoOficina(bateria: 150));
      expect(juego.session.bateria, 100);
      juego.handleMessage(estadoOficina(bateria: -3));
      expect(juego.session.bateria, 0);
    });

    test('un estado_oficina roto se ignora y no deja la sesión a medias', () {
      final GameProvider juego = GameProvider();
      juego.handleMessage(<String, dynamic>{
        'type': 'estado_oficina', 'bateria': 'mucha', 'compuerta_izquierda': true, 'compuerta_derecha': false,
      });
      expect(juego.session.bateria, isNull);
      expect(juego.session.compuertaIzquierdaCerrada, isFalse);
    });

    test('cambiarCompuerta manda el mensaje compuerta a Unity', () {
      final GameProvider juego = GameProvider();
      final List<Map<String, dynamic>> enviados = <Map<String, dynamic>>[];
      juego.sendToServer = enviados.add;
      juego.cambiarCompuerta('izquierda', true);
      juego.cambiarCompuerta('derecha', false);
      expect(enviados, <Map<String, dynamic>>[
        <String, dynamic>{'type': 'compuerta', 'lado': 'izquierda', 'cerrar': true},
        <String, dynamic>{'type': 'compuerta', 'lado': 'derecha', 'cerrar': false},
      ]);
    });
  });

  group('BotonDeCompuerta', () {
    late List<bool> pedidos;

    Future<void> montar(WidgetTester tester, {required bool cerrada, required int bateria}) async {
      pedidos = <bool>[];
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: Center(
            child: BotonDeCompuerta(lado: 'izquierda', cerrada: cerrada, bateria: bateria, alCambiar: pedidos.add),
          ),
        ),
      ));
    }

    testWidgets('abierta: tocarla pide cerrarla, y lo dice', (tester) async {
      await montar(tester, cerrada: false, bateria: 80);
      expect(find.text('ABIERTA'), findsOneWidget);
      expect(find.bySemanticsLabel('Cerrar la compuerta izquierda'), findsOneWidget);
      await tester.tap(find.byKey(const Key('compuerta-izquierda')));
      expect(pedidos, <bool>[true]);
    });

    testWidgets('cerrada: tocarla pide abrirla', (tester) async {
      await montar(tester, cerrada: true, bateria: 80);
      expect(find.text('CERRADA'), findsOneWidget);
      expect(find.bySemanticsLabel('Abrir la compuerta izquierda'), findsOneWidget);
      await tester.tap(find.byKey(const Key('compuerta-izquierda')));
      expect(pedidos, <bool>[false]);
    });

    testWidgets('sin batería no se puede cerrar y lo dice', (tester) async {
      await montar(tester, cerrada: false, bateria: 0);
      expect(find.text('SIN BATERÍA'), findsOneWidget);
      await tester.tap(find.byKey(const Key('compuerta-izquierda')));
      expect(pedidos, isEmpty);
    });

    testWidgets('es grande, como los botones de cámara (se acierta sin mirar)', (tester) async {
      await montar(tester, cerrada: false, bateria: 80);
      final Size tamano = tester.getSize(find.byKey(const Key('compuerta-izquierda')));
      expect(tamano.height, greaterThanOrEqualTo(88));
      expect(tamano.width, greaterThanOrEqualTo(160));
    });
  });

  group('IndicadorDeBateria', () {
    Future<void> montar(WidgetTester tester, int bateria) => tester.pumpWidget(MaterialApp(
          home: Scaffold(body: Center(child: IndicadorDeBateria(bateria: bateria))),
        ));

    testWidgets('muestra el porcentaje', (tester) async {
      await montar(tester, 76);
      expect(find.text('76 %'), findsOneWidget);
      expect(find.bySemanticsLabel('Batería de las compuertas: 76 %'), findsOneWidget);
    });

    testWidgets('en 0 dice que no hay batería', (tester) async {
      await montar(tester, 0);
      expect(find.text('0 %'), findsOneWidget);
      expect(find.text('SIN BATERÍA'), findsOneWidget);
    });
  });

  group('GameScreen', () {
    Future<JuegoEspia> montar(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1600, 960);
      tester.view.devicePixelRatio = 1;
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

    testWidgets('sin estado de Unity no aparecen las compuertas (un Unity viejo no las tiene)', (tester) async {
      await montar(tester);
      expect(find.byKey(const Key('compuerta-izquierda')), findsNothing);
      expect(find.byKey(const Key('camara-izquierda')), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('con estado: compuertas en los extremos de la fila de la cámara y la batería en medio', (tester) async {
      final JuegoEspia juego = await montar(tester);
      juego.handleMessage(estadoOficina(bateria: 76));
      await tester.pump();
      final Rect compuertaIzq = tester.getRect(find.byKey(const Key('compuerta-izquierda')));
      final Rect compuertaDer = tester.getRect(find.byKey(const Key('compuerta-derecha')));
      final Rect camaraIzq = tester.getRect(find.byKey(const Key('camara-izquierda')));
      final Rect camaraDer = tester.getRect(find.byKey(const Key('camara-derecha')));
      final Rect bateria = tester.getRect(find.byKey(const Key('bateria-compuertas')));
      expect(compuertaIzq.right, lessThanOrEqualTo(camaraIzq.left), reason: 'la compuerta izquierda, a la izquierda de todo');
      expect(camaraIzq.right, lessThanOrEqualTo(bateria.left));
      expect(bateria.right, lessThanOrEqualTo(camaraDer.left));
      expect(camaraDer.right, lessThanOrEqualTo(compuertaDer.left), reason: 'la compuerta derecha, a la derecha de todo');
      expect((compuertaIzq.center.dy - camaraIzq.center.dy).abs(), lessThan(1), reason: 'misma fila: no le quita alto a las tareas');
      expect(camaraIzq.width, greaterThanOrEqualTo(240), reason: 'los botones de cámara siguen siendo grandes');
      expect(find.text('76 %'), findsOneWidget);
      expect(tester.takeException(), isNull, reason: 'sin desbordes');
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('tocar una compuerta se lo pide a Unity por GameProvider', (tester) async {
      final JuegoEspia juego = await montar(tester);
      juego.handleMessage(estadoOficina(bateria: 50, derecha: true));
      await tester.pump();
      await tester.tap(find.byKey(const Key('compuerta-izquierda')));
      await tester.tap(find.byKey(const Key('compuerta-derecha')));
      expect(juego.pedidos, <String>['izquierda:true', 'derecha:false']);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
