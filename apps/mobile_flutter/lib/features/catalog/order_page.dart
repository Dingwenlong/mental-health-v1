import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import '../ai_consultation/ai_consultation_page.dart';
import '../ai_consultation/ai_session_launcher.dart';
import 'catalog_repository.dart';
import '../consultation/chat_connection.dart';
import '../consultation/chat_page.dart';
import '../consultation/video_page.dart';
import '../consultation/video_session_launcher.dart';

class OrderPage extends StatefulWidget {
  const OrderPage({
    required this.order,
    this.plan,
    this.repository,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    super.key,
  });

  final DemoOrderSummary order;
  final ServicePlanSummary? plan;
  final DemoOrderRepository? repository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;

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
            if (!awaiting && _canStartHumanVideo) ...<Widget>[
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('start-video'),
                onPressed: _isBusy ? null : _startVideo,
                child: Text(UiCopy.get('video.start')),
              ),
            ],
            if (!awaiting && _canStartAiChat) ...<Widget>[
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('start-ai-chat'),
                onPressed: _isBusy ? null : _startAiChat,
                child: Text(UiCopy.get('ai.start')),
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

  bool get _canStartHumanVideo =>
      widget.videoLauncher != null &&
      widget.plan?.kind == ServiceKind.human &&
      widget.plan?.channel == ServiceChannel.video;

  bool get _canStartAiChat =>
      widget.aiLauncher != null &&
      widget.plan?.kind == ServiceKind.ai &&
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

  Future<void> _startVideo() async {
    setState(() {
      _isBusy = true;
      _errorCopyKey = null;
    });
    try {
      final video = await widget.videoLauncher!.startHumanVideo(_order.id);
      if (!mounted) return;
      await Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (videoContext) => VideoPage(
            sessionId: video.sessionId,
            media: video.media,
            onSwitchToText: () {
              Navigator.of(videoContext).pushReplacement(
                MaterialPageRoute<void>(
                  builder: (_) => ChatPage(
                    sessionId: video.sessionId,
                    controller: video.textController,
                  ),
                ),
              );
            },
          ),
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
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _startAiChat() async {
    setState(() {
      _isBusy = true;
      _errorCopyKey = null;
    });
    try {
      final ai = await widget.aiLauncher!.startAiChat(_order.id);
      if (!mounted) return;
      await Navigator.of(context).push(
        MaterialPageRoute<void>(
          builder: (_) => AiConsultationPage(
            sessionId: ai.sessionId,
            gateway: ai.gateway,
            speech: ai.speech,
          ),
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
      if (mounted) setState(() => _isBusy = false);
    }
  }
}
