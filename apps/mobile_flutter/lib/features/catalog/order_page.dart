import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import 'catalog_repository.dart';
import '../consultation/chat_connection.dart';
import '../consultation/chat_page.dart';

class OrderPage extends StatefulWidget {
  const OrderPage({
    required this.order,
    this.plan,
    this.repository,
    this.chatLauncher,
    super.key,
  });

  final DemoOrderSummary order;
  final ServicePlanSummary? plan;
  final DemoOrderRepository? repository;
  final ChatSessionLauncher? chatLauncher;

  @override
  State<OrderPage> createState() => _OrderPageState();
}

class _OrderPageState extends State<OrderPage> {
  late DemoOrderSummary _order = widget.order;
  bool _isBusy = false;
  String? _errorCopyKey;

  @override
  Widget build(BuildContext context) {
    final awaiting = _order.status == DemoOrderStatus.awaitingDemoPayment;
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('order.created'))),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            Text(
              UiCopy.get(
                awaiting
                    ? 'order.awaitingDemoPayment'
                    : _order.amountInMinorUnits == 0
                    ? 'order.freeConfirmed'
                    : 'order.confirmed',
              ),
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            if (_order.amountInMinorUnits > 0) ...<Widget>[
              const SizedBox(height: 12),
              Text(UiCopy.get('order.demoPaid')),
              const SizedBox(height: 8),
              Text('¥${(_order.amountInMinorUnits / 100).toStringAsFixed(2)}'),
            ],
            if (_errorCopyKey case final key?) ...<Widget>[
              const SizedBox(height: 12),
              Text(
                UiCopy.get(key),
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            if (awaiting && widget.repository != null) ...<Widget>[
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('confirm-order'),
                onPressed: _isBusy ? null : _confirm,
                child: Text(UiCopy.get('order.confirmDemoPayment')),
              ),
            ],
            if (!awaiting && _canStartHumanChat) ...<Widget>[
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('start-chat'),
                onPressed: _isBusy ? null : _startChat,
                child: Text(UiCopy.get('chat.start')),
              ),
            ],
          ],
        ),
      ),
    );
  }

  bool get _canStartHumanChat =>
      widget.chatLauncher != null &&
      widget.plan?.kind == ServiceKind.human &&
      widget.plan?.channel == ServiceChannel.chat;

  Future<void> _confirm() async {
    setState(() {
      _isBusy = true;
      _errorCopyKey = null;
    });
    try {
      final order = await widget.repository!.confirmOrder(_order.id);
      if (mounted) {
        setState(() => _order = order);
      }
    } on ApiFailure catch (failure) {
      if (mounted) {
        setState(() {
          _errorCopyKey = failure.code == 'FORBIDDEN_RESOURCE'
              ? 'error.forbidden'
              : 'error.retry';
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }

  Future<void> _startChat() async {
    setState(() {
      _isBusy = true;
      _errorCopyKey = null;
    });
    try {
      final chat = await widget.chatLauncher!.startHumanChat(_order.id);
      if (!mounted) {
        return;
      }
      await Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) =>
              ChatPage(sessionId: chat.sessionId, controller: chat.controller),
        ),
      );
    } on ApiFailure catch (failure) {
      if (mounted) {
        setState(() {
          _errorCopyKey = failure.code == 'FORBIDDEN_RESOURCE'
              ? 'error.forbidden'
              : 'error.retry';
        });
      }
    } finally {
      if (mounted) {
        setState(() => _isBusy = false);
      }
    }
  }
}
