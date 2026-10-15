import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/widgets/puppet_game_widget.dart';

void main() {
  Future<({int empezo, int solto})> mantener(
    WidgetTester tester, {
    required Offset desplazamiento,
  }) async {
    int empezo = 0;
    int solto = 0;
    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: PuppetGameWidget(
          enPeligro: false,
          valorCajaPorcentaje: 50,
          alEmpezarASostener: () => empezo++,
          alSoltar: () => solto++,
        ),
      ),
    ));
    final Finder boton = find.byIcon(Icons.music_note);
    final TestGesture dedo = await tester.startGesture(tester.getCenter(boton));
    await tester.pump(const Duration(milliseconds: 300));
    if (desplazamiento != Offset.zero) {
      await dedo.moveBy(desplazamiento);
      await tester.pump(const Duration(milliseconds: 300));
    }
    final int soltoMientrasSostiene = solto;
    await dedo.up();
    await tester.pump();
    expect(solto, 1, reason: 'al levantar el dedo se suelta exactamente una vez');
    return (empezo: empezo, solto: soltoMientrasSostiene);
  }

  testWidgets('dedo quieto: sigue sosteniendo hasta levantarlo', (tester) async {
    final r = await mantener(tester, desplazamiento: Offset.zero);
    expect(r.empezo, 1);
    expect(r.solto, 0);
  });

  testWidgets('dedo que se desliza unos píxeles: NO suelta la cuerda', (tester) async {
    final r = await mantener(tester, desplazamiento: const Offset(40, 25));
    expect(r.empezo, 1);
    expect(r.solto, 0, reason: 'un dedo que se mueve al sostener no debe cancelar');
  });
}
