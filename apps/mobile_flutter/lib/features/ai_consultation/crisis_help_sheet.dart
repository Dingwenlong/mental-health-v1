import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';

class CrisisHelpSheet extends StatelessWidget {
  const CrisisHelpSheet({super.key});

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Card(
      key: const Key('crisis-help'),
      color: colors.errorContainer,
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              UiCopy.get('crisis.title'),
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text(UiCopy.get('crisis.help')),
            const SizedBox(height: 12),
            SelectableText(UiCopy.get('crisis.mentalHealthPhone')),
            SelectableText(UiCopy.get('crisis.medicalPhone')),
            SelectableText(UiCopy.get('crisis.policePhone')),
            const SizedBox(height: 12),
            Text(UiCopy.get('crisis.inputLocked')),
          ],
        ),
      ),
    );
  }
}
