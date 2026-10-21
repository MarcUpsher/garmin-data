import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivityService, WellnessImportResult, WellnessImportStatus } from './activity.service';

@Component({
  selector: 'app-data-management',
  imports: [DatePipe, RouterLink],
  styleUrl: './data-management.scss',
  templateUrl: './data-management.html',
})
export class DataManagement implements OnInit {
  private readonly service = inject(ActivityService);
  protected readonly directory = signal(this.loadDirectory());
  protected readonly status = signal<WellnessImportStatus | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<WellnessImportResult | null>(null);

  ngOnInit(): void { this.refresh(); }

  protected setDirectory(event: Event): void {
    const path = (event.target as HTMLInputElement).value;
    this.directory.set(path);
    try { localStorage.setItem('garmin-connect-export-directory', path); } catch { /* Keep the path for this session. */ }
  }

  protected refresh(): void {
    this.service.getWellnessStatus().subscribe({ next: value => this.status.set(value), error: () => this.status.set(null) });
  }

  protected importWellness(): void {
    if (!this.directory().trim()) { this.error.set('Enter the path to the DI_CONNECT folder in your Garmin Connect export.'); return; }
    this.busy.set(true); this.error.set(null); this.result.set(null);
    this.service.importWellness(this.directory().trim()).subscribe({
      next: value => { this.result.set(value); this.busy.set(false); this.refresh(); },
      error: err => { this.busy.set(false); this.error.set(err?.error?.message ?? err?.error ?? 'The Garmin export could not be imported.'); },
    });
  }

  private loadDirectory(): string {
    try { return localStorage.getItem('garmin-connect-export-directory') ?? ''; } catch { return ''; }
  }
}
