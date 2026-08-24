import 'package:flutter/material.dart';

import '../../core/api/api_client.dart';
import '../../generated/ui_copy.g.dart';
import '../ai_consultation/ai_session_launcher.dart';
import 'catalog_repository.dart';
import '../consultation/chat_connection.dart';
import '../consultation/video_session_launcher.dart';
import 'consent_page.dart';
import 'service_plan_card.dart';

class CatalogPage extends StatefulWidget {
  const CatalogPage({
    required this.repository,
    this.chatLauncher,
    this.videoLauncher,
    this.aiLauncher,
    this.onLogout,
    super.key,
  });

  final CatalogRepository repository;
  final ChatSessionLauncher? chatLauncher;
  final VideoSessionLauncher? videoLauncher;
  final AiSessionLauncher? aiLauncher;
  final VoidCallback? onLogout;

  @override
  State<CatalogPage> createState() => _CatalogPageState();
}

class _CatalogPageState extends State<CatalogPage> {
  late Future<List<ServicePlanSummary>> _plans;

  @override
  void initState() {
    super.initState();
    _plans = widget.repository.listPlans();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(UiCopy.get('catalog.title')),
        actions: widget.onLogout == null
            ? null
            : <Widget>[
                TextButton(
                  onPressed: widget.onLogout,
                  child: Text(UiCopy.get('auth.logout')),
                ),
              ],
      ),
      body: FutureBuilder<List<ServicePlanSummary>>(
        future: _plans,
        builder: (context, snapshot) {
          if (snapshot.connectionState != ConnectionState.done) {
            return const Center(child: CircularProgressIndicator());
          }
          if (snapshot.hasError) {
            final error = snapshot.error;
            final copyKey =
                error is ApiFailure && error.code == 'FORBIDDEN_RESOURCE'
                ? 'error.forbidden'
                : 'error.retry';
            return Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: <Widget>[
                  Text(UiCopy.get(copyKey)),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _reload,
                    child: Text(UiCopy.get('catalog.retry')),
                  ),
                ],
              ),
            );
          }
          final plans = snapshot.data ?? const <ServicePlanSummary>[];
          if (plans.isEmpty) {
            return Center(child: Text(UiCopy.get('catalog.noPlans')));
          }
          return ListView.separated(
            padding: const EdgeInsets.all(16),
            itemCount: plans.length,
            separatorBuilder: (_, _) => const SizedBox(height: 8),
            itemBuilder: (context, index) {
              final plan = plans[index];
              return ServicePlanCard(
                plan: plan,
                onSelected: () => Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => ConsentPage(
                      plan: plan,
                      repository: widget.repository,
                      chatLauncher: widget.chatLauncher,
                      videoLauncher: widget.videoLauncher,
                      aiLauncher: widget.aiLauncher,
                    ),
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }

  void _reload() {
    setState(() => _plans = widget.repository.listPlans());
  }
}
