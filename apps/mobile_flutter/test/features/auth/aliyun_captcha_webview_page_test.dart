import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_flutter/core/auth/auth_store.dart';
import 'package:mobile_flutter/features/auth/aliyun_captcha_webview_page.dart';

void main() {
  test('captcha page URL carries only the bootstrap query values', () {
    final uri = buildCaptchaPageUri(
      Uri.parse('https://api.example.test/api/v1/'),
      CaptchaBootstrap(
        preChallengeToken: 'not-for-webview',
        prefix: 'xfkdn8',
        encryptedSceneId: 'encrypted/value+',
        expiresAt: DateTime.utc(2026, 8, 25),
      ),
    );

    expect(uri.origin, 'https://api.example.test');
    expect(uri.path, '/captcha/mobile.html');
    expect(uri.queryParameters, <String, String>{
      'prefix': 'xfkdn8',
      'encryptedSceneId': 'encrypted/value+',
    });
    expect(uri.toString(), isNot(contains('not-for-webview')));
    expect(uri.queryParameters, isNot(contains('SceneId')));
  });

  test('top-level navigation allows only the exact captcha page', () {
    final page = Uri.parse(
      'https://api.example.test/captcha/mobile.html'
      '?prefix=xfkdn8&encryptedSceneId=ciphertext',
    );

    expect(isAllowedCaptchaTopLevelNavigation(page, page), isTrue);
    for (final blocked in <String>[
      'https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js',
      'https://captcha.aliyuncs.com/mobile.html',
      'https://api.example.test/captcha/mobile.html/next?prefix=xfkdn8&encryptedSceneId=ciphertext',
      'https://api.example.test/other?prefix=xfkdn8&encryptedSceneId=ciphertext',
      'http://api.example.test/captcha/mobile.html?prefix=xfkdn8&encryptedSceneId=ciphertext',
      'https://api.example.test.evil.invalid/captcha/mobile.html?prefix=xfkdn8&encryptedSceneId=ciphertext',
      'https://api.example.test/captcha/mobile.html?prefix=xfkdn8&encryptedSceneId=ciphertext&next=https://evil.invalid',
      'https://api.example.test/captcha/mobile.html?prefix=xfkdn8&encryptedSceneId=ciphertext#fragment',
    ]) {
      expect(
        isAllowedCaptchaTopLevelNavigation(Uri.parse(blocked), page),
        isFalse,
        reason: blocked,
      );
    }
  });

  test('channel messages expose params or fixed states only', () {
    expect(
      parseCaptchaChannelMessage('captcha-test-param')!.value,
      'captcha-test-param',
    );
    expect(
      parseCaptchaChannelMessage('captcha_failed')!.isTerminalFailure,
      isTrue,
    );
    expect(
      parseCaptchaChannelMessage('captcha_closed')!.isTerminalFailure,
      isTrue,
    );
    expect(
      parseCaptchaChannelMessage('captcha_init_error')!.isTerminalFailure,
      isTrue,
    );
    expect(parseCaptchaChannelMessage(''), isNull);
  });
}
