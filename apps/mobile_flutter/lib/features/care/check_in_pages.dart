import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../design/app_design.dart';
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

  void _adjustSleep(double delta) {
    final current = double.tryParse(_sleep.text) ?? 0;
    final next = (current + delta).clamp(0, 24).toDouble();
    setState(() {
      _sleep.text = next == next.roundToDouble()
          ? next.toStringAsFixed(0)
          : next.toStringAsFixed(1);
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(careCopy('care.checkIn'))),
    body: Form(
      key: _form,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(20, 8, 20, 36),
        children: [
          AppEyebrow(careCopy('care.todayEyebrow')),
          const SizedBox(height: 8),
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
          const SizedBox(height: 24),
          Text(
            careCopy('care.mood'),
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: List.generate(
              5,
              (index) => _MoodChoice(
                value: index + 1,
                selected: _mood == index + 1,
                enabled: !_busy,
                onSelected: _busy
                    ? null
                    : (_) => setState(() => _mood = index + 1),
              ),
            ),
          ),
          const SizedBox(height: 20),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              IconButton.outlined(
                tooltip: careCopy('care.sleepDecrease'),
                onPressed: _busy ? null : () => _adjustSleep(-0.5),
                icon: const Icon(Icons.remove_rounded),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: TextFormField(
                  controller: _sleep,
                  key: const Key('sleep-hours'),
                  enabled: !_busy,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: InputDecoration(
                    labelText: careCopy('care.sleep'),
                    suffixText: careCopy('care.sleepShort'),
                  ),
                  validator: (value) {
                    final hours = double.tryParse(value ?? '');
                    return hours == null ||
                            !hours.isFinite ||
                            hours < 0 ||
                            hours > 24 ||
                            (hours * 10).roundToDouble() != hours * 10
                        ? careCopy('care.invalid')
                        : null;
                  },
                ),
              ),
              const SizedBox(width: 10),
              IconButton.outlined(
                tooltip: careCopy('care.sleepIncrease'),
                onPressed: _busy ? null : () => _adjustSleep(0.5),
                icon: const Icon(Icons.add_rounded),
              ),
            ],
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

class _MoodChoice extends StatelessWidget {
  const _MoodChoice({
    required this.value,
    required this.selected,
    required this.enabled,
    required this.onSelected,
  });

  final int value;
  final bool selected;
  final bool enabled;
  final ValueChanged<bool>? onSelected;

  static const _icons = [
    Icons.sentiment_very_dissatisfied_outlined,
    Icons.sentiment_dissatisfied_outlined,
    Icons.sentiment_neutral_outlined,
    Icons.sentiment_satisfied_outlined,
    Icons.sentiment_very_satisfied_outlined,
  ];

  @override
  Widget build(BuildContext context) {
    final label = careCopy('care.mood.$value');
    return Semantics(
      button: true,
      selected: selected,
      label: '${careCopy('care.moodChoice')}$label',
      child: ChoiceChip(
        avatar: Icon(
          _icons[value - 1],
          size: 20,
          color: selected ? AppPalette.pine : AppPalette.muted,
        ),
        label: Text(label),
        selected: selected,
        onSelected: enabled ? onSelected : null,
        showCheckmark: false,
      ),
    );
  }
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
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        FilledButton.icon(
          onPressed: () async {
            await openTodayEntry(context, widget.repository);
            reload();
          },
          icon: const Icon(Icons.add),
          label: Text(careCopy('care.recordToday')),
        ),
        if ((data['total'] as num) == 0)
          AppStatePanel(message: careCopy('care.empty')),
        ...careObjects(data['items']).map(
          (entry) => Padding(
            padding: const EdgeInsets.only(top: 12),
            child: AppPanel(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          entry['date'] as String,
                          style: Theme.of(context).textTheme.titleMedium,
                        ),
                      ),
                      AppStatusBadge(
                        label: careCopy('care.mood.${entry['mood']}'),
                        tone: AppTone.info,
                      ),
                    ],
                  ),
                  Text('${entry['sleepHours']} ${careCopy('care.sleepShort')}'),
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
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        SegmentedButton<int>(
          segments: [
            ButtonSegment(value: 7, label: Text(careCopy('care.days7'))),
            ButtonSegment(value: 30, label: Text(careCopy('care.days30'))),
          ],
          selected: {_days},
          onSelectionChanged: (values) => setState(() => _days = values.first),
        ),
        const SizedBox(height: 12),
        AppNotice(text: careCopy('care.trendNotice')),
        const SizedBox(height: 18),
        AppSectionHeader(title: careCopy('care.date')),
        _TrendDateStrip(days: days),
        const SizedBox(height: 18),
        _TrendPanel(
          title: careCopy('care.moodTrend'),
          days: days,
          valueKey: 'mood',
          maxValue: 5,
        ),
        const SizedBox(height: 14),
        _TrendPanel(
          title: careCopy('care.sleepTrend'),
          days: days,
          valueKey: 'sleepHours',
          maxValue: 24,
        ),
        const SizedBox(height: 24),
        AppSectionHeader(title: careCopy('care.exerciseTrend')),
        AppPanel(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
          child: Column(
            children: days
                .map(
                  (day) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(day['date'] as String),
                    subtitle: Text(
                      '${careCopy('care.sleepShort')}: ${day['sleepHours'] ?? careCopy('care.noValue')}',
                    ),
                    trailing: Text(
                      '${careCopy('care.exerciseCount')}: ${day['exerciseCount']}',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                )
                .toList(),
          ),
        ),
      ],
    ),
  );
}

class _TrendDateStrip extends StatelessWidget {
  const _TrendDateStrip({required this.days});

  final List<CareJson> days;

  @override
  Widget build(BuildContext context) => AppPanel(
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
    child: SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: days
            .map(
              (day) => SizedBox(
                width: 102,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      day['date'] as String,
                      style: Theme.of(context).textTheme.labelMedium,
                    ),
                    const SizedBox(height: 6),
                    Text(
                      day['mood'] == null
                          ? careCopy('care.noValue')
                          : careCopy('care.mood.${day['mood']}'),
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: day['mood'] == null
                            ? AppPalette.muted
                            : AppPalette.pine,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ],
                ),
              ),
            )
            .toList(),
      ),
    ),
  );
}

class _TrendPanel extends StatelessWidget {
  const _TrendPanel({
    required this.title,
    required this.days,
    required this.valueKey,
    required this.maxValue,
  });

  final String title;
  final List<CareJson> days;
  final String valueKey;
  final double maxValue;

  @override
  Widget build(BuildContext context) => Semantics(
    label: title,
    image: true,
    child: AppPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 14),
          SizedBox(
            height: 140,
            child: CustomPaint(
              painter: _TrendPainter(
                days: days,
                valueKey: valueKey,
                maxValue: maxValue,
              ),
              child: const SizedBox.expand(),
            ),
          ),
        ],
      ),
    ),
  );
}

class _TrendPainter extends CustomPainter {
  _TrendPainter({
    required this.days,
    required this.valueKey,
    required this.maxValue,
  });

  final List<CareJson> days;
  final String valueKey;
  final double maxValue;

  @override
  void paint(Canvas canvas, Size size) {
    if (days.isEmpty) return;
    final grid = Paint()
      ..color = AppPalette.line
      ..strokeWidth = 1;
    final paint = Paint()
      ..color = AppPalette.pine
      ..strokeWidth = 2.4
      ..strokeCap = StrokeCap.round;
    for (var row = 0; row < 3; row++) {
      final y = 8 + row * (size.height - 16) / 2;
      canvas.drawLine(Offset(0, y), Offset(size.width, y), grid);
    }
    Offset? previous;
    for (var i = 0; i < days.length; i++) {
      final value = days[i][valueKey] as num?;
      if (value == null) {
        previous = null;
        continue;
      }
      final point = Offset(
        8 + i * (size.width - 16) / (days.length > 1 ? days.length - 1 : 1),
        8 +
            (maxValue - value.toDouble().clamp(0, maxValue)) *
                (size.height - 16) /
                maxValue,
      );
      if (previous != null) canvas.drawLine(previous, point, paint);
      canvas.drawCircle(point, 4, paint);
      previous = point;
    }
  }

  @override
  bool shouldRepaint(covariant _TrendPainter oldDelegate) =>
      oldDelegate.days != days ||
      oldDelegate.valueKey != valueKey ||
      oldDelegate.maxValue != maxValue;
}
