import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AppShell } from './app-shell';
import { routes } from '../app.routes';
import { ActivityList } from '../activities/activity-list';
import { ThemeService } from '../shared/theme.service';

describe('AppShell routing', () => {
  beforeEach(async () => {
    localStorage.removeItem('garmin-theme');
    await TestBed.configureTestingModule({
      imports: [AppShell],
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('loads Trends and Imports without rendering or fetching the Activities page', async () => {
    const fixture = TestBed.createComponent(AppShell);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    await router.navigateByUrl('/trends');
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/trends/consistency').flush({
      period: '6m', bucket: 'month', rangeStart: '2026-04-01', rangeEnd: '2026-10-01', buckets: [],
    });
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(ActivityList))).toBeNull();
    expect(fixture.nativeElement.querySelector('a[aria-label="Trends"]').classList.contains('active')).toBe(true);
    http.expectNone(request => request.url === '/api/activities');

    await router.navigateByUrl('/data');
    fixture.detectChanges();
    http.expectOne('/api/wellness/status').flush({});
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(ActivityList))).toBeNull();
    expect(fixture.nativeElement.querySelector('a[aria-label="Data & Imports"]').classList.contains('active')).toBe(true);
    http.expectNone(request => request.url === '/api/activities');
    http.verify();
  });

  it('preserves the list and its filters while opening and closing the activity drawer', async () => {
    const fixture = TestBed.createComponent(AppShell);
    fixture.detectChanges();
    const router = TestBed.inject(Router);
    const http = TestBed.inject(HttpTestingController);
    const page = { items: [], totalCount: 0, page: 1, pageSize: 25 };
    await router.navigateByUrl('/');
    fixture.detectChanges();
    http.expectOne(request => request.url === '/api/activities').flush(page);
    fixture.detectChanges();
    const list = fixture.debugElement.query(By.directive(ActivityList)).componentInstance as any;
    list.changeDay(0);
    http.expectOne(request => request.url === '/api/activities' && request.params.get('dayOfWeek') === '0').flush(page);

    await router.navigateByUrl('/activities/42');
    fixture.detectChanges();
    http.expectOne('/api/activities/42').flush({ activity: { id: 42, title: 'Selected run' }, laps: [], records: [] });
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(ActivityList)).componentInstance).toBe(list);
    expect(list.selectedDay()).toBe(0);
    expect(fixture.nativeElement.querySelector('#detail-title')?.textContent).toContain('Selected run');
    http.expectNone(request => request.url === '/api/activities');

    await router.navigateByUrl('/');
    fixture.detectChanges();
    fixture.detectChanges();
    expect(fixture.debugElement.query(By.directive(ActivityList)).componentInstance).toBe(list);
    expect(fixture.nativeElement.querySelector('#detail-title')).toBeNull();
    http.verify();
  });

  it('applies theme changes to the shell surrounding every routed page', () => {
    const fixture = TestBed.createComponent(AppShell);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.app-shell').classList.contains('dark-theme')).toBe(true);
    TestBed.inject(ThemeService).toggle();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.app-shell').classList.contains('dark-theme')).toBe(false);
    expect(localStorage.getItem('garmin-theme')).toBe('light');
  });
});
