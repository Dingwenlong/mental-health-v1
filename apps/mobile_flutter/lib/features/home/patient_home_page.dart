import 'package:flutter/material.dart';

import '../../generated/ui_copy.g.dart';
import '../ai_consultation/ai_session_launcher.dart';
import '../ai_consultation/demo_questionnaire_page.dart';
import '../catalog/catalog_page.dart';
import '../catalog/catalog_repository.dart';
import '../consultation/chat_connection.dart';
import '../consultation/video_session_launcher.dart';
import '../data_rights/data_rights_page.dart';
import '../follow_ups/follow_up_list_page.dart';
import '../follow_ups/local_reminder_service.dart';
import '../results/analysis_progress_page.dart';

class PatientHomePage extends StatefulWidget {
  const PatientHomePage({
    required this.catalogRepository,
    required this.resultGateway,
    required this.followUpRepository,
    required this.reminders,
    required this.dataRightsGateway,
    required this.dataExportSaver,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    this.onLogout,
    super.key,
  });

  final CatalogRepository catalogRepository;
  final ResultGateway resultGateway;
  final FollowUpRepository followUpRepository;
  final LocalReminderService reminders;
  final DataRightsGateway dataRightsGateway;
  final DataExportSaver dataExportSaver;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  final VoidCallback? onLogout;

  @override
  State<PatientHomePage> createState() => _PatientHomePageState();
}

class _PatientHomePageState extends State<PatientHomePage> {
  int _index = 0;

  @override
  Widget build(BuildContext context) => Scaffold(
    body: IndexedStack(
      index: _index,
      children: <Widget>[
        CatalogPage(
          repository: widget.catalogRepository,
          chatLauncher: widget.chatLauncher,
          videoLauncher: widget.videoLauncher,
          aiLauncher: widget.aiLauncher,
          onLogout: widget.onLogout,
        ),
        _ResultLookupPage(gateway: widget.resultGateway),
        FollowUpListPage(
          repository: widget.followUpRepository,
          reminders: widget.reminders,
        ),
        DataRightsPage(
          gateway: widget.dataRightsGateway,
          saver: widget.dataExportSaver,
        ),
      ],
    ),
    bottomNavigationBar: NavigationBar(
      selectedIndex: _index,
      onDestinationSelected: (value) => setState(() => _index = value),
      destinations: <NavigationDestination>[
        NavigationDestination(
          icon: const Icon(Icons.forum_outlined),
          selectedIcon: const Icon(Icons.forum),
          label: UiCopy.get('home.consultation'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.assessment_outlined),
          selectedIcon: const Icon(Icons.assessment),
          label: UiCopy.get('home.result'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.event_note_outlined),
          selectedIcon: const Icon(Icons.event_note),
          label: UiCopy.get('home.followUp'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.folder_outlined),
          selectedIcon: const Icon(Icons.folder),
          label: UiCopy.get('home.dataRights'),
        ),
      ],
    ),
  );
}

class _ResultLookupPage extends StatefulWidget {
  const _ResultLookupPage({required this.gateway});

  final ResultGateway gateway;

  @override
  State<_ResultLookupPage> createState() => _ResultLookupPageState();
}

class _ResultLookupPageState extends State<_ResultLookupPage> {
  final _sessionId = TextEditingController();
  String? _activeSessionId;

  @override
  void dispose() {
    _sessionId.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_activeSessionId case final sessionId?) {
      return AnalysisProgressPage(
        sessionId: sessionId,
        gateway: widget.gateway,
        onBack: () => setState(() => _activeSessionId = null),
      );
    }
    return Scaffold(
      appBar: AppBar(title: Text(UiCopy.get('result.title'))),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: <Widget>[
          TextField(
            controller: _sessionId,
            decoration: InputDecoration(
              labelText: UiCopy.get('result.sessionId'),
            ),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: _openResult,
            child: Text(UiCopy.get('result.lookup')),
          ),
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute<void>(
                builder: (routeContext) => DemoQuestionnairePage(
                  onSubmitted: (submission) => showDialog<void>(
                    context: routeContext,
                    builder: (dialogContext) => AlertDialog(
                      title: Text(UiCopy.get('questionnaire.completed')),
                      content: Text(
                        '${submission.score.round()}\n\n${UiCopy.get('result.notDiagnosis')}',
                      ),
                      actions: <Widget>[
                        TextButton(
                          onPressed: () => Navigator.of(dialogContext).pop(),
                          child: Text(UiCopy.get('common.confirm')),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            child: Text(UiCopy.get('questionnaire.open')),
          ),
        ],
      ),
    );
  }

  void _openResult() {
    final sessionId = _sessionId.text.trim();
    if (sessionId.isEmpty) return;
    setState(() => _activeSessionId = sessionId);
  }
}
