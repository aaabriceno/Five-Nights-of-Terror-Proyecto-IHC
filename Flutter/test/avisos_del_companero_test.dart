import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/aviso_de_companero.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/services/servicio_de_avisos.dart';
import 'package:fn_attention_defense_tablet/widgets/franja_del_companero.dart';
import 'package:provider/provider.dart';

/// El jugador 2 (compañero) hace cosas que cambian el juego del jugador 1:
/// retrasa animatrónicos, pone trampas, lo salva de Puppet (con una multa en la
/// caja) y entra o se cae. Unity lo manda como `efecto`; la tablet lo muestra.
class VibradorFalso implements Vibrador {
  final List<List<int>> patrones = <List<int>>[];
  int cancelaciones = 0;

  @override
  Future<bool> get tieneVibrador async => true;

  @override
  Future<void> vibrar(List<int> patron) async => patrones.add(patron);

  @override
  Future<void> cancelar() async => cancelaciones++;
}

Map<String, dynamic> efecto(String tipo,
        {String animatronic = '',
        num segundos = 0,
        String message = '',
        num multa = 0,
        num segundosCaja = 0}) =>
    <String, dynamic>{
      'type': 'efecto',
      'animatronic': animatronic,
      'tipo': tipo,
      'segundos': segundos,
      'message': message,
      'id': 0,
      'multa_factor': multa,
      'segundos_caja': segundosCaja,
    };

void main() {
  group('GameProvider recibe lo que hace el compañero', () {
    late GameProvider juego;
    setUp(() => juego = GameProvider());

    test('retrasar: dice quién fue y a quién, y por cuánto tiempo', () {
      juego.handleMessage(efecto('ralentizado', animatronic: 'freddy', segundos: 5));
      final AvisoDeCompanero? aviso = juego.session.avisoDeCompanero;
      expect(aviso, isNotNull);
      expect(aviso!.titulo, 'Tu compañero ralentizó a Freddy');
      expect(aviso.detalle, contains('5 s'));
    });

    test('trampa colocada', () {
      juego.handleMessage(efecto('trampa_colocada'));
      expect(juego.session.avisoDeCompanero!.titulo, 'Tu compañero puso una trampa');
    });

    test('trampa disparada: dice a quién atrapó', () {
      juego.handleMessage(efecto('trampa_disparada', animatronic: 'vixy', segundos: 6));
      expect(juego.session.avisoDeCompanero!.titulo, 'Trampa: Vixy quedó atrapado');
    });

    test('rescate de Puppet: avisa del rescate Y de cuánto dura ahora la caja', () {
      juego.handleMessage(efecto('puppet_rescatado', animatronic: 'puppet', multa: 2, segundosCaja: 15));
      final AvisoDeCompanero aviso = juego.session.avisoDeCompanero!;
      expect(aviso.titulo, 'Tu compañero te salvó de Puppet');
      expect(aviso.detalle, 'Pero ahora la caja dura 15 s');
      expect(juego.session.multaDeCaja, 2);
      expect(juego.session.segundosDeCaja, 15);
    });

    test('cada rescate parte la duración de la caja en dos (15 s, 7,5 s) y ahí se queda', () {
      juego.handleMessage(efecto('puppet_rescatado', multa: 2, segundosCaja: 15));
      juego.handleMessage(efecto('puppet_rescatado', multa: 4, segundosCaja: 7.5));
      expect(juego.session.segundosDeCaja, 7.5);
      expect(juego.session.avisoDeCompanero!.detalle, 'Pero ahora la caja dura 7,5 s');
      juego.handleMessage(efecto('puppet_rescatado', multa: 4, segundosCaja: 7.5));
      expect(juego.session.segundosDeCaja, 7.5);
      expect(juego.session.multaDeCaja, 4);
    });

    test('los segundos se escriben sin ceros de más (15, 7,5, 3,75)', () {
      expect(AvisoDeCompanero.formatearSegundos(15), '15');
      expect(AvisoDeCompanero.formatearSegundos(7.5), '7,5');
      expect(AvisoDeCompanero.formatearSegundos(3.75), '3,75');
    });

    test('un Unity que solo manda el factor: se avisa con "×2 más rápido"', () {
      juego.handleMessage(efecto('puppet_rescatado', multa: 2));
      expect(juego.session.avisoDeCompanero!.detalle, contains('×2'));
      expect(juego.session.avisoDeCompanero!.detalle, contains('más rápido'));
      expect(juego.session.segundosDeCaja, isNull);
    });

    test('estado_puppet trae la multa y la duración vigentes; sin esos campos, la caja va normal', () {
      juego.handleMessage(<String, dynamic>{
        'type': 'estado_puppet',
        'en_peligro': false,
        'valor_caja_porcentaje': 80,
        'multa_factor': 2.0,
        'segundos_caja': 15.0,
      });
      expect(juego.session.multaDeCaja, 2);
      expect(juego.session.segundosDeCaja, 15);
      juego.handleMessage(<String, dynamic>{
        'type': 'estado_puppet',
        'en_peligro': false,
        'valor_caja_porcentaje': 70,
      });
      expect(juego.session.multaDeCaja, 1, reason: 'un Unity viejo no manda multa: la caja va normal');
      expect(juego.session.segundosDeCaja, isNull);
    });

    test('compañero conectado: avisa y explica que los animatrónicos van más rápido', () {
      expect(juego.session.companeroConectado, isNull, reason: 'todavía no se sabe');
      juego.handleMessage(efecto('operador_conectado'));
      expect(juego.session.companeroConectado, isTrue);
      expect(juego.session.avisoDeCompanero!.titulo, 'Tu compañero se conectó');
      expect(juego.session.avisoDeCompanero!.detalle, contains('más rápido'));
    });

    test('compañero desconectado: avisa y dice que vuelven a su ritmo', () {
      juego.handleMessage(efecto('operador_conectado'));
      juego.handleMessage(efecto('operador_perdido'));
      expect(juego.session.companeroConectado, isFalse);
      expect(juego.session.avisoDeCompanero!.titulo, 'Tu compañero se desconectó');
      expect(juego.session.avisoDeCompanero!.detalle, contains('ritmo normal'));
    });

    test('un efecto de un tipo que no conoce usa el texto que mandó Unity', () {
      juego.handleMessage(efecto('algo_nuevo', message: 'Pasó algo'));
      expect(juego.session.avisoDeCompanero!.titulo, 'Pasó algo');
    });

    test('un efecto desconocido y sin texto se ignora sin fallar', () {
      juego.handleMessage(efecto('algo_nuevo'));
      expect(juego.session.avisoDeCompanero, isNull);
    });

    test('cada efecto avisa una vez a quien escucha (para vibrar)', () {
      final List<String> tipos = <String>[];
      juego.alAvisoDeCompanero = (AvisoDeCompanero a) => tipos.add(a.tipo);
      juego.handleMessage(efecto('trampa_colocada'));
      juego.handleMessage(efecto('algo_nuevo'));
      juego.handleMessage(efecto('puppet_rescatado', multa: 2));
      expect(tipos, <String>['trampa_colocada', 'puppet_rescatado']);
    });

    test('reset borra todo lo del compañero', () {
      juego.handleMessage(efecto('operador_conectado'));
      juego.handleMessage(efecto('puppet_rescatado', multa: 2, segundosCaja: 15));
      juego.reset();
      expect(juego.session.avisoDeCompanero, isNull);
      expect(juego.session.companeroConectado, isNull);
      expect(juego.session.multaDeCaja, 1);
      expect(juego.session.segundosDeCaja, isNull);
    });
  });

  group('FranjaDelCompanero', () {
    Future<void> montar(WidgetTester tester, Widget franja) async {
      await tester.pumpWidget(MaterialApp(home: Scaffold(body: franja)));
    }

    AvisoDeCompanero aviso(String tipo, {num multa = 0, String animatronic = ''}) =>
        AvisoDeCompanero.deEfecto(efecto(tipo, multa: multa, animatronic: animatronic))!;

    testWidgets('muestra el aviso y lo quita solo a los 5 s', (tester) async {
      await montar(tester, FranjaDelCompanero(aviso: aviso('trampa_colocada')));
      expect(find.text('Tu compañero puso una trampa'), findsOneWidget);
      await tester.pump(const Duration(seconds: 4));
      expect(find.text('Tu compañero puso una trampa'), findsOneWidget);
      await tester.pump(const Duration(seconds: 2));
      expect(find.text('Tu compañero puso una trampa'), findsNothing);
    });

    testWidgets('un aviso nuevo reemplaza al anterior y reinicia los 5 s', (tester) async {
      await montar(tester, FranjaDelCompanero(aviso: aviso('trampa_colocada')));
      await tester.pump(const Duration(seconds: 4));
      await montar(tester, FranjaDelCompanero(aviso: aviso('ralentizado', animatronic: 'vixy')));
      expect(find.text('Tu compañero ralentizó a Vixy'), findsOneWidget);
      await tester.pump(const Duration(seconds: 4));
      expect(find.text('Tu compañero ralentizó a Vixy'), findsOneWidget, reason: 'cuenta desde que llegó');
      await tester.pump(const Duration(seconds: 2));
      expect(find.text('Tu compañero ralentizó a Vixy'), findsNothing);
    });

    testWidgets('el rescate muestra título y cuánto dura ahora la caja', (tester) async {
      final AvisoDeCompanero rescate =
          AvisoDeCompanero.deEfecto(efecto('puppet_rescatado', multa: 2, segundosCaja: 15))!;
      await montar(tester, FranjaDelCompanero(aviso: rescate));
      expect(find.text('Tu compañero te salvó de Puppet'), findsOneWidget);
      expect(find.text('Pero ahora la caja dura 15 s'), findsOneWidget);
    });

    testWidgets('el texto del aviso es grande (se lee de reojo) y se anuncia a TalkBack', (tester) async {
      await montar(tester, FranjaDelCompanero(aviso: aviso('trampa_colocada')));
      final TextStyle estilo = tester.widget<Text>(find.text('Tu compañero puso una trampa')).style!;
      expect(estilo.fontSize, greaterThanOrEqualTo(24));
      final SemanticsHandle semantica = tester.ensureSemantics();
      expect(
        tester.getSemantics(find.text('Tu compañero puso una trampa')),
        matchesSemantics(label: 'Tu compañero puso una trampa', isLiveRegion: true),
      );
      semantica.dispose();
    });

    testWidgets('chip del compañero: conectado, desconectado o nada si nunca hubo', (tester) async {
      await montar(tester, const FranjaDelCompanero(companeroConectado: null));
      expect(find.textContaining('Compañero'), findsNothing);
      await montar(tester, const FranjaDelCompanero(companeroConectado: true));
      expect(find.text('Compañero conectado'), findsOneWidget);
      await montar(tester, const FranjaDelCompanero(companeroConectado: false));
      expect(find.text('Compañero desconectado'), findsOneWidget);
    });

    testWidgets('chip de la caja: aparece solo tras un rescate y dice cuánto dura', (tester) async {
      await montar(tester, const FranjaDelCompanero(multaDeCaja: 1));
      expect(find.textContaining('Caja'), findsNothing);
      await montar(tester, const FranjaDelCompanero(multaDeCaja: 2, segundosDeCaja: 15));
      expect(find.text('Caja de Puppet dura 15 s'), findsOneWidget);
      await montar(tester, const FranjaDelCompanero(multaDeCaja: 4, segundosDeCaja: 7.5));
      expect(find.text('Caja de Puppet dura 7,5 s'), findsOneWidget);
    });

    testWidgets('sin la duración (Unity viejo) el chip usa el factor', (tester) async {
      await montar(tester, const FranjaDelCompanero(multaDeCaja: 4));
      expect(find.text('Caja de Puppet ×4 más rápida'), findsOneWidget);
    });
  });

  group('vibración por lo que hace el compañero', () {
    final DateTime t0 = DateTime(2026, 11, 10, 12);
    late VibradorFalso vibrador;
    late ServicioDeAvisos servicio;
    DateTime ahora = t0;

    setUp(() async {
      vibrador = VibradorFalso();
      ahora = t0;
      servicio = ServicioDeAvisos(vibrador: vibrador, ahora: () => ahora);
      await servicio.iniciar();
    });

    int pulsos(List<int> p) => p.length ~/ 2;

    test('una trampa o un retraso dan un pulso corto', () {
      servicio.avisarDeCompanero('trampa_colocada');
      servicio.avisarDeCompanero('ralentizado');
      expect(vibrador.patrones, hasLength(2));
      expect(pulsos(vibrador.patrones.first), 1);
    });

    test('el rescate de Puppet da dos pulsos (distinto, es lo más importante)', () {
      servicio.avisarDeCompanero('puppet_rescatado');
      expect(pulsos(vibrador.patrones.single), 2);
    });

    test('si se cae el compañero vibra más largo que un aviso normal', () {
      servicio.avisarDeCompanero('operador_perdido');
      final List<int> p = vibrador.patrones.single;
      expect(p[1], greaterThan(300));
    });

    test('no corta el "trrr" de la caja en peligro con un aviso menor', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      vibrador.patrones.clear();
      servicio.avisarDeCompanero('trampa_colocada');
      expect(vibrador.patrones, isEmpty);
    });

    test('pero el rescate sí vibra, aunque la caja estuviera en peligro, y no lo cancela la revisión', () {
      servicio.revisar(ahora: t0, cajaEnPeligro: true, tareas: const []);
      vibrador.patrones.clear();
      final int cancelacionesAntes = vibrador.cancelaciones;
      servicio.avisarDeCompanero('puppet_rescatado');
      expect(vibrador.patrones, hasLength(1));
      final int cancelacionesTrasRescate = vibrador.cancelaciones;
      // Un segundo después la tablet aún tiene el estado viejo (caja en peligro)
      // o ya el nuevo (a salvo): ninguno de los dos debe tapar el aviso.
      ahora = t0.add(const Duration(seconds: 1));
      servicio.revisar(ahora: ahora, cajaEnPeligro: true, tareas: const []);
      servicio.revisar(ahora: ahora, cajaEnPeligro: false, tareas: const []);
      expect(vibrador.patrones, hasLength(1), reason: 'no vuelve a sonar el "trrr"');
      expect(vibrador.cancelaciones, cancelacionesTrasRescate, reason: 'no cancela el aviso del rescate');
      expect(cancelacionesTrasRescate, greaterThanOrEqualTo(cancelacionesAntes));
    });

    test('un tipo desconocido no vibra', () {
      servicio.avisarDeCompanero('algo_nuevo');
      expect(vibrador.patrones, isEmpty);
    });
  });

  group('GameScreen', () {
    Future<GameProvider> montar(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1600, 960);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      final GameProvider juego = GameProvider();
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

    testWidgets('lo que hace el compañero se ve en la pantalla de juego de la tablet', (tester) async {
      final GameProvider juego = await montar(tester);
      expect(find.text('Tu compañero puso una trampa'), findsNothing);
      juego.handleMessage(efecto('trampa_colocada'));
      await tester.pump();
      expect(find.text('Tu compañero puso una trampa'), findsOneWidget);
      await tester.pump(const Duration(seconds: 6));
      expect(find.text('Tu compañero puso una trampa'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('la multa de la caja queda a la vista mientras dure', (tester) async {
      final GameProvider juego = await montar(tester);
      juego.handleMessage(efecto('puppet_rescatado', multa: 2, segundosCaja: 15));
      await tester.pump();
      await tester.pump(const Duration(seconds: 6));
      expect(find.text('Caja de Puppet dura 15 s'), findsOneWidget, reason: 'el aviso se fue, la multa sigue');
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('el estado del compañero se ve desde que entra', (tester) async {
      final GameProvider juego = await montar(tester);
      juego.handleMessage(efecto('operador_conectado'));
      await tester.pump();
      expect(find.text('Compañero conectado'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
