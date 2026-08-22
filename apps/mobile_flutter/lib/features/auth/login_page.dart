import 'package:flutter/material.dart';

import '../../core/auth/auth_store.dart';
import '../../generated/ui_copy.g.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({required this.authStore, super.key});

  final AuthStore authStore;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _totpCode = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    _totpCode.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final auth = widget.authStore;
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('auth.title'))),
      body: SafeArea(
        child: AnimatedBuilder(
          animation: auth,
          builder: (context, _) => ListView(
            padding: const EdgeInsets.all(24),
            children: <Widget>[
              TextField(
                key: const Key('login-email'),
                controller: _email,
                keyboardType: TextInputType.emailAddress,
                autofillHints: const <String>[AutofillHints.email],
                decoration: InputDecoration(
                  labelText: UiCopy.get('auth.email'),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                key: const Key('login-password'),
                controller: _password,
                obscureText: true,
                autofillHints: const <String>[AutofillHints.password],
                decoration: InputDecoration(
                  labelText: UiCopy.get('auth.password'),
                ),
              ),
              if (auth.needsMfaSetup || auth.needsMfaCode) ...<Widget>[
                const SizedBox(height: 20),
                if (auth.needsMfaSetup) ...<Widget>[
                  Text(UiCopy.get('auth.mfaSetup')),
                  const SizedBox(height: 8),
                  SelectableText(auth.mfaManualKey ?? ''),
                ],
                TextField(
                  key: const Key('login-totp'),
                  controller: _totpCode,
                  keyboardType: TextInputType.number,
                  maxLength: 6,
                  decoration: InputDecoration(
                    labelText: UiCopy.get('auth.mfaCode'),
                    helperText: UiCopy.get('auth.mfaHint'),
                  ),
                ),
              ],
              if (auth.errorCopyKey case final key?) ...<Widget>[
                const SizedBox(height: 12),
                Text(
                  UiCopy.get(key),
                  style: TextStyle(color: Theme.of(context).colorScheme.error),
                ),
              ],
              const SizedBox(height: 20),
              FilledButton(
                key: const Key('login-submit'),
                onPressed: auth.isBusy ? null : _submit,
                child: Text(
                  auth.isBusy
                      ? UiCopy.get('auth.loggingIn')
                      : auth.needsMfaSetup
                      ? UiCopy.get('auth.enableMfa')
                      : UiCopy.get('auth.login'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Future<void> _submit() async {
    final auth = widget.authStore;
    if (auth.needsMfaSetup) {
      await auth.completeMfaSetup(_totpCode.text);
      return;
    }

    await auth.login(
      email: _email.text,
      password: _password.text,
      totpCode: auth.needsMfaCode ? _totpCode.text : null,
    );
  }
}
