import 'package:flutter/material.dart';

import '../../core/auth/auth_store.dart';
import 'aliyun_captcha_webview_page.dart';

abstract interface class CaptchaRunner {
  Future<String?> run(BuildContext context, CaptchaBootstrap bootstrap);
}

class WebViewCaptchaRunner implements CaptchaRunner {
  const WebViewCaptchaRunner({required this.apiBaseUri});

  final Uri apiBaseUri;

  @override
  Future<String?> run(BuildContext context, CaptchaBootstrap bootstrap) {
    return Navigator.of(context).push<String>(
      MaterialPageRoute<String>(
        fullscreenDialog: true,
        builder: (_) => AliyunCaptchaWebViewPage(
          pageUri: buildCaptchaPageUri(apiBaseUri, bootstrap),
        ),
      ),
    );
  }
}
