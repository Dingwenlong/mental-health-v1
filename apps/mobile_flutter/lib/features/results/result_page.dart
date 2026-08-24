import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';
import 'evidence_list.dart';

export 'evidence_list.dart' show AssessmentEvidence;

class AssessmentResult {
  const AssessmentResult({
    required this.sessionId,
    required this.score,
    required this.level,
    required this.ruleSetVersion,
    required this.missing,
    required this.evidence,
    required this.createdAt,
  });

  final String sessionId;
  final double score;
  final String level;
  final String ruleSetVersion;
  final List<String> missing;
  final List<AssessmentEvidence> evidence;
  final DateTime createdAt;
}

class FollowUpSummary {
  const FollowUpSummary({
    required this.id,
    required this.status,
    this.dueAt,
    this.assigneeName,
  });

  final String id;
  final String status;
  final DateTime? dueAt;
  final String? assigneeName;
}

class ResultPage extends StatelessWidget {
  const ResultPage({
    required this.result,
    this.followUp,
    this.onOpenFollowUp,
    this.onBack,
    super.key,
  });

  final AssessmentResult result;
  final FollowUpSummary? followUp;
  final VoidCallback? onOpenFollowUp;
  final VoidCallback? onBack;

  @override
  Widget build(BuildContext context) {
    final used = result.evidence
        .where((item) => !result.missing.contains(item.modality))
        .map((item) => item.modality)
        .toSet()
        .toList();
    return Scaffold(
      appBar: AppBar(
        leading: onBack == null
            ? null
            : IconButton(onPressed: onBack, icon: const Icon(Icons.arrow_back)),
        title: Text(UiCopy.get('result.title')),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 28),
          children: <Widget>[
            const _StatusRail(),
            const SizedBox(height: 24),
            Row(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: <Widget>[
                Text(
                  result.score.round().toString(),
                  style: Theme.of(context).textTheme.displayLarge?.copyWith(
                    color: const Color(0xFF174E49),
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: 18),
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Text(
                    _levelName(result.level),
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: _levelColor(result.level),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(UiCopy.get('result.notDiagnosis')),
            const _SectionDivider(),
            _SectionTitle(UiCopy.get('result.usedData')),
            ...used.map(
              (modality) => _DataRow(
                modality: modality,
                trailing: UiCopy.get('result.completed'),
              ),
            ),
            if (result.missing.isNotEmpty) ...<Widget>[
              const SizedBox(height: 8),
              Text(
                _missingSentence(result.missing),
                key: const Key('result-missing-summary'),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            const _SectionDivider(),
            EvidenceList(evidence: result.evidence, missing: result.missing),
            const _SectionDivider(),
            _SectionTitle(UiCopy.get('followUp.arrangement')),
            if (followUp == null)
              Text(
                UiCopy.get('followUp.none'),
                style: Theme.of(context).textTheme.bodyMedium
                    ?.copyWith(color: const Color(0xFF5D6D68)),
              )
            else ...<Widget>[
              if (followUp!.dueAt case final dueAt?)
                _InfoLine(
                  icon: Icons.calendar_today_outlined,
                  text: _formatLocalDateTime(dueAt),
                ),
              if (followUp!.assigneeName case final name?)
                _InfoLine(icon: Icons.person_outline, text: name),
              const SizedBox(height: 14),
              FilledButton(
                key: const Key('open-follow-up'),
                onPressed: onOpenFollowUp,
                child: Text(UiCopy.get('followUp.open')),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static String _missingSentence(List<String> missing) {
    final names = missing.map(_modalityName).toList();
    final prefix = UiCopy.get('result.missingPrefix');
    if (names.length == 1) return '$prefix${names.single}';
    if (names.length == 2) {
      return '$prefix${names.first}${UiCopy.get('common.and')}${names.last}';
    }
    return '$prefix${names.join(UiCopy.get('common.listSeparator'))}';
  }

  static String _modalityName(String modality) => switch (modality) {
    'Scale' => UiCopy.get('modality.scale'),
    'Text' => UiCopy.get('modality.text'),
    'Audio' => UiCopy.get('modality.audio'),
    'Video' => UiCopy.get('modality.video'),
    'Trend' => UiCopy.get('modality.previousRecords'),
    _ => modality,
  };

  static String _levelName(String level) => switch (level) {
    'L1' => UiCopy.get('result.level.l1'),
    'L2' => UiCopy.get('result.level.l2'),
    'L3' => UiCopy.get('result.level.l3'),
    'Crisis' => UiCopy.get('result.level.crisis'),
    _ => level,
  };

  static Color _levelColor(String level) => level == 'Crisis' || level == 'L3'
      ? const Color(0xFFB43D2F)
      : const Color(0xFF245D57);

  static String _formatLocalDateTime(DateTime value) {
    final local = value.toLocal();
    String two(int number) => number.toString().padLeft(2, '0');
    return '${local.year}${UiCopy.get('date.year')}'
        '${local.month}${UiCopy.get('date.month')}'
        '${local.day}${UiCopy.get('date.day')} '
        '${two(local.hour)}:${two(local.minute)}';
  }
}

class _StatusRail extends StatelessWidget {
  const _StatusRail();

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.centerLeft,
    child: Container(width: 98, height: 4, color: const Color(0xFF2A9D8F)),
  );
}

class _SectionDivider extends StatelessWidget {
  const _SectionDivider();

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 18),
    child: Divider(height: 1, color: Theme.of(context).dividerColor),
  );
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      text,
      style: Theme.of(context).textTheme.titleMedium
          ?.copyWith(fontWeight: FontWeight.w700),
    ),
  );
}

class _DataRow extends StatelessWidget {
  const _DataRow({required this.modality, required this.trailing});

  final String modality;
  final String trailing;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 9),
    child: Row(
      children: <Widget>[
        Icon(_iconFor(modality), size: 20),
        const SizedBox(width: 12),
        Expanded(child: Text(ResultPage._modalityName(modality))),
        Text(
          trailing,
          style: Theme.of(context).textTheme.bodySmall
              ?.copyWith(color: const Color(0xFF5D6D68)),
        ),
      ],
    ),
  );

  static IconData _iconFor(String modality) => switch (modality) {
    'Scale' => Icons.assignment_outlined,
    'Text' => Icons.chat_bubble_outline,
    'Audio' => Icons.mic_none,
    'Video' => Icons.videocam_outlined,
    _ => Icons.history,
  };
}

class _InfoLine extends StatelessWidget {
  const _InfoLine({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      children: <Widget>[
        Icon(icon, size: 20),
        const SizedBox(width: 12),
        Text(text),
      ],
    ),
  );
}
