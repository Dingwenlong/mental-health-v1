import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';

import 'care_repository.dart';
import 'care_widgets.dart';

class ExercisesPage extends StatefulWidget {
  const ExercisesPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<ExercisesPage> createState() => _ExercisesPageState();
}

class _ExercisesPageState extends State<ExercisesPage> {
  int _page = 1;
  @override
  Widget build(
    BuildContext context,
  ) => CarePageFrame<(List<CareJson>, CareJson)>(
    key: ValueKey(_page),
    title: careCopy('care.exercises'),
    load: () async => (
      await widget.repository.exercises(),
      await widget.repository.completions(_page),
    ),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        careNotice('care.exerciseNotice'),
        ...data.$1.map(
          (exercise) => Card(
            child: ListTile(
              title: Text(exercise['title'] as String),
              subtitle: Text(
                '${exercise['durationSeconds']} ${careCopy('care.remaining')}',
              ),
              trailing: const Icon(Icons.chevron_right),
              onTap: () async {
                await Navigator.of(context).push(
                  MaterialPageRoute<bool>(
                    builder: (_) => ExercisePracticePage(
                      repository: widget.repository,
                      exercise: exercise,
                    ),
                  ),
                );
                reload();
              },
            ),
          ),
        ),
        careNotice('care.exerciseSource'),
        Text(
          careCopy('care.exerciseHistory'),
          style: Theme.of(context).textTheme.titleLarge,
        ),
        if ((data.$2['total'] as num) == 0) careNotice('care.empty'),
        ...careObjects(data.$2['items']).map(
          (item) => ListTile(
            title: Text(
              data.$1.firstWhere(
                    (exercise) => exercise['id'] == item['exerciseId'],
                  )['title']
                  as String,
            ),
            subtitle: Text(careTime(item['completedAt'])),
          ),
        ),
        CarePagination(
          page: _page,
          total: (data.$2['total'] as num).toInt(),
          onChanged: (page) => setState(() => _page = page),
        ),
      ],
    ),
  );
}

class ExercisePracticePage extends StatefulWidget {
  const ExercisePracticePage({
    required this.repository,
    required this.exercise,
    super.key,
  });
  final CareRepository repository;
  final CareJson exercise;
  @override
  State<ExercisePracticePage> createState() => _ExercisePracticePageState();
}

class _ExercisePracticePageState extends State<ExercisePracticePage> {
  Timer? _timer;
  late int _remaining;
  bool _started = false, _busy = false;
  late final String _id;
  @override
  void initState() {
    super.initState();
    _remaining = (widget.exercise['durationSeconds'] as num).toInt();
    final random = Random.secure();
    final bytes = List.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    _id =
        '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  void _start() {
    setState(() => _started = true);
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) {
        timer.cancel();
        return;
      }
      setState(() => _remaining--);
      if (_remaining <= 0) timer.cancel();
    });
  }

  Future<void> _complete() async {
    if (_busy || _remaining > 0) return;
    setState(() => _busy = true);
    try {
      await widget.repository.completeExercise(
        _id,
        widget.exercise['id'] as String,
      );
      if (mounted) Navigator.pop(context, true);
    } catch (error) {
      if (mounted) careError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(widget.exercise['title'] as String)),
    body: ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(
          widget.exercise['instruction'] as String,
          style: Theme.of(context).textTheme.titleMedium,
        ),
        careNotice('care.exerciseNotice'),
        const SizedBox(height: 24),
        Text(
          '${careCopy('care.remaining')}: $_remaining',
          style: Theme.of(context).textTheme.headlineMedium,
        ),
        const SizedBox(height: 24),
        if (!_started)
          FilledButton(
            onPressed: _start,
            child: Text(careCopy('care.startExercise')),
          ),
        if (_started)
          FilledButton(
            onPressed: _remaining == 0 && !_busy ? _complete : null,
            child: Text(careCopy('care.finishExercise')),
          ),
        TextButton(
          onPressed: _busy ? null : () => Navigator.pop(context, false),
          child: Text(careCopy('care.stopExercise')),
        ),
      ],
    ),
  );
}
