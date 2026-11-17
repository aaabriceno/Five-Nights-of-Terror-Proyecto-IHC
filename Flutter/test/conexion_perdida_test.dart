import 'package:flutter/material.dart' hide ConnectionState;
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_screen.dart';
import 'package:fn_attention_defense_tablet/screens/tutorial_screen.dart';
import 'package:provider/provider.dart';

import 'ayudas/servicio_falso.dart';

/// Si la tablet (el jugador 1) pierde la conexión a mitad de la noche, la
/// partida termina y no hay forma de volver a ella: la tablet tiene que decirlo
/// en grande, no con un texto chico en la barra (2026-11-17).
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

  tearDown(() => ServerConfig.useMock = true);

  Future<void> montar(WidgetTester tester, Widget pantalla) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: conexion),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: MaterialApp(home: pantalla),
    ));
    await tester.pump();
  }

  testWidgets('en plena noche, si se cae la conexión, la tablet lo dice y ofrece volver al menú', (tester) async {
    conexion.connect();
    ws.llega(respuestaDeUnity);
    await montar(tester, const GameScreen());
    expect(find.text('SE PERDIÓ LA CONEXIÓN'), findsNothing);

    ws.seCierra();
    await tester.pumpAndSettle();
    expect(find.text('SE PERDIÓ LA CONEXIÓN'), findsOneWidget);
    expect(find.textContaining('la noche terminó'), findsOneWidget);
    expect(find.widgetWithText(ElevatedButton, 'Volver al menú'), findsOneWidget);
    expect(find.byType(GameScreen), findsNothing, reason: 'ya no se puede jugar esa noche');
  });

  testWidgets('en el tutorial, una conexión perdida no se confunde con «no se encontró el juego»', (tester) async {
    await montar(tester, const TutorialScreen()); // la pantalla se conecta sola
    ws.llega(<String, dynamic>{'type': 'tutorial_estado', 'indice': 0, 'total': 3});
    await tester.pump();
    expect(conexion.state, ConnectionState.connected);
    ws.seCierra();
    await tester.pump();
    expect(find.text('Se perdió la conexión con el juego'), findsOneWidget);
    expect(find.text('No se encontró el juego'), findsNothing);
  });
}
