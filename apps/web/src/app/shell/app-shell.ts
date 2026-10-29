import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivityService } from '../shared/activity.service';
import { ThemeService } from '../shared/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss',
})
export class AppShell implements OnInit {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly darkTheme = inject(ThemeService).darkTheme;
  protected readonly activityCount = inject(ActivityService).listCount;
  protected readonly isTrends = signal(false);
  protected readonly isData = signal(false);

  ngOnInit(): void {
    this.updateNavigation(this.router.url);
    this.router.events.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(event => {
      if (event instanceof NavigationEnd) this.updateNavigation(event.urlAfterRedirects);
    });
  }

  private updateNavigation(url: string): void {
    const path = url.split('?')[0].split('#')[0];
    this.isTrends.set(path === '/trends');
    this.isData.set(path === '/data');
  }
}
