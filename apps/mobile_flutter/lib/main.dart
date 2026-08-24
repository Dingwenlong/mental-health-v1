import 'package:flutter/material.dart';

import 'core/api/api_client.dart';
import 'core/auth/auth_store.dart';
import 'core/network/demo_certificate_trust.dart';
import 'features/auth/login_page.dart';
import 'features/ai_consultation/ai_session_launcher.dart';
import 'features/catalog/catalog_page.dart';
import 'features/catalog/catalog_repository.dart';
import 'features/consultation/chat_connection.dart';
import 'features/consultation/video_session_launcher.dart';
import 'generated/ui_copy.g.dart';
import 'providers/speech/chime_fallback_speech_provider.dart';
import 'providers/speech/flutter_tts_speech_provider.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await installDemoCertificateTrust();
  const storage = SecureTokenStorage();
  final client = ApiClient(
    baseUrl: const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://10.0.2.2:5165/api/v1/',
    ),
    readAccessToken: storage.readAccessToken,
  );
  final authStore = AuthStore(
    gateway: ApiAuthGateway(client),
    storage: storage,
  );
  const chatHubUrl = String.fromEnvironment(
    'CHAT_HUB_URL',
    defaultValue: 'http://10.0.2.2:5165/hubs/chat',
  );
  final chatLauncher = ApiChatSessionLauncher(client, chatHubUrl);
  final videoLauncher = ApiVideoSessionLauncher(
    client,
    const String.fromEnvironment(
      'RTC_HUB_URL',
      defaultValue: 'http://10.0.2.2:5165/hubs/rtc',
    ),
    chatHubUrl,
  );
  final aiLauncher = ApiAiSessionLauncher(
    client,
    speechFactory: () => FlutterTtsSpeechProvider(
      engine: FlutterTtsEngine(),
      fallback: ChimeFallbackSpeechProvider(player: AssetChimePlayer()),
    ),
  );
  await authStore.restore();
  runApp(
    MentalHealthApp(
      authStore: authStore,
      catalogRepository: ApiCatalogRepository(client),
      chatLauncher: chatLauncher,
      videoLauncher: videoLauncher,
      aiLauncher: aiLauncher,
    ),
  );
}

class MentalHealthApp extends StatelessWidget {
  const MentalHealthApp({
    this.authStore,
    this.catalogRepository,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    super.key,
  });

  final AuthStore? authStore;
  final CatalogRepository? catalogRepository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: UiCopy.get('app.title'),
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF2E6F68)),
        useMaterial3: true,
      ),
      home: authStore == null || catalogRepository == null
          ? const Scaffold()
          : AnimatedBuilder(
              animation: authStore!,
              builder: (context, _) => authStore!.isAuthenticated
                  ? CatalogPage(
                      repository: catalogRepository!,
                      chatLauncher: chatLauncher,
                      videoLauncher: videoLauncher,
                      aiLauncher: aiLauncher,
                      onLogout: authStore!.logout,
                    )
                  : LoginPage(authStore: authStore!),
            ),
    );
  }
}
