import 'dart:async';

import 'package:flutter/material.dart';
import '../models/task.dart';
import '../utils/colors.dart';

/// Muestra la lista de tareas pendientes de la noche como tarjetas
/// tocables. Al tocar una, llama `alElegirTarea` con esa tarea — la
/// pantalla que use este widget decide qué hacer (normalmente,
/// GameProvider.elegirTarea).
///
/// Cada tarjeta cambia el color de su borde según cuánto tiempo lleva
/// pendiente (calculado con `Task.createdAt`, sin depender de ningún dato
/// nuevo del servidor): normal -> ámbar -> rojo. Es deliberadamente
/// ambiguo — no muestra segundos ni ningún número — porque el tiempo sin
/// resolver una tarea sube la probabilidad de ataque del animatrónico
/// dueño en el backend real, y esa probabilidad es mecánica interna que
/// no debe revelarse al jugador (misma decisión que ocultar el riesgo
/// global, ver PROGRESS.md 2026-09-11).
class MenuDeTareas extends StatefulWidget {
  final List<Task> tareas;
  final void Function(Task tarea) alElegirTarea;
  final bool wifiActivo;

  const MenuDeTareas({
    super.key,
    required this.tareas,
    required this.alElegirTarea,
    required this.wifiActivo,
  });

  @override
  State<MenuDeTareas> createState() => _MenuDeTareasState();
}

class _MenuDeTareasState extends State<MenuDeTareas> {
  static const Duration _umbralAmbar = Duration(seconds: 20);
  static const Duration _umbralRojo = Duration(seconds: 40);

  Timer? _temporizadorRepintado;

  @override
  void initState() {
    super.initState();
    // Solo fuerza un repintado periódico para que el color de urgencia
    // avance con el tiempo — no consulta al servidor ni cambia estado.
    _temporizadorRepintado = Timer.periodic(
      const Duration(seconds: 2),
      (_) => setState(() {}),
    );
  }

  @override
  void dispose() {
    _temporizadorRepintado?.cancel();
    super.dispose();
  }

  Color _colorDeUrgencia(Task tarea) {
    final Duration antiguedad = DateTime.now().difference(tarea.createdAt);
    if (antiguedad >= _umbralRojo) return AppColors.peligro;
    if (antiguedad >= _umbralAmbar) return AppColors.advertencia;
    return AppColors.panelBorde;
  }

  String _nombreLegible(String taskType) {
    switch (taskType) {
      case 'cables':
        return 'Conectar Cables';
      case 'dials':
        return 'Girar Perillas';
      case 'sequence':
        return 'Resolver Secuencia';
      case 'rhythm':
        return 'Ritmo Crítico';
      case 'wifi':
        return 'Reiniciar WiFi';
      case 'ventiladores':
        return 'Encender Ventiladores';
      case 'temperatura':
        return 'Reparar Temperatura';
      case 'procesar_datos':
        return 'Procesar Datos';
      case 'subir_datos':
        return 'Subir Datos';
      case 'trazar_curso':
        return 'Trazar Curso';
      default:
        return taskType;
    }
  }

  // Ícono provisional por tipo de tarea — se reemplazará por una imagen
  // real cuando estén listos los assets del minijuego correspondiente.
  IconData _iconoPorTipo(String taskType) {
    switch (taskType) {
      case 'cables':
        return Icons.cable;
      case 'dials':
        return Icons.tune;
      case 'sequence':
        return Icons.format_list_numbered;
      case 'rhythm':
        return Icons.graphic_eq;
      case 'wifi':
        return Icons.wifi;
      case 'ventiladores':
        return Icons.air;
      case 'temperatura':
        return Icons.thermostat;
      case 'procesar_datos':
        return Icons.data_usage;
      case 'subir_datos':
        return Icons.cloud_upload;
      case 'trazar_curso':
        return Icons.timeline;
      default:
        return Icons.help_outline;
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.tareas.isEmpty) {
      return Center(
        child: Text(
          'Sin tareas pendientes — vigila la pantalla',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      );
    }

    return Column(
      children: [
        Expanded(
          child: GridView.builder(
            gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
              crossAxisCount: 2,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 3.4,
            ),
            itemCount: widget.tareas.length,
            itemBuilder: (context, indice) {
              return _buildTarjeta(context, widget.tareas[indice]);
            },
          ),
        ),
        if (widget.tareas.length > 4) ...[
          const SizedBox(height: 8),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(
                Icons.keyboard_double_arrow_down,
                size: 16,
                color: AppColors.textoSecundario,
              ),
              const SizedBox(width: 6),
              Text(
                'Desliza para ver más tareas',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        ],
      ],
    );
  }

  Widget _buildTarjeta(BuildContext context, Task tarea) {
    final bool bloqueadaPorWifi =
        tarea.taskType == 'subir_datos' && !widget.wifiActivo;
    return Opacity(
      opacity: bloqueadaPorWifi ? 0.4 : 1.0,
      child: Container(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(12),
          border: Border.all(color: _colorDeUrgencia(tarea), width: 2),
        ),
        child: Card(
          margin: EdgeInsets.zero,
          child: InkWell(
            borderRadius: BorderRadius.circular(12),
            onTap: bloqueadaPorWifi ? null : () => widget.alElegirTarea(tarea),
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: AppColors.acento.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Icon(
                      _iconoPorTipo(tarea.taskType),
                      color: AppColors.acento,
                      size: 22,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _nombreLegible(tarea.taskType),
                          style: Theme.of(context).textTheme.titleMedium,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          bloqueadaPorWifi
                              ? 'Requiere WiFi activo'
                              : '${tarea.duration}s',
                          style: Theme.of(context).textTheme.bodySmall,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
