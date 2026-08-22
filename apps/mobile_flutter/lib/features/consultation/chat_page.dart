import 'dart:async';

import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';
import 'chat_connection.dart';

class ChatPage extends StatefulWidget {
  const ChatPage({
    required this.sessionId,
    required this.controller,
    super.key,
  });

  final String sessionId;
  final ChatController controller;

  @override
  State<ChatPage> createState() => _ChatPageState();
}

class _ChatPageState extends State<ChatPage> {
  final TextEditingController _input = TextEditingController();
  var _sending = false;
  String? _errorCopyKey;

  @override
  void initState() {
    super.initState();
    widget.controller.addListener(_refresh);
    unawaited(_connect());
  }

  @override
  void dispose() {
    widget.controller.removeListener(_refresh);
    _input.dispose();
    unawaited(widget.controller.disconnect());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final connected =
        widget.controller.status == ChatConnectionStatus.connected;
    final counselorOnline = widget.controller.presences.any(
      (presence) => presence.kind == 'Practitioner' && presence.online,
    );
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('chat.title'))),
      body: SafeArea(
        child: Column(
          children: <Widget>[
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              color: Theme.of(context).colorScheme.surfaceContainerHighest,
              child: Text(
                '${_statusText(widget.controller.status)} · '
                '${UiCopy.get(counselorOnline ? 'chat.counselorOnline' : 'chat.counselorOffline')}',
              ),
            ),
            Expanded(
              child: widget.controller.messages.isEmpty
                  ? Center(child: Text(UiCopy.get('chat.noMessages')))
                  : ListView.builder(
                      padding: const EdgeInsets.all(16),
                      itemCount: widget.controller.messages.length,
                      itemBuilder: (context, index) {
                        final message = widget.controller.messages[index];
                        return ListTile(
                          key: ValueKey(message.id),
                          title: Text(message.text),
                          subtitle: Text(
                            UiCopy.get(
                              message.senderKind == 'Practitioner'
                                  ? 'chat.counselor'
                                  : 'chat.me',
                            ),
                          ),
                        );
                      },
                    ),
            ),
            if (_errorCopyKey case final key?)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(
                  UiCopy.get(key),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ),
            Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                children: <Widget>[
                  Expanded(
                    child: TextField(
                      key: const Key('chat-input'),
                      controller: _input,
                      enabled: connected,
                      maxLength: 4000,
                      minLines: 1,
                      maxLines: 4,
                      textInputAction: TextInputAction.send,
                      decoration: InputDecoration(
                        hintText: UiCopy.get('chat.messageHint'),
                        counterText: '',
                      ),
                      onSubmitted: (_) => unawaited(_send()),
                    ),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    key: const Key('chat-send'),
                    onPressed: connected && !_sending ? _send : null,
                    child: Text(UiCopy.get('chat.send')),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _connect() async {
    try {
      await widget.controller.connect(widget.sessionId);
    } catch (_) {
      if (mounted) {
        setState(() => _errorCopyKey = 'error.retry');
      }
    }
  }

  Future<void> _send() async {
    final text = _input.text.trim();
    if (text.isEmpty || _sending) {
      return;
    }
    _input.clear();
    setState(() {
      _sending = true;
      _errorCopyKey = null;
    });
    try {
      await widget.controller.send(
        text,
        'mobile-${DateTime.now().microsecondsSinceEpoch}',
      );
    } catch (_) {
      if (mounted) {
        if (_input.text.isEmpty) {
          _input.value = TextEditingValue(
            text: text,
            selection: TextSelection.collapsed(offset: text.length),
          );
        }
        setState(() => _errorCopyKey = 'error.retry');
      }
    } finally {
      if (mounted) {
        setState(() => _sending = false);
      }
    }
  }

  void _refresh() {
    if (mounted) {
      setState(() {});
    }
  }

  String _statusText(ChatConnectionStatus status) =>
      UiCopy.get(switch (status) {
        ChatConnectionStatus.connecting => 'chat.connecting',
        ChatConnectionStatus.connected => 'chat.connected',
        ChatConnectionStatus.reconnecting => 'chat.reconnecting',
        ChatConnectionStatus.disconnected => 'chat.disconnected',
      });
}
