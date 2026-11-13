import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/screens/configurar_servidor_screen.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// La pantalla de Opciones la usa quien va a jugar, no quien programa: tiene que
/// hablar de la PC y de la partida, dejar escribir una IP (con punto) y avisar
/// cuando la IP está mal antes de guardarla.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    ServerConfig.useMock = false;
    ServerConfig.host = '192.168.1.100';
    ServerConfig.port = 8000;
  });

  tearDown(() {
    ServerConfig.useMock = true;
  });

  Future<void> montar(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1600, 960);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(const MaterialApp(home: ConfigurarServidorScreen()));
    await tester.pump();
  }

  Finder campoIp() => find.byKey(const Key('campo-ip'));
  Finder campoPuerto() => find.byKey(const Key('campo-puerto'));

  test('esIpValida acepta IPv4 y rechaza lo demás', () {
    expect(ServerConfig.esIpValida('192.168.1.100'), isTrue);
    expect(ServerConfig.esIpValida('10.0.0.1'), isTrue);
    expect(ServerConfig.esIpValida('192.168.1'), isFalse);
    expect(ServerConfig.esIpValida('192.168.1.256'), isFalse);
    expect(ServerConfig.esIpValida('abc'), isFalse);
    expect(ServerConfig.esIpValida(''), isFalse);
    expect(ServerConfig.esIpValida('192.168.1.100 '), isTrue, reason: 'los espacios de más no cuentan');
  });

  testWidgets('habla de la PC y de la partida, no de servidores ni mocks', (tester) async {
    await montar(tester);
    expect(find.text('Jugar sin la PC (modo de prueba)'), findsOneWidget);
    expect(find.text('IP de la PC'), findsOneWidget);
    expect(find.textContaining('pantalla de la PC'), findsOneWidget, reason: 'dice dónde ver la IP');
    expect(find.textContaining('Modo simulado'), findsNothing);
    expect(find.textContaining('servidor'), findsNothing);
  });

  testWidgets('el campo de la IP deja escribir el punto (teclado numérico con decimal)', (tester) async {
    await montar(tester);
    final TextField campo = tester.widget<TextField>(campoIp());
    expect(campo.keyboardType, const TextInputType.numberWithOptions(decimal: true));
  });

  testWidgets('los textos son grandes (≥ 22 px el título del interruptor, ≥ 20 px lo que se escribe)', (tester) async {
    await montar(tester);
    final Text titulo = tester.widget<Text>(find.text('Jugar sin la PC (modo de prueba)'));
    expect(titulo.style?.fontSize, greaterThanOrEqualTo(22));
    final TextField campo = tester.widget<TextField>(campoIp());
    expect(campo.style?.fontSize, greaterThanOrEqualTo(20));
    expect(tester.getSize(find.widgetWithText(ElevatedButton, 'Guardar')).height, greaterThanOrEqualTo(60));
  });

  testWidgets('una IP incompleta no se guarda y se explica cómo va', (tester) async {
    await montar(tester);
    await tester.enterText(campoIp(), '192.168.1');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Guardar'));
    await tester.pump();
    expect(find.textContaining('cuatro números'), findsOneWidget);
    expect(ServerConfig.host, '192.168.1.100', reason: 'no cambió');
    expect(find.byType(ConfigurarServidorScreen), findsOneWidget, reason: 'sigue en Opciones');
  });

  testWidgets('un puerto fuera de rango tampoco se guarda', (tester) async {
    await montar(tester);
    await tester.enterText(campoPuerto(), '70000');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Guardar'));
    await tester.pump();
    expect(find.textContaining('1 a 65535'), findsOneWidget);
    expect(ServerConfig.port, 8000);
  });

  testWidgets('una IP buena se guarda y la confirmación dice a dónde va a buscar', (tester) async {
    await montar(tester);
    await tester.enterText(campoIp(), '10.0.0.7');
    await tester.tap(find.widgetWithText(ElevatedButton, 'Guardar'));
    await tester.pump();
    expect(ServerConfig.host, '10.0.0.7');
    expect(ServerConfig.useMock, isFalse);
    expect(find.textContaining('10.0.0.7:8000'), findsOneWidget);
  });

  testWidgets('en modo de prueba no se valida la IP y la confirmación lo dice', (tester) async {
    await montar(tester);
    await tester.tap(find.byType(Switch));
    await tester.pump();
    await tester.tap(find.widgetWithText(ElevatedButton, 'Guardar'));
    await tester.pump();
    expect(ServerConfig.useMock, isTrue);
    expect(find.textContaining('sin la PC'), findsWidgets);
  });
}
