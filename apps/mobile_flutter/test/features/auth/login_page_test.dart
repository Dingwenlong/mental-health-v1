import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/auth/captcha_runner.dart';
import 'package:mobile_flutter/features/auth/login_page.dart';

void main() {
  for (final size in <Size>[const Size(360, 800), const Size(412, 915)]) {
    testWidgets(
      'login remains scrollable with keyboard at ${size.width.toInt()}x${size.height.toInt()}',
      (tester) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = size;
        addTearDown(tester.view.reset);

        final store = AuthStore(
          gateway: _FakeAuthGateway(),
          storage: _MemoryTokenStorage(),
        );
        await tester.pumpWidget(
          MaterialApp(
            home: LoginPage(
              authStore: store,
              captchaRunner: const _FakeCaptchaRunner(),
            ),
          ),
        );

        expect(tester.takeException(), isNull);
        expect(
          tester.getSize(find.byKey(const Key('login-cover'))).aspectRatio,
          closeTo(16 / 9, 0.01),
        );

        await tester.showKeyboard(find.byKey(const Key('login-phone')));
        tester.view.viewInsets = const FakeViewPadding(bottom: 320);
        await tester.pump();
        await tester.ensureVisible(find.byKey(const Key('login-send-code')));
        await tester.pump();

        expect(tester.takeException(), isNull);
        expect(find.byKey(const Key('login-send-code')), findsOneWidget);
      },
    );
  }

  testWidgets('login stays one form and reveals the SMS fields in place', (
    tester,
  ) async {
    final gateway = _FakeAuthGateway();
    final store = AuthStore(gateway: gateway, storage: _MemoryTokenStorage());
    await tester.pumpWidget(
      MaterialApp(
        home: LoginPage(
          authStore: store,
          captchaRunner: const _FakeCaptchaRunner(),
        ),
      ),
    );

    expect(find.byType(Form), findsOneWidget);
    expect(find.byKey(const Key('login-cover')), findsOneWidget);
    expect(find.text('心理健康'), findsOneWidget);
    expect(find.text('登录'), findsOneWidget);
    expect(find.byKey(const Key('login-phone')), findsOneWidget);
    expect(find.byKey(const Key('login-send-code')), findsOneWidget);
    expect(find.byKey(const Key('login-sms-code')), findsNothing);
    expect(find.byKey(const Key('login-submit')), findsNothing);
    expect(find.textContaining('+86'), findsNothing);
    expect(find.text('换个手机号'), findsNothing);
    expect(find.textContaining('密码'), findsNothing);
    expect(find.textContaining('邮箱'), findsNothing);

    await tester.enterText(find.byKey(const Key('login-phone')), '13800138001');
    await tester.ensureVisible(find.byKey(const Key('login-send-code')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('login-send-code')));
    await tester.pump();

    final phone = tester.widget<TextField>(
      find.byKey(const Key('login-phone')),
    );
    expect(phone.enabled, isFalse);
    expect(find.byKey(const Key('login-sms-code')), findsOneWidget);
    await tester.drag(find.byType(ListView), const Offset(0, -240));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('login-submit')), findsOneWidget);
    expect(find.text('如果该手机号已登记，你会收到验证码'), findsOneWidget);
    expect(find.text('60 秒后重新获取'), findsOneWidget);
    expect(find.byType(Form), findsOneWidget);

    await tester.pump(const Duration(seconds: 1));
    expect(find.text('59 秒后重新获取'), findsOneWidget);

    await tester.enterText(find.byKey(const Key('login-sms-code')), '246810');
    await tester.ensureVisible(find.byKey(const Key('login-submit')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('login-submit')));
    await tester.pump();
    expect(gateway.verifiedCode, '246810');
    expect(store.isAuthenticated, isTrue);
  });

  testWidgets('captcha close keeps the phone form and announces the error', (
    tester,
  ) async {
    final store = AuthStore(
      gateway: _FakeAuthGateway(),
      storage: _MemoryTokenStorage(),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: LoginPage(
          authStore: store,
          captchaRunner: const _FakeCaptchaRunner(result: null),
        ),
      ),
    );

    await tester.enterText(find.byKey(const Key('login-phone')), '13800138001');
    await tester.ensureVisible(find.byKey(const Key('login-send-code')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('login-send-code')));
    await tester.pump();

    expect(find.text('未完成安全验证，请重试'), findsOneWidget);
    expect(find.byKey(const Key('login-sms-code')), findsNothing);
    expect(
      tester
          .getSemantics(find.text('未完成安全验证，请重试'))
          .flagsCollection
          .isLiveRegion,
      isTrue,
    );
  });
}

class _FakeCaptchaRunner implements CaptchaRunner {
  const _FakeCaptchaRunner({this.result = 'captcha-test-param'});

  final String? result;

  @override
  Future<String?> run(BuildContext context, CaptchaBootstrap bootstrap) async =>
      result;
}

class _FakeAuthGateway implements AuthGateway {
  String? verifiedCode;

  @override
  Future<CaptchaBootstrap> bootstrapPhone(String phoneNumber) async =>
      CaptchaBootstrap(
        preChallengeToken: 'pre-token',
        prefix: 'xfkdn8',
        encryptedSceneId: 'encrypted-scene-id',
        expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 5)),
      );

  @override
  Future<SmsChallenge> createSmsChallenge(
    String preChallengeToken,
    String captchaVerifyParam,
  ) async => SmsChallenge(
    challengeToken: 'challenge-token',
    expiresAt: DateTime.now().toUtc().add(const Duration(minutes: 5)),
    resendAt: DateTime.now().toUtc().add(const Duration(seconds: 60)),
  );

  @override
  Future<String> verifySmsCode(String challengeToken, String code) async {
    verifiedCode = code;
    return 'access-token';
  }
}

class _MemoryTokenStorage implements TokenStorage {
  String? token;

  @override
  Future<void> clearAccessToken() async => token = null;

  @override
  Future<String?> readAccessToken() async => token;

  @override
  Future<void> writeAccessToken(String value) async => token = value;
}
