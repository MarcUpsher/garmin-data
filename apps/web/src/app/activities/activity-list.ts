import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivityService } from '../shared/activity.service';
import { ActivityPage, ActivityRow, SyncResult } from '../shared/activity.models';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatRadioModule } from '@angular/material/radio';
import { ConfirmHideActivityDialog } from './confirm-hide-activity-dialog';
import { activityTypeLabel } from '../shared/activity-type-label';
import { ThemeService } from '../shared/theme.service';

@Component({ imports: [DatePipe, RouterLink, RouterOutlet, MatButtonModule, MatCardModule, MatFormFieldModule, MatSelectModule, MatSnackBarModule, MatDialogModule, MatRadioModule], selector: 'app-activity-list', styleUrl: './activity-list.scss', templateUrl: './activity-list.html' })
export class ActivityList implements OnInit {
  private readonly service = inject(ActivityService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  protected readonly page = signal<ActivityPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly syncing = signal(false);
  protected readonly syncMessage = signal<string | null>(null);
  protected readonly selectedType = signal('running');
  protected readonly selectedDay = signal<number | ''>('');
  protected readonly showingHidden = signal(false);
  protected readonly updatingActivityId = signal<number | null>(null);
  private readonly theme = inject(ThemeService);
  protected readonly darkTheme = this.theme.darkTheme;
  protected readonly pageNumber = signal(1);
  protected readonly pageSize = 25;
  protected readonly Math = Math;

  ngOnInit(): void { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.service.list(this.pageNumber(), this.pageSize, this.selectedType(), this.selectedDay(), this.showingHidden()).subscribe({
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
    this.service.syncWatch().subscribe({
      next: (result) => {
        this.syncing.set(false);
        this.syncMessage.set(this.syncSummary(result));
        this.pageNumber.set(1);
        this.load();
      },
      error: (response: { error?: { message?: string; detail?: string } }) => {
        this.syncing.set(false);
        const message = response.error?.message ?? response.error?.detail ?? 'Could not sync the watch. Check that it is plugged in and mounted, then try again.';
        this.snackBar.open(message, 'Dismiss', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: ['sync-error-toast', this.darkTheme() ? 'dark' : 'light'],
          politeness: 'assertive',
        });
      },
    });
  }

  protected toggleTheme(): void { this.theme.toggle(); }

  private syncSummary(result: SyncResult): string {
    const imported = `${result.imported} new ${result.imported === 1 ? 'activity' : 'activities'}`;
    const skipped = `${result.skipped} already imported`;
    const scan = `checked ${result.filesScanned} files and hashed ${result.filesHashed}`;
    return result.failed > 0
      ? `Imported ${imported}; skipped ${skipped}; ${result.failed} file${result.failed === 1 ? '' : 's'} could not be read. ${scan}.${result.errors[0] ? ` First error: ${result.errors[0]}` : ''}`
      : `Sync complete: imported ${imported}, skipped ${skipped}; ${scan}.`;
  }

  protected changeType(type: string): void { this.selectedType.set(type); this.pageNumber.set(1); this.load(); }
  protected changeDay(day: number | ''): void { this.selectedDay.set(day); this.pageNumber.set(1); this.load(); }
  protected changeVisibility(showHidden: boolean): void {
    this.showingHidden.set(showHidden);
    this.pageNumber.set(1);
    this.load();
  }

  protected confirmHide(activity: ActivityRow): void {
    this.dialog.open(ConfirmHideActivityDialog, {
      data: { title: this.activityLabel(activity) },
      width: 'min(440px, calc(100vw - 32px))',
    }).afterClosed().subscribe((confirmed: boolean | undefined) => {
      if (confirmed) this.setActivityHidden(activity.id, true);
    });
  }

  protected setActivityHidden(id: number, isHidden: boolean): void {
    if (this.updatingActivityId() !== null) return;
    this.updatingActivityId.set(id);
    this.service.setActivityHidden(id, isHidden).subscribe({
      next: () => {
        this.updatingActivityId.set(null);
        this.snackBar.open(isHidden ? 'Activity moved to Hidden activities.' : 'Activity restored to visible activities.', undefined, { duration: 2400 });
        this.load();
      },
      error: () => {
        this.updatingActivityId.set(null);
        this.snackBar.open('Could not update this activity. Please try again.', 'Dismiss', {
          duration: 3000,
          horizontalPosition: 'end',
          verticalPosition: 'top',
          panelClass: ['sync-error-toast', this.darkTheme() ? 'dark' : 'light'],
          politeness: 'assertive',
        });
      },
    });
  }
  protected goToPage(page: number): void { this.pageNumber.set(page); this.load(); }
  protected pageCount(result: ActivityPage): number { return Math.max(1, Math.ceil(result.totalCount / result.pageSize)); }
  protected activityLabel(activity: ActivityRow): string { return activity.title || activityTypeLabel(activity.activityType); }
  protected activityTypeLabel = activityTypeLabel;

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
