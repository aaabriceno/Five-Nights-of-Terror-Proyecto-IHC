import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/task.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/services/servicio_de_avisos.dart';
import 'package:fn_attention_defense_tablet/utils/colors.dart';
import 'package:fn_attention_defense_tablet/utils/urgencia_de_tareas.dart';
import 'package:fn_attention_defense_tablet/widgets/menu_de_tareas.dart';
import 'package:provider/provider.dart';

import 'servicio_de_avisos_test.dart' show VibradorFalso;

/// Mejoras de jugabilidad de la tablet (2026-11-22), tras revisar el juego
/// antes de las pruebas de conectividad, inmersión y jugabilidad.
void main() {
  Task tarea({int id = 1, int duracion = 15, String tipo = 'wifi'}) => Task(
        taskId: id,
        taskType: tipo,
        duration: duracion,
        description: 'x',
        difficulty: 1,
        createdAt: DateTime.now(),
        params: const {},
      );

  Map<String, dynamic> listaDeTareas(int timestampDeLaPc) => <String, dynamic>{
        'type': 'task_list',
        'tasks': <Map<String, dynamic>>[
          <String, dynamic>{
            'task_id': 7,
            'task_type': 'cables',
            'duration': 30,
            'description': 'Conectar los cables',
            'difficulty': 1,
            'timestamp': timestampDeLaPc,
            'task_params': <String, dynamic>{},
          },
        ],
      };

  group('la urgencia se cuenta con el reloj de la tablet', () {
    test('con la PC 10 min atrasada, la tarea no sale roja apenas llega', () {
      DateTime ahora = DateTime(2026, 11, 22, 12);
      final GameProvider juego = GameProvider(reloj: () => ahora);
      final int pcAtrasada = ahora.subtract(const Duration(minutes: 10)).millisecondsSinceEpoch;
      juego.handleMessage(listaDeTareas(pcAtrasada));
      expect(nivelDeUrgencia(juego.session.tareasPendientes.single, ahora), NivelDeUrgencia.normal);

      ahora = ahora.add(const Duration(seconds: 25));
      juego.handleMessage(listaDeTareas(pcAtrasada)); // Unity reenvía la lista
      expect(nivelDeUrgencia(juego.session.tareasPendientes.single, ahora), NivelDeUrgencia.ambar,
          reason: 'la tarea conserva la hora en que llegó, aunque la lista se reenvíe');
    });

    test('con la PC 10 min adelantada, la tarea igual llega a rojo a los 40 s', () {
      DateTime ahora = DateTime(2026, 11, 22, 12);
      final GameProvider juego = GameProvider(reloj: () => ahora);
      final int pcAdelantada = ahora.add(const Duration(minutes: 10)).millisecondsSinceEpoch;
      juego.handleMessage(listaDeTareas(pcAdelantada));
      ahora = ahora.add(const Duration(seconds: 41));
      juego.handleMessage(listaDeTareas(pcAdelantada));
      expect(nivelDeUrgencia(juego.session.tareasPendientes.single, ahora), NivelDeUrgencia.roja);
    });

    test('una tarea nueva (otro id) cuenta desde su propia llegada', () {
      DateTime ahora = DateTime(2026, 11, 22, 12);
      final GameProvider juego = GameProvider(reloj: () => ahora);
      juego.handleMessage(listaDeTareas(0));
      ahora = ahora.add(const Duration(seconds: 45));
      final Map<String, dynamic> otra = listaDeTareas(0);
      (otra['tasks'] as List<Map<String, dynamic>>).first['task_id'] = 8;
      juego.handleMessage(otra);
      expect(nivelDeUrgencia(juego.session.tareasPendientes.single, ahora), NivelDeUrgencia.normal);
    });
  });

  group('respuesta al terminar una tarea', () {
    test('el provider avisa si se resolvió o se falló, y la fallada sigue pendiente', () {
      final GameProvider juego = GameProvider();
      final List<bool> resultados = <bool>[];
      juego.alResultadoDeTarea = resultados.add;
      final Task wifi = tarea();
      juego.session.tareasPendientes = <Task>[wifi];
      juego.elegirTarea(wifi);
      juego.reportTaskCompleted(success: false, timeTaken: 3);
      expect(resultados, <bool>[false]);
      expect(juego.session.tareasPendientes, contains(wifi));
      juego.elegirTarea(wifi);
      juego.reportTaskCompleted(success: true, timeTaken: 3);
      expect(resultados, <bool>[false, true]);
      expect(juego.session.tareasPendientes, isEmpty);
    });

    test('vibra distinto al resolver (un toque) y al fallar (dos toques)', () async {
      final VibradorFalso vibrador = VibradorFalso();
      final ServicioDeAvisos servicio = ServicioDeAvisos(vibrador: vibrador);
      await servicio.iniciar();
      servicio.avisarResultadoDeTarea(true);
      servicio.avisarResultadoDeTarea(false);
      expect(vibrador.patrones, <List<int>>[
        ServicioDeAvisos.patronDeTareaResuelta,
        ServicioDeAvisos.patronDeTareaFallada,
      ]);
      expect(ServicioDeAvisos.patronDeTareaResuelta.length ~/ 2, 1);
      expect(ServicioDeAvisos.patronDeTareaFallada.length ~/ 2, 2);
    });

    test('no corta el «trrr» de la caja en peligro', () async {
      final VibradorFalso vibrador = VibradorFalso();
      final ServicioDeAvisos servicio = ServicioDeAvisos(vibrador: vibrador);
      await servicio.iniciar();
      servicio.revisar(ahora: DateTime(2026), cajaEnPeligro: true, tareas: const <Task>[]);
      vibrador.patrones.clear();
      servicio.avisarResultadoDeTarea(true);
      expect(vibrador.patrones, isEmpty);
    });

    Future<GameProvider> montarPartida(WidgetTester tester) async {
      tester.view.physicalSize = const Size(1600, 960);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      final GameProvider juego = GameProvider();
      final Task wifi = tarea();
      juego.session.tareasPendientes = <Task>[wifi];
      juego.session.currentTask = wifi;
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

    testWidgets('al fallar se ve en grande que falló y que sigue pendiente, y luego se va', (tester) async {
      final GameProvider juego = await montarPartida(tester);
      juego.reportTaskCompleted(success: false, timeTaken: 3);
      await tester.pump();
      final Finder aviso = find.text('Fallaste: la tarea sigue pendiente');
      expect(aviso, findsOneWidget);
      expect(tester.widget<Text>(aviso).style?.fontSize, greaterThanOrEqualTo(24));
      await tester.pump(const Duration(seconds: 3));
      expect(aviso, findsNothing);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('al resolverla se ve «¡Tarea resuelta!»', (tester) async {
      final GameProvider juego = await montarPartida(tester);
      juego.reportTaskCompleted(success: true, timeTaken: 3);
      await tester.pump();
      expect(find.text('¡Tarea resuelta!'), findsOneWidget);
      await tester.pump(const Duration(seconds: 3));
      expect(find.text('¡Tarea resuelta!'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('el aviso no tapa los toques: se puede abrir otra tarea enseguida', (tester) async {
      final GameProvider juego = await montarPartida(tester);
      final Task otra = tarea(id: 2, tipo: 'cables', duracion: 30);
      juego.session.tareasPendientes = <Task>[otra];
      juego.reportTaskCompleted(success: true, timeTaken: 3);
      await tester.pump();
      await tester.tap(find.text('Conectar Cables'));
      await tester.pump();
      expect(juego.session.currentTask, otra);
      await tester.pumpWidget(const SizedBox());
    });
  });

  group('menú de tareas', () {
    Future<void> montarMenu(WidgetTester tester, {required List<Task> tareas, int caja = 63, bool peligro = false}) async {
      tester.view.physicalSize = const Size(1600, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      await tester.pumpWidget(MaterialApp(
        home: Scaffold(
          body: MenuDeTareas(
            tareas: tareas,
            alElegirTarea: (_) {},
            puppetEnPeligro: peligro,
            valorCajaPorcentaje: caja,
            alAbrirCajaDePuppet: () {},
          ),
        ),
      ));
      await tester.pump();
    }

    testWidgets('la tarjeta de la caja muestra cuánta cuerda le queda, con número y barra', (tester) async {
      await montarMenu(tester, tareas: const <Task>[], caja: 63);
      expect(find.text('63 %'), findsOneWidget);
      final LinearProgressIndicator barra =
          tester.widget<LinearProgressIndicator>(find.byType(LinearProgressIndicator));
      expect(barra.value, closeTo(0.63, 0.001));
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('en peligro la barra de la caja se pone roja', (tester) async {
      await montarMenu(tester, tareas: const <Task>[], caja: 12, peligro: true);
      final LinearProgressIndicator barra =
          tester.widget<LinearProgressIndicator>(find.byType(LinearProgressIndicator));
      expect(barra.color, AppColors.peligro);
      expect(find.text('12 %'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('cada tarea dice cuánto tiempo hay para resolverla', (tester) async {
      await montarMenu(tester, tareas: <Task>[tarea(duracion: 15)]);
      expect(find.text('15 s para resolverla'), findsOneWidget);
      expect(find.text('15s'), findsNothing);
      await tester.pumpWidget(const SizedBox());
    });
  });
}
