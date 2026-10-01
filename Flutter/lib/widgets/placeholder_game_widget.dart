import 'package:flutter/material.dart';
import '../models/task.dart';

/// Stand-in for the real minigames (cables, dials, sequence, rhythm),
/// which are implemented in a later plan. Lets the full connect → task →
/// complete → next-task flow be exercised end-to-end today.
class PlaceholderGameWidget extends StatelessWidget {
  final Task task;
  final void Function(bool success) onComplete;

  const PlaceholderGameWidget({
    super.key,
    required this.task,
    required this.onComplete,
  });

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            'Minijuego: ${task.taskType}',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 8),
          Text(task.description),
          const SizedBox(height: 24),
          ElevatedButton(
            onPressed: () => onComplete(true),
            child: const Text('Completar (simulado)'),
          ),
          TextButton(
            onPressed: () => onComplete(false),
            child: const Text('Fallar (simulado)'),
          ),
        ],
      ),
    );
  }
}
