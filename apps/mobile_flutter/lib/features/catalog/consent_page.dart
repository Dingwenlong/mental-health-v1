import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import 'catalog_repository.dart';
import 'order_page.dart';
import '../consultation/chat_connection.dart';
import '../consultation/video_session_launcher.dart';

class ConsentPage extends StatefulWidget {
  const ConsentPage({
    required this.plan,
    required this.repository,
    this.chatLauncher,
    this.videoLauncher,
    super.key,
  });

  final ServicePlanSummary plan;
  final CatalogRepository repository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;

  @override
  State<ConsentPage> createState() => _ConsentPageState();
}

class _ConsentPageState extends State<ConsentPage> {
  final Set<ConsentChoice> _choices = <ConsentChoice>{};
  bool _isCreating = false;
  String? _errorCopyKey;

  bool get _allSelected =>
      ConsentChoice.values.every(_choices.contains) && !_isCreating;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('consent.title'))),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: <Widget>[
          Text(UiCopy.get('consent.help')),
          if (widget.plan.paymentMode == PaymentMode.demoPaid) ...<Widget>[
            const SizedBox(height: 16),
            Text(UiCopy.get('order.demoPaid')),
          ],
          const SizedBox(height: 16),
          _consentRow(
            ConsentChoice.service,
            'consent.service',
            const Key('consent-service'),
          ),
          _consentRow(
            ConsentChoice.recording,
            'consent.recording',
            const Key('consent-recording'),
          ),
          _consentRow(
            ConsentChoice.analysis,
            'consent.analysis',
            const Key('consent-analysis'),
          ),
          if (_errorCopyKey case final key?) ...<Widget>[
            const SizedBox(height: 12),
            Text(
              UiCopy.get(key),
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          const SizedBox(height: 20),
          FilledButton(
            key: const Key('create-order'),
            onPressed: _allSelected ? _createOrder : null,
            child: Text(
              _isCreating
                  ? UiCopy.get('order.creating')
                  : UiCopy.get('order.create'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _consentRow(ConsentChoice choice, String copyKey, Key key) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(UiCopy.get(copyKey)),
      trailing: Switch(
        key: key,
        value: _choices.contains(choice),
        onChanged: (value) {
          setState(() {
            value ? _choices.add(choice) : _choices.remove(choice);
          });
        },
      ),
    );
  }

  Future<void> _createOrder() async {
    setState(() {
      _isCreating = true;
      _errorCopyKey = null;
    });
    try {
      final order = await widget.repository.createOrder(
        planId: widget.plan.id,
        consents: Set<ConsentChoice>.from(_choices),
      );
      if (!mounted) {
        return;
      }
      Navigator.of(context).pushReplacement(
        MaterialPageRoute<void>(
          builder: (_) => OrderPage(
            order: order,
            plan: widget.plan,
            repository: widget.repository is DemoOrderRepository
                ? widget.repository as DemoOrderRepository
                : null,
            chatLauncher: widget.chatLauncher,
            videoLauncher: widget.videoLauncher,
          ),
        ),
      );
    } on ApiFailure catch (failure) {
      if (!mounted) {
        return;
      }
      setState(() {
        _errorCopyKey = failure.code == 'FORBIDDEN_RESOURCE'
            ? 'error.forbidden'
            : 'error.retry';
      });
    } finally {
      if (mounted) {
        setState(() => _isCreating = false);
      }
    }
  }
}
