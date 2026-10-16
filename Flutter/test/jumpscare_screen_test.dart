import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/attack.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_over_screen.dart';
import 'package:fn_attention_defense_tablet/screens/jumpscare_screen.dart';
import 'package:provider/provider.dart';
import 'package:video_player/video_player.dart';

/// El jumpscare de la tablet usa el mismo video que la PC, y si el video no
/// está o falla vuelve a la imagen fija de siempre: nunca queda la pantalla
/// vacía.
void main() {
  Finder imagenFija() => find.byWidgetPredicate(
    (Widget w) =>
        w is Image &&
        w.image is AssetImage &&
        (w.image as AssetImage).assetName == 'assets/images/jumpscare.jpg',
  );

  Future<void> montar(
    WidgetTester tester,
    JumpscareScreen pantalla, {
    String? animatronico,
  }) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    final GameProvider juego = GameProvider()
      ..isGameOver = true
      ..lastAttack = Attack.fromJson(<String, dynamic>{
        'type': 'attack',
        'attack_id': 'atk_1',
        'message': 'te encontró',
        'timestamp': 1791525986413,
        if (animatronico != null) 'animatronic': animatronico,
      });
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(create: (_) => ConnectionProvider()),
          ChangeNotifierProvider.value(value: juego),
        ],
        child: MaterialApp(home: pantalla),
      ),
    );
    await tester.pump();
  }

  testWidgets('pide el video del animatrónico que atacó', (tester) async {
    final List<String> pedidos = <String>[];
    VideoPlayerController falla(String ruta) {
      pedidos.add(ruta);
      throw StateError('sin reproductor en la prueba');
    }

    await montar(
      tester,
      JumpscareScreen(crearControlador: falla),
      animatronico: 'Puppet',
    );
    await tester.pump();
    expect(pedidos, <String>['assets/videos/jumpscare_puppet.webm']);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('si el video falla, queda la imagen fija y el nombre', (
    tester,
  ) async {
    VideoPlayerController falla(String ruta) =>
        throw StateError('sin reproductor en la prueba');

    await montar(
      tester,
      JumpscareScreen(crearControlador: falla),
      animatronico: 'Freddy',
    );
    await tester.pump();
    expect(imagenFija(), findsOneWidget);
    expect(find.text('FREDDY'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('un animatrónico sin video ni intenta cargarlo: imagen fija', (
    tester,
  ) async {
    int pedidos = 0;
    VideoPlayerController nunca(String ruta) {
      pedidos++;
      throw StateError('no debía pedirse');
    }

    await montar(tester, JumpscareScreen(crearControlador: nunca));
    await tester.pump();
    expect(pedidos, 0);
    expect(imagenFija(), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('pasa solo a "juego terminado" a los 1,6 s, igual que en la PC', (
    tester,
  ) async {
    VideoPlayerController falla(String ruta) =>
        throw StateError('sin reproductor en la prueba');

    await montar(
      tester,
      JumpscareScreen(crearControlador: falla),
      animatronico: 'Vixy',
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 1500));
    expect(find.byType(GameOverScreen), findsNothing);
    await tester.pump(const Duration(milliseconds: 200));
    await tester.pumpAndSettle();
    expect(find.byType(GameOverScreen), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  test(
    'el reproductor real no pide el foco de audio (si no, Android pausa el video al sonar el jumpscare)',
    () {
      final VideoPlayerController controlador = crearControladorDeJumpscare(
        'assets/videos/jumpscare_puppet.webm',
      );
      expect(controlador.videoPlayerOptions?.mixWithOthers, isTrue);
    },
  );
}
