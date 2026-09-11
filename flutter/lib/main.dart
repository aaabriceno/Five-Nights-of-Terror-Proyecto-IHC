import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'config/server_config.dart';
import 'providers/connection_provider.dart';
import 'providers/game_provider.dart';
import 'screens/menu_principal_screen.dart';
import 'utils/colors.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await ServerConfig.cargar();
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ConnectionProvider()),
        ChangeNotifierProvider(create: (_) => GameProvider()),
      ],
      child: MaterialApp(
        title: "Five Nights at Freddy's - Tablet",
        theme: _construirTema(),
        home: const MenuPrincipalScreen(),
        debugShowCheckedModeBanner: false,
      ),
    );
  }

  ThemeData _construirTema() {
    final ColorScheme esquemaDeColores = ColorScheme.fromSeed(
      seedColor: AppColors.acento,
      brightness: Brightness.dark,
      surface: AppColors.panel,
      error: AppColors.peligro,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      colorScheme: esquemaDeColores,
      scaffoldBackgroundColor: AppColors.fondo,
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.panel,
        foregroundColor: AppColors.textoPrimario,
        elevation: 0,
        centerTitle: false,
        shape: const Border(
          bottom: BorderSide(color: AppColors.panelBorde, width: 1),
        ),
      ),
      cardTheme: CardThemeData(
        color: AppColors.panel,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
          side: const BorderSide(color: AppColors.panelBorde, width: 1),
        ),
      ),
      textTheme: const TextTheme(
        titleLarge: TextStyle(
          color: AppColors.textoPrimario,
          fontWeight: FontWeight.bold,
        ),
        titleMedium: TextStyle(
          color: AppColors.textoPrimario,
          fontWeight: FontWeight.w600,
        ),
        bodyMedium: TextStyle(color: AppColors.textoPrimario),
        bodySmall: TextStyle(color: AppColors.textoSecundario),
      ),
      iconTheme: const IconThemeData(color: AppColors.acento),
      progressIndicatorTheme: const ProgressIndicatorThemeData(
        color: AppColors.acento,
        linearTrackColor: AppColors.panelBorde,
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.acento,
          foregroundColor: AppColors.fondo,
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
          ),
        ),
      ),
    );
  }
}
