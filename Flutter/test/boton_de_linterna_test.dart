import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/widgets/boton_de_linterna.dart';
import 'package:provider/provider.dart';

/// La linterna del jugador 1 (paso 8): se mantiene apretado su botón en la
/// tablet y alumbra hacia donde mira la cámara del juego. Gasta la batería de
/// las compuertas; la tablet repite la orden mientras el dedo sigue apoyado y
/// Unity la apaga sola si dejan de llegar órdenes.
class JuegoEspia extends GameProvider {
  final List<bool> linterna = <bool>[];

  @override
  void prenderLinterna(bool encendida) => linterna.add(encendida);
}

Map<String, dynamic> estadoOficina({int bateria = 76, bool? linterna}) => <String, dynamic>{
      'type': 'estado_oficina',
      'bateria': bateria,
      'compuerta_izquierda': false,
      'compuerta_derecha': false,
      'luz_izquierda': 'verde',
      'luz_derecha': 'verde',
      'linterna': ?linterna,
    };

void main() {
  group('GameProvider y la linterna', () {
    test('estado_oficina dice si la linterna está prendida', () {
      final GameProvider juego = GameProvider();
      expect(juego.session.linternaEncendida, isFalse);
      juego.handleMessage(estadoOficina(linterna: true));
      expect(juego.session.linternaEncendida, isTrue);
      juego.handleMessage(estadoOficina(linterna: false));
      expect(juego.session.linternaEncendida, isFalse);
    });

    test('un Unity sin linterna (sin el campo) la deja apagada', () {
      final GameProvider juego = GameProvider();
      juego.handleMessage(estadoOficina());
      expect(juego.session.linternaEncendida, isFalse);
      expect(juego.session.bateria, 76);
    });

    test('prenderLinterna manda el mensaje linterna a Unity', () {
      final GameProvider juego = GameProvider();
      final List<Map<String, dynamic>> enviados = <Map<String, dynamic>>[];
      juego.sendToServer = enviados.add;
      juego.prenderLinterna(true);
      juego.prenderLinterna(false);
      expect(enviados, <Map<String, dynamic>>[
        <String, dynamic>{'type': 'linterna', 'encendida': true},
        <String, dynamic>{'type': 'linterna', 'encendida': false},
      ]);
    });
  });

  group('BotonDeLinterna', () {
    late List<bool> pedidos;

    Future<void> montar(WidgetTester tester, {int bateria = 76, bool encendida = false}) async {
      pedidos = <bool>[];
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: Center(
            child: BotonDeLinterna(bateria: bateria, encendida: encendida, alCambiar: pedidos.add),
          ),
        ),
      ));
    }

    testWidgets('mantener apretado la prende y repite la orden; soltar la apaga', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
      expect(pedidos, <bool>[true], reason: 'responde al apoyar el dedo');
      await tester.pump(const Duration(milliseconds: 160));
      await tester.pump(const Duration(milliseconds: 160));
      expect(pedidos.length, greaterThanOrEqualTo(3), reason: 'repite cada 0,15 s mientras sigue apoyado');
      expect(pedidos.every((bool p) => p), isTrue);
      await dedo.up();
      expect(pedidos.last, isFalse);
      final int alSoltar = pedidos.length;
      await tester.pump(const Duration(milliseconds: 400));
      expect(pedidos.length, alSoltar, reason: 'suelto ya no repite');
    });

    testWidgets('muestra la batería y que es la linterna', (tester) async {
      await montar(tester, bateria: 76);
      expect(find.text('76 %'), findsOneWidget);
      expect(find.text('Linterna'), findsOneWidget);
      expect(find.bySemanticsLabel(RegExp('Linterna.*mantén apretado.*76 %')), findsOneWidget);
    });

    testWidgets('sin batería no se prende y lo dice', (tester) async {
      await montar(tester, bateria: 0);
      expect(find.text('SIN BATERÍA'), findsOneWidget);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
      await tester.pump(const Duration(milliseconds: 400));
      await dedo.up();
      expect(pedidos, isEmpty);
    });

    testWidgets('si se acaba la batería con el dedo apoyado deja de pedirla', (tester) async {
      await montar(tester, bateria: 2);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(body: Center(child: BotonDeLinterna(bateria: 0, encendida: false, alCambiar: pedidos.add))),
      ));
      expect(pedidos.last, isFalse);
      final int alAcabarse = pedidos.length;
      await tester.pump(const Duration(milliseconds: 400));
      expect(pedidos.length, alAcabarse);
      await dedo.up();
    });

    testWidgets('si el botón desaparece con el dedo apoyado, la apaga', (tester) async {
      await montar(tester);
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
      await tester.pumpWidget(const SizedBox());
      expect(pedidos.last, isFalse);
      await dedo.up();
    });

    testWidgets('es grande, como los demás botones de la fila (se acierta sin mirar)', (tester) async {
      await montar(tester);
      final Size tamano = tester.getSize(find.byKey(const Key('linterna')));
      expect(tamano.height, greaterThanOrEqualTo(88));
      expect(tamano.width, greaterThanOrEqualTo(160));
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

    testWidgets('la linterna va entre los dos botones de cámara, con la batería', (tester) async {
      final JuegoEspia juego = await montar(tester);
      juego.handleMessage(estadoOficina(bateria: 64));
      await tester.pump();
      final Rect linterna = tester.getRect(find.byKey(const Key('linterna')));
      final Rect camaraIzq = tester.getRect(find.byKey(const Key('camara-izquierda')));
      final Rect camaraDer = tester.getRect(find.byKey(const Key('camara-derecha')));
      expect(camaraIzq.right, lessThanOrEqualTo(linterna.left));
      expect(linterna.right, lessThanOrEqualTo(camaraDer.left));
      expect(find.text('64 %'), findsOneWidget);
      expect(tester.takeException(), isNull, reason: 'sin desbordes');
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('mantenerla apretada se lo pide a Unity por GameProvider', (tester) async {
      final JuegoEspia juego = await montar(tester);
      juego.handleMessage(estadoOficina(bateria: 64));
      await tester.pump();
      final TestGesture dedo = await tester.startGesture(tester.getCenter(find.byKey(const Key('linterna'))));
      await tester.pump(const Duration(milliseconds: 160));
      await dedo.up();
      expect(juego.linterna.first, isTrue);
      expect(juego.linterna.last, isFalse);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('sin estado de Unity no hay linterna (un Unity viejo no la tiene)', (tester) async {
      await montar(tester);
      expect(find.byKey(const Key('linterna')), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
