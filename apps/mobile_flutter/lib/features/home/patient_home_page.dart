import 'package:flutter/material.dart';

import '../../core/account/contact_email_gateway.dart';
import '../../design/app_design.dart';
import '../account/contact_email_page.dart';
import '../ai_consultation/ai_session_launcher.dart';
import '../care/care_plan_page.dart';
import '../care/care_repository.dart';
import '../care/care_widgets.dart';
import '../care/check_in_pages.dart';
import '../care/consultation_history_page.dart';
import '../care/exercise_pages.dart';
import '../care/sharing_page.dart';
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
    body: IndexedStack(
      index: _index,
      children: [
        _TodayPage(
          repository: widget.careRepository,
          followUps: widget.followUpRepository,
          onOpenFollowUps: () => _open(_followUps()),
        ),
        _ConsultationLanding(
          onOpenHistory: () => _open(_history()),
          onOpenCatalog: () => _open(
            CatalogPage(
              repository: widget.catalogRepository,
              chatLauncher: widget.chatLauncher,
              videoLauncher: widget.videoLauncher,
              aiLauncher: widget.aiLauncher,
            ),
          ),
        ),
        _RecordsLanding(
          repository: widget.careRepository,
          onOpenCheckIns: () =>
              _open(CheckInListPage(repository: widget.careRepository)),
          onOpenTrends: () =>
              _open(TrendsPage(repository: widget.careRepository)),
          onOpenExercises: () =>
              _open(ExercisesPage(repository: widget.careRepository)),
          onOpenReports: () => _open(_history(reports: true)),
        ),
        _MineLanding(
          contactEmailGateway: widget.contactEmailGateway,
          careRepository: widget.careRepository,
          dataRightsGateway: widget.dataRightsGateway,
          dataExportSaver: widget.dataExportSaver,
          onOpen: _open,
          onOpenFollowUps: () => _open(_followUps()),
          onLogout: widget.onLogout,
        ),
      ],
    ),
    bottomNavigationBar: DecoratedBox(
      decoration: const BoxDecoration(
        border: Border(top: BorderSide(color: AppPalette.line)),
      ),
      child: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (value) => setState(() => _index = value),
        destinations: [
          NavigationDestination(
            icon: const Icon(Icons.today_outlined),
            selectedIcon: const Icon(Icons.today_rounded),
            label: careCopy('care.today'),
          ),
          NavigationDestination(
            icon: const Icon(Icons.forum_outlined),
            selectedIcon: const Icon(Icons.forum_rounded),
            label: careCopy('home.consultation'),
          ),
          NavigationDestination(
            icon: const Icon(Icons.assessment_outlined),
            selectedIcon: const Icon(Icons.assessment_rounded),
            label: careCopy('care.records'),
          ),
          NavigationDestination(
            icon: const Icon(Icons.person_outline),
            selectedIcon: const Icon(Icons.person_rounded),
            label: careCopy('care.mine'),
          ),
        ],
      ),
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
          .where((entry) => entry['date'] == careToday())
          .toList();
      final plans = careObjects(data.$2['items']);
      final todayEntry = entries.firstOrNull;
      final nextPlan = plans.firstOrNull;
      final tasks = nextPlan == null || nextPlan['tasks'] is! List
          ? const <CareJson>[]
          : careObjects(nextPlan['tasks']);
      final nextTask = tasks
          .where((task) => task['status'] == 'Pending')
          .firstOrNull;
      return RefreshIndicator(
        onRefresh: () async => reload(),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
          children: [
            AppEyebrow(careCopy('care.todayEyebrow')),
            const SizedBox(height: 8),
            Text(careToday(), style: Theme.of(context).textTheme.headlineLarge),
            const SizedBox(height: 22),
            AppPanel(
              color: todayEntry == null
                  ? AppPalette.surface
                  : const Color(0xFFF0F6F3),
              borderColor: todayEntry == null
                  ? AppPalette.line
                  : AppPalette.mint,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(
                        todayEntry == null
                            ? Icons.edit_note_outlined
                            : Icons.check_circle_outline_rounded,
                        color: AppPalette.pine,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          careCopy(
                            todayEntry == null
                                ? 'care.todayPending'
                                : 'care.todayRecorded',
                          ),
                          style: Theme.of(context).textTheme.titleLarge,
                        ),
                      ),
                      AppStatusBadge(
                        label: careCopy(
                          todayEntry == null
                              ? 'care.status.Pending'
                              : 'care.saved',
                        ),
                        tone: todayEntry == null
                            ? AppTone.warning
                            : AppTone.success,
                      ),
                    ],
                  ),
                  if (todayEntry != null) ...[
                    const SizedBox(height: 14),
                    Text(
                      '${careCopy('care.mood.${todayEntry['mood']}')}  ·  ${todayEntry['sleepHours']} ${careCopy('care.sleepShort')}',
                      style: Theme.of(context).textTheme.bodyLarge,
                    ),
                  ],
                  const SizedBox(height: 18),
                  FilledButton.icon(
                    onPressed: () async {
                      await Navigator.push(
                        context,
                        MaterialPageRoute<bool>(
                          builder: (_) => CheckInEditorPage(
                            repository: repository,
                            entry: todayEntry,
                          ),
                        ),
                      );
                      reload();
                    },
                    icon: Icon(
                      todayEntry == null
                          ? Icons.add_rounded
                          : Icons.edit_outlined,
                    ),
                    label: Text(
                      careCopy(
                        todayEntry == null ? 'care.recordToday' : 'care.edit',
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            AppSectionHeader(title: careCopy('care.nextTask')),
            if (nextPlan == null)
              AppNotice(
                text: careCopy('care.noImmediateTask'),
                icon: Icons.event_available_outlined,
              )
            else
              AppPanel(
                child: InkWell(
                  onTap: () async {
                    await Navigator.push(
                      context,
                      MaterialPageRoute<void>(
                        builder: (_) => CarePlanPage(
                          repository: repository,
                          planId: nextPlan['id'] as String,
                        ),
                      ),
                    );
                    reload();
                  },
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.route_outlined, color: AppPalette.pine),
                      const SizedBox(width: 14),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              nextPlan['title'] as String,
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                            const SizedBox(height: 6),
                            Text(
                              nextTask == null
                                  ? careCopy(
                                      'care.status.${nextPlan['status']}',
                                    )
                                  : '${nextTask['dueDate']} · ${careCopy('care.task.${nextTask['kind']}')}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ],
                        ),
                      ),
                      const Icon(Icons.chevron_right_rounded),
                    ],
                  ),
                ),
              ),
            const SizedBox(height: 24),
            _ActionRow(
              icon: Icons.event_note_outlined,
              title: careCopy('home.followUp'),
              subtitle: data.$3.isEmpty
                  ? careCopy('followUp.none')
                  : '${data.$3.length}',
              onTap: onOpenFollowUps,
            ),
            const SizedBox(height: 24),
            AppSectionHeader(title: careCopy('care.quickExercise')),
            _ActionRow(
              icon: Icons.spa_outlined,
              title: careCopy('care.exercises'),
              subtitle: careCopy('care.exerciseNotice'),
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
            const SizedBox(height: 22),
            AppNotice(
              text: careCopy('result.notDiagnosis'),
              icon: Icons.health_and_safety_outlined,
              tone: AppTone.warning,
            ),
          ],
        ),
      );
    },
  );
}

class _ConsultationLanding extends StatelessWidget {
  const _ConsultationLanding({
    required this.onOpenHistory,
    required this.onOpenCatalog,
  });

  final VoidCallback onOpenHistory;
  final VoidCallback onOpenCatalog;

  @override
  Widget build(BuildContext context) => _TabScaffold(
    title: careCopy('home.consultation'),
    description: careCopy('care.consultationIntro'),
    children: [
      _FeatureRow(
        icon: Icons.history_rounded,
        title: careCopy('care.history'),
        subtitle: careCopy('care.openConsultation'),
        primary: true,
        onTap: onOpenHistory,
      ),
      const SizedBox(height: 14),
      _FeatureRow(
        icon: Icons.medical_services_outlined,
        title: careCopy('catalog.title'),
        subtitle: careCopy('catalog.select'),
        onTap: onOpenCatalog,
      ),
    ],
  );
}

class _RecordsLanding extends StatefulWidget {
  const _RecordsLanding({
    required this.repository,
    required this.onOpenCheckIns,
    required this.onOpenTrends,
    required this.onOpenExercises,
    required this.onOpenReports,
  });

  final CareRepository repository;
  final VoidCallback onOpenCheckIns;
  final VoidCallback onOpenTrends;
  final VoidCallback onOpenExercises;
  final VoidCallback onOpenReports;

  @override
  State<_RecordsLanding> createState() => _RecordsLandingState();
}

class _RecordsLandingState extends State<_RecordsLanding> {
  late Future<List<CareJson>> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.repository.trends(7);
  }

  @override
  void didUpdateWidget(covariant _RecordsLanding oldWidget) {
    super.didUpdateWidget(oldWidget);
    _reload();
  }

  void _reload() {
    final future = widget.repository.trends(7);
    setState(() {
      _future = future;
    });
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: Text(careCopy('care.records')),
      actions: [
        IconButton(
          onPressed: _reload,
          tooltip: careCopy('care.refresh'),
          icon: const Icon(Icons.refresh_rounded),
        ),
      ],
    ),
    body: ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        AppEyebrow(careCopy('care.recordsSummary')),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: Text(
                careCopy('care.trends'),
                style: Theme.of(context).textTheme.headlineLarge,
              ),
            ),
            TextButton.icon(
              onPressed: widget.onOpenReports,
              icon: const Icon(Icons.description_outlined),
              label: Text(careCopy('care.reports')),
            ),
          ],
        ),
        const SizedBox(height: 8),
        Text(
          careCopy('care.recordsIntro'),
          style: Theme.of(context).textTheme.bodyMedium
              ?.copyWith(color: AppPalette.muted),
        ),
        const SizedBox(height: 20),
        FutureBuilder<List<CareJson>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const AppPanel(
                child: SizedBox(
                  height: 120,
                  child: Center(child: CircularProgressIndicator()),
                ),
              );
            }
            if (snapshot.hasError) {
              return AppPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    AppNotice(
                      text: careCopy('care.retry'),
                      icon: Icons.cloud_off_outlined,
                      tone: AppTone.warning,
                    ),
                    const SizedBox(height: 12),
                    TextButton.icon(
                      onPressed: _reload,
                      icon: const Icon(Icons.refresh_rounded),
                      label: Text(careCopy('care.refresh')),
                    ),
                  ],
                ),
              );
            }
            final days = snapshot.data ?? const <CareJson>[];
            final recorded = days.where((day) => day['mood'] != null).length;
            final exerciseCount = days.fold<int>(
              0,
              (total, day) => total + (day['exerciseCount'] as num).toInt(),
            );
            return AppPanel(
              child: Column(
                children: [
                  SizedBox(
                    height: 110,
                    child: CustomPaint(
                      painter: _TrendPreviewPainter(days),
                      child: const SizedBox.expand(),
                    ),
                  ),
                  const Divider(height: 28),
                  Row(
                    children: [
                      Expanded(
                        child: _Metric(
                          value: '$recorded / 7',
                          label: careCopy('care.recordedDays'),
                        ),
                      ),
                      const SizedBox(
                        height: 42,
                        child: VerticalDivider(width: 24),
                      ),
                      Expanded(
                        child: _Metric(
                          value: '$exerciseCount',
                          label: careCopy('care.exerciseTimes'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton.icon(
                      onPressed: widget.onOpenTrends,
                      icon: const Icon(Icons.show_chart_rounded),
                      label: Text(careCopy('care.viewAll')),
                    ),
                  ),
                ],
              ),
            );
          },
        ),
        const SizedBox(height: 24),
        AppSectionHeader(title: careCopy('care.records')),
        _ActionRow(
          icon: Icons.edit_note_outlined,
          title: careCopy('care.checkIn'),
          subtitle: careCopy('care.recordToday'),
          onTap: widget.onOpenCheckIns,
        ),
        const Divider(height: 1),
        _ActionRow(
          icon: Icons.spa_outlined,
          title: careCopy('care.exercises'),
          subtitle: careCopy('care.exerciseHistory'),
          onTap: widget.onOpenExercises,
        ),
      ],
    ),
  );
}

class _MineLanding extends StatelessWidget {
  const _MineLanding({
    required this.contactEmailGateway,
    required this.careRepository,
    required this.dataRightsGateway,
    required this.dataExportSaver,
    required this.onOpen,
    required this.onOpenFollowUps,
    required this.onLogout,
  });

  final ContactEmailGateway? contactEmailGateway;
  final CareRepository careRepository;
  final DataRightsGateway dataRightsGateway;
  final DataExportSaver dataExportSaver;
  final Future<void> Function(Widget page) onOpen;
  final VoidCallback onOpenFollowUps;
  final VoidCallback? onLogout;

  @override
  Widget build(BuildContext context) => _TabScaffold(
    title: careCopy('care.mine'),
    description: careCopy('care.recordsIntro'),
    children: [
      AppSectionHeader(title: careCopy('care.accountSection')),
      if (contactEmailGateway != null)
        _ActionRow(
          icon: Icons.mail_outline_rounded,
          title: careCopy('admin.account'),
          subtitle: careCopy('account.contactEmail.help'),
          onTap: () => onOpen(ContactEmailPage(gateway: contactEmailGateway!)),
        ),
      const SizedBox(height: 18),
      AppSectionHeader(title: careCopy('care.privacySection')),
      _ActionRow(
        icon: Icons.shield_outlined,
        title: careCopy('care.sharing'),
        subtitle: careCopy('care.notShared'),
        onTap: () => onOpen(SharingPage(repository: careRepository)),
      ),
      const Divider(height: 1),
      _ActionRow(
        icon: Icons.event_note_outlined,
        title: careCopy('home.followUp'),
        subtitle: careCopy('followUp.open'),
        onTap: onOpenFollowUps,
      ),
      const SizedBox(height: 18),
      AppSectionHeader(title: careCopy('care.dataSection')),
      _ActionRow(
        icon: Icons.folder_outlined,
        title: careCopy('home.dataRights'),
        subtitle: careCopy('dataRights.export'),
        onTap: () => onOpen(
          DataRightsPage(gateway: dataRightsGateway, saver: dataExportSaver),
        ),
      ),
      const SizedBox(height: 18),
      OutlinedButton.icon(
        onPressed: onLogout,
        icon: const Icon(Icons.logout_rounded),
        label: Text(careCopy('auth.logout')),
      ),
    ],
  );
}

class _TabScaffold extends StatelessWidget {
  const _TabScaffold({
    required this.title,
    required this.children,
    this.description,
  });

  final String title;
  final String? description;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(title)),
    body: ListView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 32),
      children: [
        if (description != null) ...[
          Text(
            description!,
            style: Theme.of(context).textTheme.bodyLarge
                ?.copyWith(color: AppPalette.muted),
          ),
          const SizedBox(height: 24),
        ],
        ...children,
      ],
    ),
  );
}

class _FeatureRow extends StatelessWidget {
  const _FeatureRow({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
    this.primary = false,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;
  final bool primary;

  @override
  Widget build(BuildContext context) => AppPanel(
    color: primary ? AppPalette.pine : AppPalette.surface,
    borderColor: primary ? AppPalette.pine : AppPalette.line,
    padding: EdgeInsets.zero,
    child: InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            Icon(icon, color: primary ? Colors.white : AppPalette.pine),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: primary ? Colors.white : AppPalette.ink,
                    ),
                  ),
                  const SizedBox(height: 5),
                  Text(
                    subtitle,
                    style: Theme.of(context).textTheme.bodySmall?.copyWith(
                      color: primary ? const Color(0xFFDCE9E4) : null,
                    ),
                  ),
                ],
              ),
            ),
            Icon(
              Icons.arrow_forward_rounded,
              color: primary ? Colors.white : AppPalette.pine,
            ),
          ],
        ),
      ),
    ),
  );
}

class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => InkWell(
    onTap: onTap,
    borderRadius: BorderRadius.circular(8),
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 42,
            height: 42,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: AppPalette.mint,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Icon(icon, color: AppPalette.pine, size: 22),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: Theme.of(context).textTheme.titleMedium),
                const SizedBox(height: 3),
                Text(
                  subtitle,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          const Padding(
            padding: EdgeInsets.only(top: 9),
            child: Icon(Icons.chevron_right_rounded, color: AppPalette.muted),
          ),
        ],
      ),
    ),
  );
}

class _Metric extends StatelessWidget {
  const _Metric({required this.value, required this.label});

  final String value;
  final String label;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        value,
        style: Theme.of(context).textTheme.headlineSmall
            ?.copyWith(fontFeatures: const [FontFeature.tabularFigures()]),
      ),
      const SizedBox(height: 4),
      Text(label, style: Theme.of(context).textTheme.bodySmall),
    ],
  );
}

class _TrendPreviewPainter extends CustomPainter {
  _TrendPreviewPainter(this.days);

  final List<CareJson> days;

  @override
  void paint(Canvas canvas, Size size) {
    final grid = Paint()
      ..color = AppPalette.line
      ..strokeWidth = 1;
    final line = Paint()
      ..color = AppPalette.pine
      ..strokeWidth = 2.4
      ..strokeCap = StrokeCap.round;
    for (var row = 0; row < 3; row++) {
      final y = 10 + row * (size.height - 20) / 2;
      canvas.drawLine(Offset(0, y), Offset(size.width, y), grid);
    }
    if (days.isEmpty) return;
    Offset? previous;
    for (var i = 0; i < days.length; i++) {
      final mood = days[i]['mood'] as num?;
      if (mood == null) {
        previous = null;
        continue;
      }
      final point = Offset(
        i * size.width / (days.length > 1 ? days.length - 1 : 1),
        10 + (5 - mood.toDouble()) * (size.height - 20) / 4,
      );
      if (previous != null) canvas.drawLine(previous, point, line);
      canvas.drawCircle(point, 4, line);
      previous = point;
    }
  }

  @override
  bool shouldRepaint(covariant _TrendPreviewPainter oldDelegate) =>
      oldDelegate.days != days;
}
