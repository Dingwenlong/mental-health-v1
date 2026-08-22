import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';
import 'catalog_repository.dart';

class ServicePlanCard extends StatelessWidget {
  const ServicePlanCard({
    required this.plan,
    required this.onSelected,
    super.key,
  });

  final ServicePlanSummary plan;
  final VoidCallback onSelected;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        onTap: onSelected,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                UiCopy.get(_planLabelKey(plan)),
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              Text(
                plan.paymentMode == PaymentMode.free
                    ? UiCopy.get('catalog.free')
                    : _formatAmount(plan.priceInMinorUnits),
              ),
              const SizedBox(height: 4),
              Text('${plan.durationMinutes} ${UiCopy.get('catalog.duration')}'),
              if (plan.paymentMode == PaymentMode.demoPaid) ...<Widget>[
                const SizedBox(height: 8),
                Text(UiCopy.get('order.demoPaid')),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

String _planLabelKey(ServicePlanSummary plan) =>
    switch ((plan.kind, plan.channel)) {
      (ServiceKind.human, ServiceChannel.chat) => 'plan.human.chat',
      (ServiceKind.human, ServiceChannel.video) => 'plan.human.video',
      (ServiceKind.ai, ServiceChannel.chat) => 'plan.ai.chat',
      (ServiceKind.ai, ServiceChannel.video) => 'plan.ai.video',
    };

String _formatAmount(int minorUnits) =>
    '¥${(minorUnits / 100).toStringAsFixed(2)}';
