import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/models/attack.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/game_over_screen.dart';
import 'package:fn_attention_defense_tablet/screens/jumpscare_screen.dart';
import 'package:fn_attention_defense_tablet/utils/animatronicos.dart';
import 'package:provider/provider.dart';

/// Un tester no entendió quién lo había matado: Unity ya manda el nombre del
/// animatrónico en el mensaje `attack`, pero la tablet lo ignoraba.
void main() {
  Map<String, dynamic> mensajeDeAtaque({String? animatronico}) => <String, dynamic>{
        'type': 'attack',
        'attack_id': 'atk_1',
        'message': 'te encontró',
        'timestamp': 1791525986413,
        if (animatronico != null) 'animatronic': animatronico,
      };

  group('Attack.fromJson', () {
    test('lee el animatrónico que manda Unity', () {
      expect(Attack.fromJson(mensajeDeAtaque(animatronico: 'Vixy')).animatronic, 'Vixy');
    });

    test('si el mensaje no lo trae, queda en null (no falla)', () {
      expect(Attack.fromJson(mensajeDeAtaque()).animatronic, isNull);
    });
  });

  group('infoDeAnimatronico', () {
    test('cada animatrónico tiene nombre, motivo e icono propios', () {
      final List<InfoDeAnimatronico> infos = <InfoDeAnimatronico>[
        infoDeAnimatronico('Freddy'),
        infoDeAnimatronico('Vixy'),
        infoDeAnimatronico('Puppet'),
      ];
      expect(infos.map((InfoDeAnimatronico i) => i.nombre), <String>['FREDDY', 'VIXY', 'PUPPET']);
      expect(infos.map((InfoDeAnimatronico i) => i.icono).toSet(), hasLength(3),
          reason: 'se distinguen por forma, no solo por color');
      for (final InfoDeAnimatronico i in infos) {
        expect(i.motivo, isNotEmpty);
      }
    });

    test('no distingue mayúsculas y un nombre desconocido o ausente es genérico', () {
      expect(infoDeAnimatronico('freddy').nombre, 'FREDDY');
      expect(infoDeAnimatronico(null).nombre, 'UN ANIMATRÓNICO');
      expect(infoDeAnimatronico('Animatrónico').nombre, 'UN ANIMATRÓNICO');
      expect(infoDeAnimatronico('Otro').nombre, 'UN ANIMATRÓNICO');
    });

    test('el motivo explica la mecánica de cada uno', () {
      expect(infoDeAnimatronico('Freddy').motivo, contains('pantalla'));
      expect(infoDeAnimatronico('Vixy').motivo, contains('tareas'));
      expect(infoDeAnimatronico('Puppet').motivo, contains('caja'));
    });
  });

  Future<void> montar(WidgetTester tester, Widget pantalla, {String? animatronico, int noche = 2}) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    final GameProvider juego = GameProvider()
      ..ultimaNocheDeGameOver = noche
      ..isGameOver = true
      ..lastAttack = Attack.fromJson(mensajeDeAtaque(animatronico: animatronico));
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ConnectionProvider()),
        ChangeNotifierProvider.value(value: juego),
      ],
      child: MaterialApp(home: pantalla),
    ));
    await tester.pump();
  }

  group('pantalla de juego terminado', () {
    testWidgets('dice quién te atrapó, en grande, con su icono y por qué', (tester) async {
      await montar(tester, const GameOverScreen(), animatronico: 'Vixy');
      final Finder titulo = find.text('TE ATRAPÓ VIXY');
      expect(titulo, findsOneWidget);
      expect(tester.widget<Text>(titulo).style!.fontSize, greaterThanOrEqualTo(44));
      expect(find.byIcon(infoDeAnimatronico('Vixy').icono), findsOneWidget);
      expect(find.text(infoDeAnimatronico('Vixy').motivo), findsOneWidget);
      expect(find.textContaining('Noche 2'), findsOneWidget);
    });

    testWidgets('con Freddy y con Puppet muestra a cada uno', (tester) async {
      await montar(tester, const GameOverScreen(), animatronico: 'Freddy');
      expect(find.text('TE ATRAPÓ FREDDY'), findsOneWidget);
      await montar(tester, const GameOverScreen(), animatronico: 'Puppet');
      expect(find.text('TE ATRAPÓ PUPPET'), findsOneWidget);
    });

    testWidgets('sin nombre en el mensaje se ve el texto de antes', (tester) async {
      await montar(tester, const GameOverScreen());
      expect(find.text('TE ATRAPÓ UN ANIMATRÓNICO'), findsNothing);
      expect(find.text('Te atraparon en la Noche 2'), findsOneWidget);
    });

    testWidgets('el botón Continuar es grande', (tester) async {
      await montar(tester, const GameOverScreen(), animatronico: 'Freddy');
      expect(tester.getSize(find.widgetWithText(ElevatedButton, 'Continuar')).height, greaterThanOrEqualTo(56));
    });
  });

  testWidgets('el jumpscare de la tablet ya muestra el nombre del que te atrapó', (tester) async {
    await montar(tester, const JumpscareScreen(), animatronico: 'Puppet');
    expect(find.text('PUPPET'), findsOneWidget);
    // El jumpscare cambia solo a "juego terminado"; se desmonta antes.
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(seconds: 3));
  });
}
