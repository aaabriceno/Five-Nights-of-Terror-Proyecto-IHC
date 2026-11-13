import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:fn_attention_defense_tablet/config/server_config.dart';
import 'package:fn_attention_defense_tablet/providers/connection_provider.dart';
import 'package:fn_attention_defense_tablet/providers/game_provider.dart';
import 'package:fn_attention_defense_tablet/screens/configurar_servidor_screen.dart';
import 'package:fn_attention_defense_tablet/screens/menu_principal_screen.dart';
import 'package:provider/provider.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// El menú tiene que decir con qué PC va a jugar la tablet (o que está en modo
/// de prueba, sin PC): el error más común al probar era tocar «Nuevo juego» con
/// el modo de prueba encendido y no entender por qué la PC no hacía nada.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  setUp(() {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    ServerConfig.host = '192.168.1.100';
    ServerConfig.port = 8000;
  });

  tearDown(() {
    ServerConfig.useMock = true;
  });

  Future<void> montar(WidgetTester tester) async {
    tester.view.physicalSize = const Size(2000, 1200);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ConnectionProvider()),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: const MaterialApp(home: MenuPrincipalScreen(musica: false)),
    ));
    await tester.pump();
  }

  testWidgets('en modo de prueba lo dice en el menú', (tester) async {
    ServerConfig.useMock = true;
    await montar(tester);
    expect(find.textContaining('Modo de prueba'), findsOneWidget);
    expect(find.textContaining('192.168.1.100'), findsNothing);
  });

  testWidgets('con la PC configurada muestra a qué IP va a conectarse', (tester) async {
    ServerConfig.useMock = false;
    await montar(tester);
    expect(find.text('PC: 192.168.1.100:8000'), findsOneWidget);
    expect(find.textContaining('Modo de prueba'), findsNothing);
  });

  testWidgets('el indicador se lee (≥ 20 px) y no tapa las opciones del menú (queda a la derecha)', (tester) async {
    ServerConfig.useMock = false;
    await montar(tester);
    final Finder texto = find.text('PC: 192.168.1.100:8000');
    expect(tester.widget<Text>(texto).style?.fontSize, greaterThanOrEqualTo(20));
    expect(tester.getTopLeft(texto).dx, greaterThan(1000), reason: 'las opciones están dibujadas a la izquierda');
  });

  testWidgets('tocar el indicador abre Opciones y al volver se actualiza', (tester) async {
    ServerConfig.useMock = true;
    await montar(tester);
    await tester.tap(find.textContaining('Modo de prueba'));
    await tester.pumpAndSettle();
    expect(find.byType(ConfigurarServidorScreen), findsOneWidget);
    await tester.tap(find.byType(Switch));
    await tester.pump();
    await tester.tap(find.widgetWithText(ElevatedButton, 'Guardar'));
    await tester.pumpAndSettle();
    expect(find.byType(MenuPrincipalScreen), findsOneWidget);
    expect(find.text('PC: 192.168.1.100:8000'), findsOneWidget);
  });
}
