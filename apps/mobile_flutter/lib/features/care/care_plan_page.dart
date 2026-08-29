import 'package:flutter/material.dart';

import '../../design/app_design.dart';
import 'care_repository.dart';
import 'care_widgets.dart';
import 'check_in_pages.dart';
import 'exercise_pages.dart';

class CarePlanListPage extends StatefulWidget {
  const CarePlanListPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<CarePlanListPage> createState() => _CarePlanListPageState();
}

class _CarePlanListPageState extends State<CarePlanListPage> {
  int _page = 1;
  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    key: ValueKey(_page),
    title: careCopy('care.plans'),
    load: () => widget.repository.plans(_page),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        AppNotice(
          text: careCopy('care.followUpSeparate'),
          icon: Icons.info_outline_rounded,
        ),
        const SizedBox(height: 18),
        if ((data['total'] as num) == 0)
          AppStatePanel(message: careCopy('care.noPlans')),
        ...careObjects(data['items']).map(
          (plan) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppPanel(
              padding: EdgeInsets.zero,
              child: ListTile(
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: 18,
                  vertical: 8,
                ),
                leading: const Icon(
                  Icons.route_outlined,
                  color: AppPalette.pine,
                ),
                title: Text(plan['title'] as String),
                subtitle: Padding(
                  padding: const EdgeInsets.only(top: 6),
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: AppStatusBadge(
                      label: careCopy('care.status.${plan['status']}'),
                      tone: statusTone(plan['status'] as String),
                    ),
                  ),
                ),
                trailing: const Icon(Icons.chevron_right_rounded),
                onTap: () async {
                  await Navigator.push(
                    context,
                    MaterialPageRoute<void>(
                      builder: (_) => CarePlanPage(
                        repository: widget.repository,
                        planId: plan['id'] as String,
                      ),
                    ),
                  );
                  reload();
                },
              ),
            ),
          ),
        ),
        CarePagination(
          page: _page,
          total: (data['total'] as num).toInt(),
          onChanged: (page) => setState(() => _page = page),
        ),
      ],
    ),
  );
}

class CarePlanPage extends StatelessWidget {
  const CarePlanPage({
    required this.repository,
    required this.planId,
    super.key,
  });
  final CareRepository repository;
  final String planId;
  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    title: careCopy('care.plans'),
    load: () => repository.plan(planId),
    builder: (plan, reload) => ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(
                plan['title'] as String,
                style: Theme.of(context).textTheme.headlineMedium,
              ),
            ),
            AppStatusBadge(
              label: careCopy('care.status.${plan['status']}'),
              tone: statusTone(plan['status'] as String),
            ),
          ],
        ),
        const SizedBox(height: 14),
        AppNotice(
          text: careCopy('care.feedbackNotice'),
          icon: Icons.privacy_tip_outlined,
        ),
        const SizedBox(height: 22),
        AppSectionHeader(title: careCopy('care.planTasks')),
        ...careObjects(plan['tasks']).map(
          (task) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppPanel(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Text(
                          careCopy('care.task.${task['kind']}'),
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      AppStatusBadge(
                        label: careCopy('care.status.${task['status']}'),
                        tone: statusTone(task['status'] as String),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      const Icon(
                        Icons.calendar_today_outlined,
                        size: 17,
                        color: AppPalette.muted,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        task['dueDate'] as String,
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ),
                  if (task['exerciseId'] != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(
                        careCopy(switch (task['exerciseId']) {
                          'grounding' => 'care.exercise.grounding',
                          'pause' => 'care.exercise.pause',
                          _ => 'care.exercise.smallStep',
                        }),
                      ),
                    ),
                  if (task['feedback'] != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: AppNotice(
                        text: task['feedback'] as String,
                        icon: Icons.chat_bubble_outline_rounded,
                      ),
                    ),
                  if (task['status'] == 'Pending' && plan['status'] == 'Active')
                    Padding(
                      padding: const EdgeInsets.only(top: 14),
                      child: Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          OutlinedButton.icon(
                            onPressed: () async {
                              await _start(context, task);
                              reload();
                            },
                            icon: const Icon(Icons.play_arrow_rounded),
                            label: Text(careCopy('care.doTask')),
                          ),
                          FilledButton.icon(
                            onPressed: () async {
                              await _respond(context, task, 'Done');
                              reload();
                            },
                            icon: const Icon(Icons.check_rounded),
                            label: Text(careCopy('care.done')),
                          ),
                          TextButton(
                            onPressed: () async {
                              await _respond(context, task, 'Skipped');
                              reload();
                            },
                            child: Text(careCopy('care.skip')),
                          ),
                        ],
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
      ],
    ),
  );
  Future<void> _start(BuildContext context, CareJson task) async {
    if (task['kind'] == 'CheckIn') {
      await openTodayEntry(context, repository);
      return;
    }
    try {
      final exercises = await repository.exercises();
      final exercise = exercises.firstWhere(
        (item) => item['id'] == task['exerciseId'],
      );
      if (!context.mounted) return;
      await Navigator.of(context).push(
        MaterialPageRoute<bool>(
          builder: (_) =>
              ExercisePracticePage(repository: repository, exercise: exercise),
        ),
      );
    } catch (error) {
      if (context.mounted) careError(context, error);
    }
  }

  Future<void> _respond(BuildContext context, CareJson task, String status) =>
      Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => _FeedbackPage(
            repository: repository,
            planId: planId,
            taskId: task['id'] as String,
            status: status,
          ),
        ),
      );
}

class _FeedbackPage extends StatefulWidget {
  const _FeedbackPage({
    required this.repository,
    required this.planId,
    required this.taskId,
    required this.status,
  });
  final CareRepository repository;
  final String planId, taskId, status;
  @override
  State<_FeedbackPage> createState() => _FeedbackPageState();
}

class _FeedbackPageState extends State<_FeedbackPage> {
  final _feedback = TextEditingController();
  bool _acknowledged = false, _busy = false;
  @override
  void dispose() {
    _feedback.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_acknowledged || _feedback.text.length > 500) return;
    setState(() => _busy = true);
    try {
      await widget.repository.feedback(
        widget.planId,
        widget.taskId,
        widget.status,
        _feedback.text.trim().isEmpty ? null : _feedback.text.trim(),
      );
      if (mounted) Navigator.pop(context);
    } catch (error) {
      if (mounted) careError(context, error);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(careCopy('care.feedback'))),
    body: ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        AppNotice(
          text: careCopy('care.feedbackNotice'),
          icon: Icons.privacy_tip_outlined,
        ),
        const SizedBox(height: 18),
        TextFormField(
          controller: _feedback,
          maxLength: 500,
          minLines: 3,
          maxLines: 6,
          enabled: !_busy,
          decoration: InputDecoration(labelText: careCopy('care.feedback')),
        ),
        CheckboxListTile(
          contentPadding: const EdgeInsets.symmetric(vertical: 6),
          value: _acknowledged,
          title: Text(careCopy('care.feedbackConfirm')),
          onChanged: _busy
              ? null
              : (value) => setState(() => _acknowledged = value ?? false),
        ),
        FilledButton(
          onPressed: _acknowledged && !_busy ? _save : null,
          child: Text(careCopy('care.save')),
        ),
      ],
    ),
  );
}
