import 'package:flutter/material.dart' hide ConnectionState;
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/screens/splash_screen.dart';
import 'package:fn_attention_defense_tablet/screens/tutorial_screen.dart';
import 'package:provider/provider.dart';

import 'ayudas/servicio_falso.dart';

void main() {
  late WebSocketFalso ws;
  late ConnectionProvider conexion;

  const Map<String, dynamic> respuestaDeUnity = <String, dynamic>{
    'type': 'night_status',
    'night': 1,
    'in_game_time': '12:00 AM',
    'risk_percent': 0,
  };

  setUp(() {
    ServerConfig.useMock = false;
    ws = WebSocketFalso();
    conexion = ConnectionProvider(wsService: ws);
  });

  tearDown(() {
    conexion.disconnect();
    ServerConfig.useMock = true;
  });

  Future<void> montar(WidgetTester tester, Widget pantalla) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider<ConnectionProvider>.value(value: conexion),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: MaterialApp(home: pantalla),
    ));
    await tester.pump();
  }

  Future<void> cerrar(WidgetTester tester) async {
    conexion.disconnect();
    await tester.pumpWidget(const SizedBox());
  }

  group('pantalla de arranque (Nuevo juego / Continuar)', () {
    testWidgets('mientras conecta dice a dónde busca el juego', (tester) async {
      await montar(tester, const SplashScreen());
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Conectando con la PC...'), findsOneWidget);
      expect(find.textContaining('${ServerConfig.host}:${ServerConfig.port}'), findsOneWidget);
      await cerrar(tester);
    });

    testWidgets('si el juego no responde, avisa qué hacer y deja reintentar', (tester) async {
      await montar(tester, const SplashScreen());
      await tester.pump(const Duration(seconds: 9));
      expect(find.text('No se encontró el juego'), findsOneWidget);
      expect(find.textContaining('Revisa que el juego esté abierto'), findsOneWidget);
      expect(find.widgetWithText(ElevatedButton, 'Reintentar'), findsOneWidget);
      expect(find.widgetWithText(OutlinedButton, 'Opciones'), findsOneWidget);
      expect(find.widgetWithText(TextButton, 'Volver al menú'), findsOneWidget);

      await tester.tap(find.widgetWithText(ElevatedButton, 'Reintentar'));
      await tester.pump();
      expect(ws.conexiones, 2);
      expect(conexion.state, ConnectionState.connecting);
      expect(find.text('Conectando con la PC...'), findsOneWidget);
      await cerrar(tester);
    });

    testWidgets('los botones de error son grandes', (tester) async {
      await montar(tester, const SplashScreen());
      await tester.pump(const Duration(seconds: 9));
      expect(tester.getSize(find.widgetWithText(ElevatedButton, 'Reintentar')).height, greaterThanOrEqualTo(60));
      await cerrar(tester);
    });

    testWidgets('cuando el juego responde, pasa a la partida', (tester) async {
      await montar(tester, const SplashScreen());
      ws.llega(respuestaDeUnity);
      // El mensaje se procesa en el primer cuadro; la pantalla reacciona en los
      // siguientes (cambio de estado, navegación y transición).
      await tester.pump();
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 600));
      expect(find.byType(GameScreen), findsOneWidget);
      await cerrar(tester);
    });
  });

  group('tutorial', () {
    testWidgets('no dice que ves la PC hasta que el juego responde', (tester) async {
      await montar(tester, const TutorialScreen());
      await tester.pump(const Duration(seconds: 1));
      expect(find.text('Conectando...'), findsOneWidget);
      expect(find.text('Mira la pantalla de la computadora'), findsNothing);
      await cerrar(tester);
    });

    testWidgets('con el juego conectado habla de tú, no de vos', (tester) async {
      await montar(tester, const TutorialScreen());
      ws.llega(const <String, dynamic>{'type': 'tutorial_estado', 'indice': 0, 'total': 3});
      await tester.pump();
      await tester.pump();
      expect(find.text('Mira la pantalla de la computadora'), findsOneWidget);
      expect(find.text('Usa los botones de abajo para avanzar'), findsOneWidget);
      expect(find.textContaining('Mirá'), findsNothing);
      expect(find.textContaining('Usá'), findsNothing);
      await cerrar(tester);
    });

    testWidgets('si el juego no responde, lo dice en vez de quedarse "conectando"', (tester) async {
      await montar(tester, const TutorialScreen());
      await tester.pump(const Duration(seconds: 9));
      expect(find.text('No se encontró el juego'), findsOneWidget);
      expect(find.text('Conectando...'), findsNothing);
      await cerrar(tester);
    });
  });
}
