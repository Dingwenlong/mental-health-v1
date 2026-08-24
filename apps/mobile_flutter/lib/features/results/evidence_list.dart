import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';

class AssessmentEvidence {
  const AssessmentEvidence({
    required this.code,
    required this.modality,
    required this.contribution,
    required this.quality,
    required this.sourceRange,
  });

  final String code;
  final String modality;
  final double contribution;
  final double quality;
  final String sourceRange;
}

class EvidenceList extends StatelessWidget {
  const EvidenceList({
    required this.evidence,
    required this.missing,
    super.key,
  });

  final List<AssessmentEvidence> evidence;
  final List<String> missing;

  static const _weights = <String, int>{
    'Scale': 45,
    'Text': 25,
    'Audio': 15,
    'Video': 5,
    'Trend': 10,
  };

  @override
  Widget build(BuildContext context) {
    final byModality = <String, AssessmentEvidence>{
      for (final item in evidence) item.modality: item,
    };
    return ExpansionTile(
      key: const Key('result-evidence'),
      tilePadding: EdgeInsets.zero,
      childrenPadding: EdgeInsets.zero,
      title: Text(
        UiCopy.get('result.scoreReason'),
        style: Theme.of(context).textTheme.titleMedium,
      ),
      children: _weights.entries.map((entry) {
        final item = byModality[entry.key];
        final isMissing = missing.contains(entry.key);
        return Container(
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            border: Border(
              top: BorderSide(color: Theme.of(context).dividerColor),
            ),
          ),
          child: Row(
            children: <Widget>[
              Expanded(
                child: Text('${_modalityName(entry.key)} ${entry.value}%'),
              ),
              Text(
                isMissing || item == null
                    ? UiCopy.get('quality.unavailable')
                    : '${UiCopy.get('quality.prefix')}${_qualityName(item.quality)}'
                          '${UiCopy.get('evidence.contributionPrefix')}${item.contribution.toStringAsFixed(1)}',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ],
          ),
        );
      }).toList(),
    );
  }

  static String _modalityName(String modality) => switch (modality) {
    'Scale' => UiCopy.get('modality.scale'),
    'Text' => UiCopy.get('modality.text'),
    'Audio' => UiCopy.get('modality.audio'),
    'Video' => UiCopy.get('modality.video'),
    'Trend' => UiCopy.get('modality.previousRecords'),
    _ => modality,
  };

  static String _qualityName(double quality) {
    if (quality >= 0.8) return UiCopy.get('quality.high');
    if (quality >= 0.5) return UiCopy.get('quality.medium');
    return UiCopy.get('quality.low');
  }
}
