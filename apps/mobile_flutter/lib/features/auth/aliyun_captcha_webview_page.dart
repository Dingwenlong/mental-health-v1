import 'dart:async';

import 'package:flutter/material.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../../core/auth/auth_store.dart';

const _captchaPath = '/captcha/mobile.html';
const _captchaChannel = 'MentalHealthCaptcha';
const _terminalStates = <String>{
  'captcha_failed',
  'captcha_closed',
  'captcha_init_error',
};

Uri buildCaptchaPageUri(Uri apiBaseUri, CaptchaBootstrap bootstrap) {
  return Uri(
    scheme: apiBaseUri.scheme,
    userInfo: apiBaseUri.userInfo,
    host: apiBaseUri.host,
    port: apiBaseUri.hasPort ? apiBaseUri.port : null,
    path: _captchaPath,
    queryParameters: <String, String>{
      'prefix': bootstrap.prefix,
      'encryptedSceneId': bootstrap.encryptedSceneId,
    },
  );
}

bool isAllowedCaptchaTopLevelNavigation(Uri candidate, Uri pageUri) {
  return candidate.scheme == pageUri.scheme &&
      candidate.userInfo == pageUri.userInfo &&
      candidate.host == pageUri.host &&
      candidate.port == pageUri.port &&
      candidate.path == _captchaPath &&
      !candidate.hasFragment &&
      candidate.queryParametersAll.length == 2 &&
      candidate.queryParametersAll['prefix']?.length == 1 &&
      candidate.queryParametersAll['prefix']!.single ==
          pageUri.queryParameters['prefix'] &&
      candidate.queryParametersAll['encryptedSceneId']?.length == 1 &&
      candidate.queryParametersAll['encryptedSceneId']!.single ==
          pageUri.queryParameters['encryptedSceneId'];
}

class CaptchaChannelMessage {
  const CaptchaChannelMessage(this.value, {required this.isTerminalFailure});

  final String value;
  final bool isTerminalFailure;
}

CaptchaChannelMessage? parseCaptchaChannelMessage(String message) {
  if (message.isEmpty) return null;
  return CaptchaChannelMessage(
    message,
    isTerminalFailure: _terminalStates.contains(message),
  );
}

class AliyunCaptchaWebViewPage extends StatefulWidget {
  const AliyunCaptchaWebViewPage({required this.pageUri, super.key});

  final Uri pageUri;

  @override
  State<AliyunCaptchaWebViewPage> createState() =>
      _AliyunCaptchaWebViewPageState();
}

class _AliyunCaptchaWebViewPageState extends State<AliyunCaptchaWebViewPage> {
  WebViewController? _controller;
  bool _completed = false;

  @override
  void initState() {
    super.initState();
    final controller = WebViewController();
    _controller = controller;
    unawaited(_configure(controller));
  }

  Future<void> _configure(WebViewController controller) async {
    await controller.setJavaScriptMode(JavaScriptMode.unrestricted);
    await controller.setNavigationDelegate(
      NavigationDelegate(
        onNavigationRequest: (request) {
          if (!request.isMainFrame) return NavigationDecision.navigate;
          final candidate = Uri.tryParse(request.url);
          return candidate != null &&
                  isAllowedCaptchaTopLevelNavigation(candidate, widget.pageUri)
              ? NavigationDecision.navigate
              : NavigationDecision.prevent;
        },
        onWebResourceError: (error) {
          if (error.isForMainFrame ?? true) _finish(null);
        },
      ),
    );
    await controller.addJavaScriptChannel(
      _captchaChannel,
      onMessageReceived: (message) {
        final parsed = parseCaptchaChannelMessage(message.message);
        if (parsed == null || parsed.isTerminalFailure) {
          _finish(null);
        } else {
          _finish(parsed.value);
        }
      },
    );
    await controller.clearCache();
    await controller.clearLocalStorage();
    if (!_completed) await controller.loadRequest(widget.pageUri);
  }

  void _finish(String? result) {
    if (_completed || !mounted) return;
    _completed = true;
    final controller = _controller;
    _controller = null;
    if (controller != null) {
      unawaited(controller.clearCache());
      unawaited(controller.clearLocalStorage());
    }
    Navigator.of(context).pop(result);
  }

  @override
  void dispose() {
    _completed = true;
    final controller = _controller;
    _controller = null;
    if (controller != null) {
      unawaited(controller.clearCache());
      unawaited(controller.clearLocalStorage());
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final controller = _controller;
    return PopScope<String?>(
      canPop: true,
      onPopInvokedWithResult: (didPop, result) {
        if (didPop) _completed = true;
      },
      child: Scaffold(
        body: SafeArea(
          child: controller == null
              ? const SizedBox.shrink()
              : WebViewWidget(controller: controller),
        ),
      ),
    );
  }
}
