import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/attack.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_over_screen.dart';
import 'package:fn_attention_defense_tablet/screens/jumpscare_screen.dart';
import 'package:provider/provider.dart';
import 'package:video_player/video_player.dart';

/// Un video que nunca termina de cargar (como uno muy lento en la tablet).
class VideoQueNoCarga extends VideoPlayerController {
  VideoQueNoCarga() : super.asset('assets/videos/jumpscare_puppet.webm');

  @override
  Future<void> initialize() => Completer<void>().future;
}

/// El jumpscare de la tablet usa el mismo video que la PC. Desde el
/// 2026-11-24 la cara del animatrónico (sacada de su video) y el grito salen
/// al instante, sin esperar al video; el video la tapa al cargar y, si falla,
/// queda la cara: nunca queda la pantalla vacía.
void main() {
  Finder imagen(String ruta) => find.byWidgetPredicate(
    (Widget w) =>
        w is Image &&
        w.image is AssetImage &&
        (w.image as AssetImage).assetName == ruta,
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

  testWidgets('la cara del que atacó y el grito salen al instante, sin esperar al video', (
    tester,
  ) async {
    int gritos = 0;
    await montar(
      tester,
      JumpscareScreen(
        crearControlador: (String ruta) => VideoQueNoCarga(),
        reproducirGrito: () async => gritos++,
      ),
      animatronico: 'Puppet',
    );
    expect(imagen('assets/images/jumpscare_puppet.jpg'), findsOneWidget);
    expect(gritos, 1);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('si el video no termina de cargar, igual pasa a "juego terminado" a los 1,6 s', (
    tester,
  ) async {
    await montar(
      tester,
      JumpscareScreen(
        crearControlador: (String ruta) => VideoQueNoCarga(),
        reproducirGrito: () async {},
      ),
      animatronico: 'Puppet',
    );
    await tester.pump(const Duration(milliseconds: 1500));
    expect(find.byType(GameOverScreen), findsNothing);
    expect(imagen('assets/images/jumpscare_puppet.jpg'), findsOneWidget);
    await tester.pump(const Duration(milliseconds: 200));
    await tester.pumpAndSettle();
    expect(find.byType(GameOverScreen), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('si el video falla, queda la cara de Freddy y el nombre', (
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
    expect(imagen('assets/images/jumpscare_freddy.jpg'), findsOneWidget);
    expect(find.text('FREDDY'), findsOneWidget);
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });

  testWidgets('un animatrónico sin video ni intenta cargarlo: imagen general', (
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
    expect(imagen('assets/images/jumpscare.jpg'), findsOneWidget);
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
