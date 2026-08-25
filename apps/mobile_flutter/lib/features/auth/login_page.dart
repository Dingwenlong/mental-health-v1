import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/auth/auth_store.dart';
import '../../generated/ui_copy.g.dart';
import 'captcha_runner.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({
    required this.authStore,
    required this.captchaRunner,
    super.key,
  });

  final AuthStore authStore;
  final CaptchaRunner captchaRunner;

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _phone = TextEditingController();
  final _smsCode = TextEditingController();
  final _phoneFocus = FocusNode();
  final _smsFocus = FocusNode();

  @override
  void dispose() {
    _phone.dispose();
    _smsCode.dispose();
    _phoneFocus.dispose();
    _smsFocus.dispose();
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
          builder: (context, _) => AutofillGroup(
            child: Form(
              child: ListView(
                padding: const EdgeInsets.all(24),
                children: <Widget>[
                  TextField(
                    key: const Key('login-phone'),
                    controller: _phone,
                    focusNode: _phoneFocus,
                    enabled: !auth.hasChallenge && !auth.isBusy,
                    keyboardType: TextInputType.phone,
                    textInputAction: auth.hasChallenge
                        ? TextInputAction.next
                        : TextInputAction.done,
                    autofillHints: const <String>[
                      AutofillHints.telephoneNumberNational,
                    ],
                    inputFormatters: <TextInputFormatter>[
                      FilteringTextInputFormatter.digitsOnly,
                      LengthLimitingTextInputFormatter(11),
                    ],
                    decoration: InputDecoration(
                      labelText: UiCopy.get('auth.phone'),
                      errorText: auth.errorCopyKey == 'auth.phoneRequired'
                          ? UiCopy.get('auth.phoneRequired')
                          : null,
                    ),
                  ),
                  if (auth.hasChallenge) ...<Widget>[
                    const SizedBox(height: 16),
                    TextField(
                      key: const Key('login-sms-code'),
                      controller: _smsCode,
                      focusNode: _smsFocus,
                      enabled: !auth.isBusy,
                      keyboardType: TextInputType.number,
                      textInputAction: TextInputAction.done,
                      autofillHints: const <String>[AutofillHints.oneTimeCode],
                      inputFormatters: <TextInputFormatter>[
                        FilteringTextInputFormatter.digitsOnly,
                        LengthLimitingTextInputFormatter(6),
                      ],
                      decoration: InputDecoration(
                        labelText: UiCopy.get('auth.smsCode'),
                        errorText: auth.errorCopyKey == 'auth.invalidSmsCode'
                            ? UiCopy.get('auth.invalidSmsCode')
                            : null,
                      ),
                      onSubmitted: (_) => _submit(),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      UiCopy.get('auth.sendHint'),
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ],
                  if (auth.errorCopyKey case final key?
                      when key != 'auth.phoneRequired' &&
                          key != 'auth.invalidSmsCode') ...<Widget>[
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
                  if (auth.hasChallenge) ...<Widget>[
                    const SizedBox(height: 20),
                    FilledButton(
                      key: const Key('login-submit'),
                      onPressed: auth.isBusy ? null : _submit,
                      child: Text(
                        UiCopy.get(
                          auth.isBusy ? 'auth.loggingIn' : 'auth.login',
                        ),
                      ),
                    ),
                  ],
                  const SizedBox(height: 12),
                  OutlinedButton(
                    key: const Key('login-send-code'),
                    onPressed: auth.isBusy || auth.secondsUntilResend > 0
                        ? null
                        : _sendCode,
                    child: Text(_sendButtonCopy(auth)),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  String _sendButtonCopy(AuthStore auth) {
    if (auth.isBusy && !auth.hasChallenge) {
      return UiCopy.get('auth.sendingCode');
    }
    if (auth.secondsUntilResend > 0) {
      return '${auth.secondsUntilResend}${UiCopy.get('auth.resendCodeSuffix')}';
    }
    return UiCopy.get('auth.sendCode');
  }

  Future<void> _sendCode() async {
    final sent = await widget.authStore.sendCode(
      _phone.text,
      (bootstrap) => widget.captchaRunner.run(context, bootstrap),
    );
    if (!mounted) return;
    if (sent) {
      _smsCode.clear();
      _smsFocus.requestFocus();
    } else if (widget.authStore.errorCopyKey == 'auth.phoneRequired') {
      _phoneFocus.requestFocus();
    }
  }

  Future<void> _submit() async {
    final loggedIn = await widget.authStore.verifySmsCode(_smsCode.text);
    if (!mounted || loggedIn) return;
    if (widget.authStore.errorCopyKey == 'auth.invalidSmsCode') {
      _smsFocus.requestFocus();
    }
  }
}
