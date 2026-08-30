import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../core/auth/auth_store.dart';
import '../../generated/ui_copy.g.dart';
import '../../design/app_design.dart';
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
      body: SafeArea(
        child: AnimatedBuilder(
          animation: auth,
          builder: (context, _) => AutofillGroup(
            child: Form(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(22, 14, 22, 36),
                children: <Widget>[
                  const _LoginCover(),
                  const SizedBox(height: 20),
                  Text(
                    UiCopy.get('app.description'),
                    style: Theme.of(context).textTheme.bodyMedium
                        ?.copyWith(color: AppPalette.muted, height: 1.65),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    UiCopy.get('auth.title'),
                    style: Theme.of(context).textTheme.headlineMedium,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    UiCopy.get('auth.sendHint'),
                    style: Theme.of(context).textTheme.bodyMedium
                        ?.copyWith(color: AppPalette.muted),
                  ),
                  const SizedBox(height: 20),
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

class _LoginCover extends StatelessWidget {
  const _LoginCover();

  @override
  Widget build(BuildContext context) => Align(
    alignment: Alignment.topCenter,
    child: ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 420),
      child: AspectRatio(
        aspectRatio: 16 / 9,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: Stack(
            fit: StackFit.expand,
            children: <Widget>[
              const ColoredBox(color: Color(0xFF103D38)),
              Image.asset(
                'assets/images/login-room.webp',
                key: const Key('login-cover'),
                fit: BoxFit.cover,
                alignment: Alignment.center,
                excludeFromSemantics: true,
                errorBuilder: (_, _, _) => const SizedBox.expand(),
              ),
              const ColoredBox(color: Color(0x8A103D38)),
              Positioned(
                left: 20,
                right: 20,
                bottom: 18,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Text(
                      UiCopy.get('app.title'),
                      style: Theme.of(context).textTheme.headlineMedium
                          ?.copyWith(
                            color: const Color(0xFFF4FBF9),
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      UiCopy.get('app.tagline'),
                      style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: const Color(0xFFDCE9E4),
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
