import 'package:flutter/material.dart';

import 'core/api/api_client.dart';
import 'core/auth/auth_store.dart';
import 'core/network/demo_certificate_trust.dart';
import 'features/auth/login_page.dart';
import 'features/ai_consultation/ai_session_launcher.dart';
import 'features/catalog/catalog_repository.dart';
import 'features/consultation/chat_connection.dart';
import 'features/consultation/video_session_launcher.dart';
import 'features/follow_ups/follow_up_list_page.dart';
import 'features/follow_ups/local_reminder_service.dart';
import 'features/home/patient_home_page.dart';
import 'features/results/analysis_progress_page.dart';
import 'generated/ui_copy.g.dart';
import 'providers/speech/chime_fallback_speech_provider.dart';
import 'providers/speech/flutter_tts_speech_provider.dart';

ThemeData buildAppTheme() => ThemeData(
  colorScheme: ColorScheme.fromSeed(
    seedColor: const Color(0xFF245D57),
    surface: Colors.white,
    error: const Color(0xFFA12626),
  ),
  scaffoldBackgroundColor: const Color(0xFFF5F7F6),
  dividerColor: const Color(0xFFD8E0DE),
  appBarTheme: const AppBarTheme(
    backgroundColor: Colors.white,
    foregroundColor: Color(0xFF18221F),
    elevation: 0,
    scrolledUnderElevation: 0,
    shape: Border(bottom: BorderSide(color: Color(0xFFD8E0DE))),
  ),
  cardTheme: const CardThemeData(
    color: Colors.white,
    elevation: 0,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.all(Radius.circular(8)),
      side: BorderSide(color: Color(0xFFD8E0DE)),
    ),
  ),
  navigationBarTheme: const NavigationBarThemeData(
    backgroundColor: Colors.white,
    indicatorColor: Color(0xFFDCEBE8),
    elevation: 0,
  ),
  useMaterial3: true,
);

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
      resultGateway: ApiResultGateway(client),
      followUpRepository: ApiFollowUpRepository(client),
      reminders: DeviceLocalReminderService(),
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
    this.resultGateway,
    this.followUpRepository,
    this.reminders,
    super.key,
  });

  final AuthStore? authStore;
  final CatalogRepository? catalogRepository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  final ResultGateway? resultGateway;
  final FollowUpRepository? followUpRepository;
  final LocalReminderService? reminders;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: UiCopy.get('app.title'),
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home:
          authStore == null ||
              catalogRepository == null ||
              resultGateway == null ||
              followUpRepository == null ||
              reminders == null
          ? const Scaffold()
          : AnimatedBuilder(
              animation: authStore!,
              builder: (context, _) => authStore!.isAuthenticated
                  ? PatientHomePage(
                      catalogRepository: catalogRepository!,
                      chatLauncher: chatLauncher,
                      videoLauncher: videoLauncher,
                      aiLauncher: aiLauncher,
                      resultGateway: resultGateway!,
                      followUpRepository: followUpRepository!,
                      reminders: reminders!,
                      onLogout: authStore!.logout,
                    )
                  : LoginPage(authStore: authStore!),
            ),
    );
  }
}
