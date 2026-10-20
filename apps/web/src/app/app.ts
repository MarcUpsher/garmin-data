import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivityPage, ActivityRow, ActivityService, SyncResult } from './activity.service';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({ imports: [DatePipe, RouterLink, RouterOutlet], selector: 'app-root', styleUrl: './app.scss', templateUrl: './app.html' })
export class App implements OnInit {
  private readonly service = inject(ActivityService);
  protected readonly page = signal<ActivityPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly syncing = signal(false);
  protected readonly syncMessage = signal<string | null>(null);
  protected readonly syncError = signal<string | null>(null);
  protected readonly selectedType = signal('');
  protected readonly darkTheme = signal(this.loadThemePreference());
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = 25;
  protected readonly Math = Math;

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.list(this.pageNumber(), this.pageSize, this.selectedType()).subscribe({
      next: (result) => { this.page.set(result); this.loading.set(false); },
      error: () => {
        this.error.set('The local activity service is unavailable. Start the Garmin API and try again.');
        this.loading.set(false);
      },
    });
  }

  protected syncWatch(): void {
    this.syncing.set(true);
    this.syncMessage.set(null);
    this.syncError.set(null);
    this.service.syncWatch().subscribe({
      next: (result) => {
        this.syncing.set(false);
        this.syncMessage.set(this.syncSummary(result));
        this.pageNumber.set(1);
        this.load();
      },
      error: (response: { error?: { message?: string; detail?: string } }) => {
        this.syncing.set(false);
        this.syncError.set(response.error?.message ?? response.error?.detail ?? 'Could not sync the watch. Check that it is plugged in and mounted, then try again.');
      },
    });
  }

  protected toggleTheme(): void {
    const nextTheme = !this.darkTheme();
    this.darkTheme.set(nextTheme);
    try { localStorage.setItem('garmin-theme', nextTheme ? 'dark' : 'light'); } catch { /* Preference remains available for this session. */ }
  }

  private loadThemePreference(): boolean {
    try { return localStorage.getItem('garmin-theme') !== 'light'; } catch { return true; }
  }

  private syncSummary(result: SyncResult): string {
    const imported = `${result.imported} new ${result.imported === 1 ? 'activity' : 'activities'}`;
    const skipped = `${result.skipped} already imported`;
    const scan = `checked ${result.filesScanned} files and hashed ${result.filesHashed}`;
    return result.failed > 0
      ? `Imported ${imported}; skipped ${skipped}; ${result.failed} file${result.failed === 1 ? '' : 's'} could not be read. ${scan}.${result.errors[0] ? ` First error: ${result.errors[0]}` : ''}`
      : `Sync complete: imported ${imported}, skipped ${skipped}; ${scan}.`;
  }

  protected changeType(type: string): void { this.selectedType.set(type); this.pageNumber.set(1); this.load(); }
  protected goToPage(page: number): void { this.pageNumber.set(page); this.load(); }
  protected pageCount(result: ActivityPage): number { return Math.max(1, Math.ceil(result.totalCount / result.pageSize)); }
  protected activityLabel(activity: ActivityRow): string { return activity.title || activity.activityType || 'Activity'; }

  protected formatDistance(meters: number | null): string {
    return meters === null ? '—' : `${(meters / 1000).toFixed(2)} km`;
  }

  protected formatDuration(seconds: number | null): string {
    if (seconds === null || !Number.isFinite(seconds)) return '—';
    const total = Math.round(seconds);
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const remaining = total % 60;
    return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remaining).padStart(2, '0')}` : `${minutes}:${String(remaining).padStart(2, '0')}`;
  }

  protected formatPace(secondsPerKm: number | null): string {
    if (secondsPerKm === null || !Number.isFinite(secondsPerKm)) return '—';
    const rounded = Math.round(secondsPerKm);
    return `${Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, '0')} /km`;
  }
}
