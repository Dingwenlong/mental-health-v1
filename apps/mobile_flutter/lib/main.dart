import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'core/account/contact_email_gateway.dart';
import 'core/api/api_client.dart';
import 'core/auth/auth_store.dart';
import 'core/network/demo_certificate_trust.dart';
import 'features/auth/login_page.dart';
import 'features/auth/captcha_runner.dart';
import 'features/ai_consultation/ai_session_launcher.dart';
import 'features/catalog/catalog_repository.dart';
import 'features/consultation/chat_connection.dart';
import 'features/consultation/video_session_launcher.dart';
import 'features/data_rights/data_rights_page.dart';
import 'features/follow_ups/follow_up_list_page.dart';
import 'features/follow_ups/local_reminder_service.dart';
import 'features/home/patient_home_page.dart';
import 'features/care/care_repository.dart';
import 'features/results/analysis_progress_page.dart';
import 'generated/ui_copy.g.dart';
import 'providers/speech/chime_fallback_speech_provider.dart';
import 'providers/speech/flutter_tts_speech_provider.dart';
import 'design/app_design.dart';

ThemeData buildAppTheme() => buildMentalHealthTheme();

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
      careRepository: CareRepository(client),
      chatLauncher: chatLauncher,
      videoLauncher: videoLauncher,
      aiLauncher: aiLauncher,
      resultGateway: ApiResultGateway(client),
      followUpRepository: ApiFollowUpRepository(client),
      reminders: DeviceLocalReminderService(),
      dataRightsGateway: ApiDataRightsGateway(client),
      dataExportSaver: const AndroidDownloadsDataExportSaver(),
      contactEmailGateway: ApiContactEmailGateway(client),
      captchaRunner: WebViewCaptchaRunner(apiBaseUri: client.baseUri),
    ),
  );
}

class MentalHealthApp extends StatelessWidget {
  const MentalHealthApp({
    this.authStore,
    this.catalogRepository,
    this.careRepository,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    this.resultGateway,
    this.followUpRepository,
    this.reminders,
    this.dataRightsGateway,
    this.dataExportSaver,
    this.contactEmailGateway,
    this.captchaRunner,
    super.key,
  });

  final AuthStore? authStore;
  final CatalogRepository? catalogRepository;
  final CareRepository? careRepository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  final ResultGateway? resultGateway;
  final FollowUpRepository? followUpRepository;
  final LocalReminderService? reminders;
  final DataRightsGateway? dataRightsGateway;
  final DataExportSaver? dataExportSaver;
  final ContactEmailGateway? contactEmailGateway;
  final CaptchaRunner? captchaRunner;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      locale: const Locale('zh', 'CN'),
      supportedLocales: const [Locale('zh', 'CN')],
      localizationsDelegates: GlobalMaterialLocalizations.delegates,
      title: UiCopy.get('app.title'),
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home:
          authStore == null ||
              catalogRepository == null ||
              careRepository == null ||
              resultGateway == null ||
              followUpRepository == null ||
              reminders == null ||
              dataRightsGateway == null ||
              dataExportSaver == null
          ? const Scaffold()
          : AnimatedBuilder(
              animation: authStore!,
              builder: (context, _) => authStore!.isAuthenticated
                  ? PatientHomePage(
                      catalogRepository: catalogRepository!,
                      careRepository: careRepository!,
                      chatLauncher: chatLauncher,
                      videoLauncher: videoLauncher,
                      aiLauncher: aiLauncher,
                      resultGateway: resultGateway!,
                      followUpRepository: followUpRepository!,
                      reminders: reminders!,
                      dataRightsGateway: dataRightsGateway!,
                      dataExportSaver: dataExportSaver!,
                      contactEmailGateway: contactEmailGateway,
                      onLogout: authStore!.logout,
                    )
                  : captchaRunner == null
                  ? const Scaffold()
                  : LoginPage(
                      authStore: authStore!,
                      captchaRunner: captchaRunner!,
                    ),
            ),
    );
  }
}
