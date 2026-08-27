import 'package:flutter/material.dart';

import '../../core/account/contact_email_gateway.dart';
import '../account/contact_email_page.dart';
import '../ai_consultation/ai_session_launcher.dart';
import '../catalog/catalog_page.dart';
import '../catalog/catalog_repository.dart';
import '../consultation/chat_connection.dart';
import '../consultation/video_session_launcher.dart';
import '../data_rights/data_rights_page.dart';
import '../follow_ups/follow_up_list_page.dart';
import '../follow_ups/local_reminder_service.dart';
import '../results/analysis_progress_page.dart';
import '../care/care_repository.dart';
import '../care/care_widgets.dart';
import '../care/check_in_pages.dart';
import '../care/exercise_pages.dart';
import '../care/sharing_page.dart';
import '../care/care_plan_page.dart';
import '../care/consultation_history_page.dart';

class PatientHomePage extends StatefulWidget {
  const PatientHomePage({
    required this.catalogRepository,
    required this.careRepository,
    required this.resultGateway,
    required this.followUpRepository,
    required this.reminders,
    required this.dataRightsGateway,
    required this.dataExportSaver,
    this.contactEmailGateway,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    this.onLogout,
    super.key,
  });
  final CatalogRepository catalogRepository;
  final CareRepository careRepository;
  final ResultGateway resultGateway;
  final FollowUpRepository followUpRepository;
  final LocalReminderService reminders;
  final DataRightsGateway dataRightsGateway;
  final DataExportSaver dataExportSaver;
  final ContactEmailGateway? contactEmailGateway;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  final VoidCallback? onLogout;
  @override
  State<PatientHomePage> createState() => _PatientHomePageState();
}

class _PatientHomePageState extends State<PatientHomePage> {
  int _index = 0;
  Future<void> _open(Widget page) async {
    await Navigator.push(
      context,
      MaterialPageRoute<void>(builder: (_) => page),
    );
    if (mounted) setState(() {});
  }

  Widget _link(String key, IconData icon, Widget page) => ListTile(
    leading: Icon(icon),
    title: Text(careCopy(key)),
    trailing: const Icon(Icons.chevron_right),
    onTap: () => _open(page),
  );
  Widget _history({bool reports = false}) => ConsultationHistoryPage(
    repository: widget.careRepository,
    resultGateway: widget.resultGateway,
    reports: reports,
    chatLauncher: widget.chatLauncher,
    videoLauncher: widget.videoLauncher,
    aiLauncher: widget.aiLauncher,
  );
  Widget _followUps() => FollowUpListPage(
    repository: widget.followUpRepository,
    reminders: widget.reminders,
  );
  @override
  Widget build(BuildContext context) => Scaffold(
    body: switch (_index) {
      0 => _TodayPage(
        repository: widget.careRepository,
        followUps: widget.followUpRepository,
        onOpenFollowUps: () => _open(_followUps()),
      ),
      1 => Scaffold(
        appBar: AppBar(title: Text(careCopy('home.consultation'))),
        body: ListView(
          children: [
            _link('care.history', Icons.history, _history()),
            _link(
              'home.consultation',
              Icons.forum_outlined,
              CatalogPage(
                repository: widget.catalogRepository,
                chatLauncher: widget.chatLauncher,
                videoLauncher: widget.videoLauncher,
                aiLauncher: widget.aiLauncher,
              ),
            ),
          ],
        ),
      ),
      2 => Scaffold(
        appBar: AppBar(title: Text(careCopy('care.records'))),
        body: ListView(
          children: [
            _link(
              'care.checkIn',
              Icons.edit_note,
              CheckInListPage(repository: widget.careRepository),
            ),
            _link(
              'care.trends',
              Icons.show_chart,
              TrendsPage(repository: widget.careRepository),
            ),
            _link(
              'care.exercises',
              Icons.spa_outlined,
              ExercisesPage(repository: widget.careRepository),
            ),
            _link(
              'care.reports',
              Icons.description_outlined,
              _history(reports: true),
            ),
          ],
        ),
      ),
      _ => Scaffold(
        appBar: AppBar(title: Text(careCopy('care.mine'))),
        body: ListView(
          children: [
            if (widget.contactEmailGateway != null)
              _link(
                'admin.account',
                Icons.mail_outline,
                ContactEmailPage(gateway: widget.contactEmailGateway!),
              ),
            _link(
              'care.sharing',
              Icons.lock_outline,
              SharingPage(repository: widget.careRepository),
            ),
            _link('home.followUp', Icons.event_note_outlined, _followUps()),
            _link(
              'home.dataRights',
              Icons.folder_outlined,
              DataRightsPage(
                gateway: widget.dataRightsGateway,
                saver: widget.dataExportSaver,
              ),
            ),
            ListTile(
              leading: const Icon(Icons.logout),
              title: Text(careCopy('auth.logout')),
              onTap: widget.onLogout,
            ),
          ],
        ),
      ),
    },
    bottomNavigationBar: NavigationBar(
      selectedIndex: _index,
      onDestinationSelected: (value) => setState(() => _index = value),
      destinations: [
        NavigationDestination(
          icon: const Icon(Icons.today_outlined),
          label: careCopy('care.today'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.forum_outlined),
          label: careCopy('home.consultation'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.assessment_outlined),
          label: careCopy('care.records'),
        ),
        NavigationDestination(
          icon: const Icon(Icons.person_outline),
          label: careCopy('care.mine'),
        ),
      ],
    ),
  );
}

class _TodayPage extends StatelessWidget {
  const _TodayPage({
    required this.repository,
    required this.followUps,
    required this.onOpenFollowUps,
  });
  final CareRepository repository;
  final FollowUpRepository followUps;
  final VoidCallback onOpenFollowUps;
  @override
  Widget build(
    BuildContext context,
  ) => CarePageFrame<(CareJson, CareJson, List<UserFollowUp>)>(
    title: careCopy('care.today'),
    load: () async => (
      await repository.entries(1),
      await repository.plans(1),
      await followUps.listFollowUps(),
    ),
    builder: (data, reload) {
      final entries = careObjects(data.$1['items'])
          .where((entry) => entry['date'] == careToday());
      return ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(careToday(), style: Theme.of(context).textTheme.headlineSmall),
          const SizedBox(height: 16),
          FilledButton.icon(
            onPressed: () async {
              await Navigator.push(
                context,
                MaterialPageRoute<bool>(
                  builder: (_) => CheckInEditorPage(
                    repository: repository,
                    entry: entries.firstOrNull,
                  ),
                ),
              );
              reload();
            },
            icon: const Icon(Icons.edit_note),
            label: Text(
              careCopy(entries.isEmpty ? 'care.recordToday' : 'care.edit'),
            ),
          ),
          if (entries.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Text(
                '${careCopy('care.mood.${entries.first['mood']}')} · ${entries.first['sleepHours']} ${careCopy('care.sleepShort')}',
              ),
            ),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.spa_outlined),
            title: Text(careCopy('care.exercises')),
            trailing: const Icon(Icons.chevron_right),
            onTap: () async {
              await Navigator.push(
                context,
                MaterialPageRoute<void>(
                  builder: (_) => ExercisesPage(repository: repository),
                ),
              );
              reload();
            },
          ),
          const Divider(),
          Text(
            careCopy('care.plans'),
            style: Theme.of(context).textTheme.titleLarge,
          ),
          if ((data.$2['total'] as num) == 0) careNotice('care.noPlans'),
          ...careObjects(data.$2['items'])
              .take(3)
              .map(
                (plan) => ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: Text(plan['title'] as String),
                  subtitle: Text(careCopy('care.status.${plan['status']}')),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () async {
                    await Navigator.push(
                      context,
                      MaterialPageRoute<void>(
                        builder: (_) => CarePlanPage(
                          repository: repository,
                          planId: plan['id'] as String,
                        ),
                      ),
                    );
                    reload();
                  },
                ),
              ),
          TextButton(
            onPressed: () async {
              await Navigator.push(
                context,
                MaterialPageRoute<void>(
                  builder: (_) => CarePlanListPage(repository: repository),
                ),
              );
              reload();
            },
            child: Text(careCopy('care.plans')),
          ),
          const Divider(),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.event_note_outlined),
            title: Text(careCopy('home.followUp')),
            subtitle: Text('${data.$3.length}'),
            onTap: onOpenFollowUps,
          ),
          careNotice('result.notDiagnosis'),
        ],
      );
    },
  );
}
