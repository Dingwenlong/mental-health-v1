import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import 'care_repository.dart';
import 'care_widgets.dart';

Future<void> openTodayEntry(
  BuildContext context,
  CareRepository repository,
) async {
  try {
    final entry = await repository.todayEntry();
    if (!context.mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute<bool>(
        builder: (_) => CheckInEditorPage(repository: repository, entry: entry),
      ),
    );
  } catch (error) {
    if (context.mounted) careError(context, error);
  }
}

class CheckInEditorPage extends StatefulWidget {
  const CheckInEditorPage({required this.repository, this.entry, super.key});
  final CareRepository repository;
  final CareJson? entry;
  @override
  State<CheckInEditorPage> createState() => _CheckInEditorPageState();
}

class _CheckInEditorPageState extends State<CheckInEditorPage> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _date, _sleep, _note;
  int _mood = 3;
  bool _busy = false;
  String? _error;
  @override
  void initState() {
    super.initState();
    _date = TextEditingController(
      text: widget.entry?['date'] as String? ?? careToday(),
    );
    _sleep = TextEditingController(
      text: widget.entry?['sleepHours']?.toString() ?? '',
    );
    _note = TextEditingController(text: widget.entry?['note'] as String? ?? '');
    _mood = (widget.entry?['mood'] as num?)?.toInt() ?? 3;
  }

  @override
  void dispose() {
    _date.dispose();
    _sleep.dispose();
    _note.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.repository.saveEntry(_date.text, {
        'mood': _mood,
        'sleepHours': double.parse(_sleep.text),
        'note': _note.text.trim().isEmpty ? null : _note.text.trim(),
        'version': widget.entry?['version'],
      });
      if (mounted) Navigator.of(context).pop(true);
    } catch (error) {
      if (mounted) {
        setState(
          () => _error = careCopy(
            error is ApiFailure && error.status == 409
                ? 'care.conflict'
                : 'care.invalid',
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(careCopy('care.checkIn'))),
    body: Form(
      key: _form,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          TextFormField(
            controller: _date,
            readOnly: widget.entry != null,
            enabled: !_busy,
            decoration: InputDecoration(
              labelText: careCopy('care.date'),
              hintText: 'YYYY-MM-DD',
            ),
            validator: (value) =>
                DateTime.tryParse(value ?? '') == null ||
                    (value ?? '').compareTo(careToday()) > 0
                ? careCopy('care.invalid')
                : null,
          ),
          const SizedBox(height: 20),
          Text(careCopy('care.mood')),
          Wrap(
            spacing: 8,
            children: List.generate(
              5,
              (index) => ChoiceChip(
                label: Text(careCopy('care.mood.${index + 1}')),
                selected: _mood == index + 1,
                onSelected: _busy
                    ? null
                    : (_) => setState(() => _mood = index + 1),
              ),
            ),
          ),
          const SizedBox(height: 20),
          TextFormField(
            controller: _sleep,
            key: const Key('sleep-hours'),
            enabled: !_busy,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: InputDecoration(labelText: careCopy('care.sleep')),
            validator: (value) {
              final hours = double.tryParse(value ?? '');
              return hours == null || !hours.isFinite || hours < 0 || hours > 24
                  ? careCopy('care.invalid')
                  : null;
            },
          ),
          const SizedBox(height: 20),
          TextFormField(
            controller: _note,
            key: const Key('daily-note'),
            enabled: !_busy,
            maxLength: 500,
            minLines: 3,
            maxLines: 5,
            decoration: InputDecoration(
              labelText: careCopy('care.note'),
              helperText: careCopy('care.noteHint'),
              helperMaxLines: 3,
            ),
          ),
          if (_error != null)
            Text(
              _error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _busy ? null : _save,
            child: Text(careCopy('care.save')),
          ),
        ],
      ),
    ),
  );
}

class CheckInListPage extends StatefulWidget {
  const CheckInListPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<CheckInListPage> createState() => _CheckInListPageState();
}

class _CheckInListPageState extends State<CheckInListPage> {
  int _page = 1;
  @override
  Widget build(BuildContext context) => CarePageFrame<CareJson>(
    key: ValueKey(_page),
    title: careCopy('care.checkIn'),
    load: () => widget.repository.entries(_page),
    builder: (data, reload) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        FilledButton.icon(
          onPressed: () async {
            await openTodayEntry(context, widget.repository);
            reload();
          },
          icon: const Icon(Icons.add),
          label: Text(careCopy('care.recordToday')),
        ),
        if ((data['total'] as num) == 0) careNotice('care.empty'),
        ...careObjects(data['items']).map(
          (entry) => Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    entry['date'] as String,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  Text(
                    '${careCopy('care.mood.${entry['mood']}')} · ${entry['sleepHours']} ${careCopy('care.sleepShort')}',
                  ),
                  if (entry['note'] != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Text(entry['note'] as String),
                    ),
                  Row(
                    children: [
                      TextButton(
                        onPressed: () async {
                          await Navigator.of(context).push(
                            MaterialPageRoute<bool>(
                              builder: (_) => CheckInEditorPage(
                                repository: widget.repository,
                                entry: entry,
                              ),
                            ),
                          );
                          reload();
                        },
                        child: Text(careCopy('care.edit')),
                      ),
                      TextButton(
                        onPressed: () async {
                          final confirmed = await showDialog<bool>(
                            context: context,
                            builder: (dialog) => AlertDialog(
                              title: Text(careCopy('care.confirmDelete')),
                              actions: [
                                TextButton(
                                  onPressed: () => Navigator.pop(dialog, false),
                                  child: Text(careCopy('care.cancel')),
                                ),
                                FilledButton(
                                  onPressed: () => Navigator.pop(dialog, true),
                                  child: Text(careCopy('care.delete')),
                                ),
                              ],
                            ),
                          );
                          if (confirmed != true) return;
                          try {
                            await widget.repository.deleteEntry(
                              entry['date'] as String,
                            );
                            reload();
                          } catch (error) {
                            if (context.mounted) careError(context, error);
                          }
                        },
                        child: Text(careCopy('care.delete')),
                      ),
                    ],
                  ),
                ],
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

class TrendsPage extends StatefulWidget {
  const TrendsPage({required this.repository, super.key});
  final CareRepository repository;
  @override
  State<TrendsPage> createState() => _TrendsPageState();
}

class _TrendsPageState extends State<TrendsPage> {
  int _days = 7;
  @override
  Widget build(BuildContext context) => CarePageFrame<List<CareJson>>(
    key: ValueKey(_days),
    title: careCopy('care.trends'),
    load: () => widget.repository.trends(_days),
    builder: (days, _) => ListView(
      padding: const EdgeInsets.all(20),
      children: [
        SegmentedButton<int>(
          segments: [
            ButtonSegment(value: 7, label: Text(careCopy('care.days7'))),
            ButtonSegment(value: 30, label: Text(careCopy('care.days30'))),
          ],
          selected: {_days},
          onSelectionChanged: (values) => setState(() => _days = values.first),
        ),
        careNotice('care.trendNotice'),
        SizedBox(
          height: 150,
          child: CustomPaint(
            painter: _MoodPainter(days, Theme.of(context).colorScheme.primary),
            child: const SizedBox.expand(),
          ),
        ),
        ...days.map(
          (day) => ListTile(
            contentPadding: EdgeInsets.zero,
            title: Text(day['date'] as String),
            subtitle: Text(
              '${careCopy('care.sleepShort')}: ${day['sleepHours'] ?? careCopy('care.noValue')} · ${careCopy('care.exerciseCount')}: ${day['exerciseCount']}',
            ),
            trailing: Text(
              day['mood'] == null
                  ? careCopy('care.noValue')
                  : careCopy('care.mood.${day['mood']}'),
            ),
          ),
        ),
      ],
    ),
  );
}

class _MoodPainter extends CustomPainter {
  _MoodPainter(this.days, this.color);
  final List<CareJson> days;
  final Color color;
  @override
  void paint(Canvas canvas, Size size) {
    if (days.isEmpty) return;
    final paint = Paint()
      ..color = color
      ..strokeWidth = 2;
    Offset? previous;
    for (var i = 0; i < days.length; i++) {
      final mood = days[i]['mood'] as num?;
      if (mood == null) {
        previous = null;
        continue;
      }
      final point = Offset(
        8 + i * (size.width - 16) / (days.length > 1 ? days.length - 1 : 1),
        12 + (5 - mood.toDouble()) * (size.height - 24) / 4,
      );
      if (previous != null) canvas.drawLine(previous, point, paint);
      canvas.drawCircle(point, 4, paint);
      previous = point;
    }
  }

  @override
  bool shouldRepaint(covariant _MoodPainter oldDelegate) =>
      oldDelegate.days != days || oldDelegate.color != color;
}
