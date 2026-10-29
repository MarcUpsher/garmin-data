import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly darkTheme = signal(this.loadPreference());

  toggle(): void {
    this.darkTheme.update(dark => !dark);
    try { localStorage.setItem('garmin-theme', this.darkTheme() ? 'dark' : 'light'); } catch { /* Session preference remains available. */ }
  }

  private loadPreference(): boolean {
    try { return localStorage.getItem('garmin-theme') !== 'light'; } catch { return true; }
  }
}
