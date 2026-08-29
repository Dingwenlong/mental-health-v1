import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';

import '../../design/app_design.dart';
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
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        AppNotice(
          text: careCopy('care.exerciseNotice'),
          icon: Icons.self_improvement_outlined,
        ),
        const SizedBox(height: 18),
        ...data.$1.map(
          (exercise) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppPanel(
              padding: EdgeInsets.zero,
              child: InkWell(
                borderRadius: BorderRadius.circular(12),
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
                child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        alignment: Alignment.center,
                        decoration: BoxDecoration(
                          color: AppPalette.mint,
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: const Icon(
                          Icons.spa_outlined,
                          color: AppPalette.pine,
                        ),
                      ),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              exercise['title'] as String,
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                            const SizedBox(height: 4),
                            Text(
                              '${exercise['durationSeconds']} ${careCopy('care.seconds')}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ],
                        ),
                      ),
                      const Icon(Icons.arrow_forward_rounded),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
        AppNotice(
          text: careCopy('care.exerciseSource'),
          icon: Icons.menu_book_outlined,
        ),
        const SizedBox(height: 22),
        AppSectionHeader(title: careCopy('care.exerciseHistory')),
        if ((data.$2['total'] as num) == 0)
          AppStatePanel(message: careCopy('care.empty')),
        ...careObjects(data.$2['items']).map(
          (item) => AppPanel(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(
                Icons.check_circle_outline_rounded,
                color: AppPalette.pine,
              ),
              title: Text(
                data.$1.firstWhere(
                      (exercise) => exercise['id'] == item['exerciseId'],
                    )['title']
                    as String,
              ),
              subtitle: Text(
                '${careCopy('care.completedAt')} · ${careTime(item['completedAt'])}',
              ),
            ),
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
      if (_remaining <= 0) {
        timer.cancel();
        setState(() => _remaining = 0);
      }
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
  Widget build(BuildContext context) {
    final duration = (widget.exercise['durationSeconds'] as num).toInt();
    final progress = duration == 0 ? 1.0 : 1 - (_remaining / duration);
    return Scaffold(
      appBar: AppBar(title: Text(widget.exercise['title'] as String)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(22, 8, 22, 36),
        children: [
          AppPanel(
            color: const Color(0xFFF0F6F3),
            borderColor: AppPalette.mint,
            child: Text(
              widget.exercise['instruction'] as String,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
          ),
          const SizedBox(height: 14),
          AppNotice(text: careCopy('care.exerciseNotice')),
          const SizedBox(height: 30),
          Center(
            child: SizedBox(
              width: 190,
              height: 190,
              child: Stack(
                alignment: Alignment.center,
                children: [
                  SizedBox.expand(
                    child: CircularProgressIndicator(
                      value: progress.clamp(0, 1),
                      strokeWidth: 8,
                      color: AppPalette.pine,
                      backgroundColor: AppPalette.mint,
                    ),
                  ),
                  Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        '$_remaining',
                        style: Theme.of(context).textTheme.headlineLarge,
                      ),
                      Text(
                        careCopy('care.seconds'),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 30),
          if (!_started)
            FilledButton.icon(
              onPressed: _start,
              icon: const Icon(Icons.play_arrow_rounded),
              label: Text(careCopy('care.startExercise')),
            ),
          if (_started)
            FilledButton.icon(
              onPressed: _remaining == 0 && !_busy ? _complete : null,
              icon: const Icon(Icons.check_rounded),
              label: Text(careCopy('care.finishExercise')),
            ),
          const SizedBox(height: 8),
          TextButton.icon(
            onPressed: _busy ? null : () => Navigator.pop(context, false),
            icon: const Icon(Icons.stop_circle_outlined),
            label: Text(careCopy('care.stopExercise')),
          ),
        ],
      ),
    );
  }
}
