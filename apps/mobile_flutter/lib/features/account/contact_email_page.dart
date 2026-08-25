import 'package:flutter/material.dart';

import '../../core/account/contact_email_gateway.dart';
import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';

class ContactEmailPage extends StatefulWidget {
  const ContactEmailPage({required this.gateway, super.key});

  final ContactEmailGateway gateway;

  @override
  State<ContactEmailPage> createState() => _ContactEmailPageState();
}

class _ContactEmailPageState extends State<ContactEmailPage> {
  final _email = TextEditingController();
  final _emailFocus = FocusNode();
  bool _isBusy = true;
  String? _errorCopyKey;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _email.dispose();
    _emailFocus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('account.contactEmail.title'))),
      body: SafeArea(
        child: Form(
          child: ListView(
            padding: const EdgeInsets.all(24),
            children: <Widget>[
              Text(UiCopy.get('account.contactEmail.help')),
              const SizedBox(height: 16),
              TextField(
                key: const Key('contact-email'),
                controller: _email,
                focusNode: _emailFocus,
                enabled: !_isBusy,
                keyboardType: TextInputType.emailAddress,
                textInputAction: TextInputAction.done,
                autofillHints: const <String>[AutofillHints.email],
                decoration: InputDecoration(
                  labelText: UiCopy.get('account.contactEmail.label'),
                ),
                onSubmitted: (_) => _save(),
              ),
              if (_errorCopyKey case final key?) ...<Widget>[
                const SizedBox(height: 12),
                Semantics(
                  liveRegion: true,
                  child: Text(
                    UiCopy.get(key),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              ],
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('contact-email-save'),
                onPressed: _isBusy ? null : _save,
                child: Text(
                  UiCopy.get(_isBusy ? 'common.saving' : 'common.save'),
                ),
              ),
              const SizedBox(height: 8),
              TextButton(
                key: const Key('contact-email-clear'),
                onPressed: _isBusy ? null : _clear,
                child: Text(UiCopy.get('account.contactEmail.clear')),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _load() async {
    try {
      _email.text = await widget.gateway.getContactEmail() ?? '';
    } catch (_) {
      _errorCopyKey = 'error.retry';
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  Future<void> _save() async {
    final value = _email.text.trim();
    if (value.isNotEmpty && !_isValidEmail(value)) {
      setState(() => _errorCopyKey = 'account.contactEmail.invalid');
      _emailFocus.requestFocus();
      return;
    }
    await _put(value.isEmpty ? null : value);
  }

  Future<void> _clear() async {
    await _put(null);
    if (mounted && _errorCopyKey == null) _email.clear();
  }

  Future<void> _put(String? value) async {
    setState(() {
      _isBusy = true;
      _errorCopyKey = null;
    });
    try {
      await widget.gateway.putContactEmail(value);
      if (mounted) _email.text = value ?? '';
    } on ApiFailure catch (failure) {
      _errorCopyKey = failure.code == 'CONTACT_EMAIL_INVALID'
          ? 'account.contactEmail.invalid'
          : 'error.retry';
    } catch (_) {
      _errorCopyKey = 'error.retry';
    } finally {
      if (mounted) setState(() => _isBusy = false);
    }
  }

  bool _isValidEmail(String value) {
    return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value);
  }
}
