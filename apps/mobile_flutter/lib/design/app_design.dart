import 'package:flutter/material.dart';

abstract final class AppPalette {
  static const pine = Color(0xFF174A42);
  static const pineLight = Color(0xFF24665B);
  static const mint = Color(0xFFDCE9E4);
  static const paper = Color(0xFFF5F3ED);
  static const surface = Color(0xFFFFFEFA);
  static const ink = Color(0xFF18211F);
  static const muted = Color(0xFF60706A);
  static const ochre = Color(0xFFB97932);
  static const danger = Color(0xFFA33A3A);
  static const line = Color(0xFFD7DDD9);
}

ThemeData buildMentalHealthTheme() {
  const scheme = ColorScheme.light(
    primary: AppPalette.pine,
    onPrimary: Colors.white,
    primaryContainer: AppPalette.mint,
    onPrimaryContainer: AppPalette.ink,
    secondary: AppPalette.ochre,
    onSecondary: Colors.white,
    surface: AppPalette.surface,
    onSurface: AppPalette.ink,
    error: AppPalette.danger,
    onError: Colors.white,
    outline: AppPalette.line,
    outlineVariant: Color(0xFFE6E9E6),
  );
  final base = ThemeData(
    colorScheme: scheme,
    scaffoldBackgroundColor: AppPalette.paper,
    useMaterial3: true,
    visualDensity: VisualDensity.standard,
  );
  final textTheme = base.textTheme.copyWith(
    headlineLarge: base.textTheme.headlineLarge?.copyWith(
      color: AppPalette.ink,
      fontSize: 30,
      height: 1.18,
      fontWeight: FontWeight.w700,
      letterSpacing: -0.8,
    ),
    headlineMedium: base.textTheme.headlineMedium?.copyWith(
      color: AppPalette.ink,
      fontSize: 26,
      height: 1.2,
      fontWeight: FontWeight.w700,
      letterSpacing: -0.5,
    ),
    headlineSmall: base.textTheme.headlineSmall?.copyWith(
      color: AppPalette.ink,
      fontSize: 23,
      height: 1.25,
      fontWeight: FontWeight.w700,
      letterSpacing: -0.35,
    ),
    titleLarge: base.textTheme.titleLarge?.copyWith(
      color: AppPalette.ink,
      fontSize: 20,
      height: 1.3,
      fontWeight: FontWeight.w700,
    ),
    titleMedium: base.textTheme.titleMedium?.copyWith(
      color: AppPalette.ink,
      fontSize: 17,
      height: 1.35,
      fontWeight: FontWeight.w600,
    ),
    bodyLarge: base.textTheme.bodyLarge?.copyWith(
      color: AppPalette.ink,
      fontSize: 16,
      height: 1.55,
    ),
    bodyMedium: base.textTheme.bodyMedium?.copyWith(
      color: AppPalette.ink,
      fontSize: 15,
      height: 1.5,
    ),
    bodySmall: base.textTheme.bodySmall?.copyWith(
      color: AppPalette.muted,
      fontSize: 13,
      height: 1.45,
    ),
    labelLarge: base.textTheme.labelLarge?.copyWith(
      fontSize: 15,
      fontWeight: FontWeight.w700,
      letterSpacing: 0,
    ),
  );
  const rounded = RoundedRectangleBorder(
    borderRadius: BorderRadius.all(Radius.circular(10)),
  );
  return base.copyWith(
    textTheme: textTheme,
    dividerColor: AppPalette.line,
    appBarTheme: AppBarTheme(
      backgroundColor: AppPalette.paper,
      foregroundColor: AppPalette.ink,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: textTheme.titleLarge,
      toolbarHeight: 68,
    ),
    cardTheme: const CardThemeData(
      color: AppPalette.surface,
      surfaceTintColor: Colors.transparent,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.all(Radius.circular(12)),
        side: BorderSide(color: AppPalette.line),
      ),
    ),
    navigationBarTheme: NavigationBarThemeData(
      height: 70,
      backgroundColor: AppPalette.surface,
      indicatorColor: AppPalette.mint,
      elevation: 0,
      labelTextStyle: WidgetStateProperty.resolveWith(
        (states) => textTheme.labelMedium?.copyWith(
          color: states.contains(WidgetState.selected)
              ? AppPalette.pine
              : AppPalette.muted,
          fontWeight: states.contains(WidgetState.selected)
              ? FontWeight.w700
              : FontWeight.w500,
        ),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        minimumSize: const Size(48, 50),
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 13),
        shape: rounded,
        textStyle: textTheme.labelLarge,
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppPalette.pine,
        minimumSize: const Size(48, 50),
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
        side: const BorderSide(color: AppPalette.pineLight),
        shape: rounded,
        textStyle: textTheme.labelLarge,
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppPalette.pine,
        minimumSize: const Size(48, 48),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        shape: rounded,
        textStyle: textTheme.labelLarge,
      ),
    ),
    iconButtonTheme: IconButtonThemeData(
      style: IconButton.styleFrom(
        foregroundColor: AppPalette.pine,
        minimumSize: const Size(48, 48),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: AppPalette.surface,
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 15),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppPalette.line),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppPalette.line),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppPalette.pine, width: 2),
      ),
      errorBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppPalette.danger),
      ),
      labelStyle: const TextStyle(color: AppPalette.muted),
      helperStyle: const TextStyle(color: AppPalette.muted, height: 1.45),
    ),
    chipTheme: base.chipTheme.copyWith(
      backgroundColor: AppPalette.surface,
      selectedColor: AppPalette.mint,
      side: const BorderSide(color: AppPalette.line),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
      labelStyle: const TextStyle(color: AppPalette.ink),
    ),
    snackBarTheme: const SnackBarThemeData(
      backgroundColor: AppPalette.ink,
      contentTextStyle: TextStyle(color: Colors.white),
      behavior: SnackBarBehavior.floating,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.all(Radius.circular(8)),
      ),
    ),
    dialogTheme: const DialogThemeData(
      backgroundColor: AppPalette.surface,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.all(Radius.circular(12)),
      ),
    ),
  );
}

enum AppTone { neutral, info, success, warning, danger }

class AppPanel extends StatelessWidget {
  const AppPanel({
    required this.child,
    this.padding = const EdgeInsets.all(18),
    this.color,
    this.borderColor,
    super.key,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color? color;
  final Color? borderColor;

  @override
  Widget build(BuildContext context) => Material(
    color: color ?? AppPalette.surface,
    clipBehavior: Clip.antiAlias,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(12),
      side: BorderSide(color: borderColor ?? AppPalette.line),
    ),
    child: Padding(padding: padding, child: child),
  );
}

class AppSectionHeader extends StatelessWidget {
  const AppSectionHeader({required this.title, this.action, super.key});

  final String title;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(2, 8, 2, 12),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Expanded(
          child: Text(title, style: Theme.of(context).textTheme.titleLarge),
        ),
        ?action,
      ],
    ),
  );
}

class AppEyebrow extends StatelessWidget {
  const AppEyebrow(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Text(
    text.toUpperCase(),
    style: Theme.of(context).textTheme.labelMedium?.copyWith(
      color: AppPalette.pine,
      fontWeight: FontWeight.w800,
      letterSpacing: 1.2,
    ),
  );
}

class AppStatusBadge extends StatelessWidget {
  const AppStatusBadge({
    required this.label,
    this.tone = AppTone.neutral,
    super.key,
  });

  final String label;
  final AppTone tone;

  @override
  Widget build(BuildContext context) {
    final (foreground, background) = switch (tone) {
      AppTone.info => (AppPalette.pine, AppPalette.mint),
      AppTone.success => (const Color(0xFF276344), const Color(0xFFE1EEE4)),
      AppTone.warning => (const Color(0xFF7A4A15), const Color(0xFFF5E7D2)),
      AppTone.danger => (AppPalette.danger, const Color(0xFFF7E1DF)),
      AppTone.neutral => (AppPalette.muted, const Color(0xFFECEFEB)),
    };
    return Semantics(
      label: label,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
          child: Text(
            label,
            style: Theme.of(context).textTheme.labelMedium
                ?.copyWith(color: foreground, fontWeight: FontWeight.w700),
          ),
        ),
      ),
    );
  }
}

class AppNotice extends StatelessWidget {
  const AppNotice({
    required this.text,
    this.icon = Icons.info_outline_rounded,
    this.tone = AppTone.info,
    super.key,
  });

  final String text;
  final IconData icon;
  final AppTone tone;

  @override
  Widget build(BuildContext context) {
    final (foreground, background, border) = switch (tone) {
      AppTone.warning => (
        const Color(0xFF7A4A15),
        const Color(0xFFFFF8ED),
        const Color(0xFFE4C79F),
      ),
      AppTone.danger => (
        AppPalette.danger,
        const Color(0xFFFFF4F3),
        const Color(0xFFE8B8B4),
      ),
      _ => (AppPalette.pine, const Color(0xFFF0F6F3), AppPalette.mint),
    };
    return DecoratedBox(
      decoration: BoxDecoration(
        color: background,
        border: Border(left: BorderSide(color: border, width: 3)),
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 20, color: foreground),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                text,
                style: Theme.of(context).textTheme.bodyMedium
                    ?.copyWith(color: foreground),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class AppStatePanel extends StatelessWidget {
  const AppStatePanel({
    required this.message,
    this.icon = Icons.auto_stories_outlined,
    this.action,
    super.key,
  });

  final String message;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) => Center(
    child: Padding(
      padding: const EdgeInsets.all(28),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 360),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Image.asset(
              'assets/images/empty-room.webp',
              height: 150,
              fit: BoxFit.contain,
              excludeFromSemantics: true,
              errorBuilder: (_, _, _) =>
                  Icon(icon, size: 54, color: AppPalette.pineLight),
            ),
            const SizedBox(height: 16),
            Text(
              message,
              textAlign: TextAlign.center,
              style: Theme.of(context).textTheme.bodyLarge,
            ),
            if (action != null) ...[const SizedBox(height: 18), action!],
          ],
        ),
      ),
    ),
  );
}

AppTone statusTone(String status) => switch (status) {
  'Completed' || 'Done' || 'Ready' => AppTone.success,
  'Active' || 'InProgress' || 'Scheduled' || 'Processing' => AppTone.info,
  'Overdue' || 'NeedsManual' => AppTone.danger,
  'Due' || 'Pending' || 'Proposed' || 'AwaitingConsent' => AppTone.warning,
  _ => AppTone.neutral,
};
