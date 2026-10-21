import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RunningTrends } from './running-trends';

describe('RunningTrends', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RunningTrends],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('loads continuous distance buckets and drills into the selected period', async () => {
    const fixture = TestBed.createComponent(RunningTrends);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne(call => call.url === '/api/trends/consistency'
      && call.params.get('period') === '6m' && call.params.get('bucket') === 'month');
    request.flush({
      period: '6m', bucket: 'month', rangeStart: '2026-04-21', rangeEnd: '2026-10-22',
      buckets: [
        { startDate: '2026-04-21', endDate: '2026-05-01', label: '2026-04', distanceKm: 0, runCount: 0, totalTimerTimeSeconds: 0 },
        { startDate: '2026-05-01', endDate: '2026-06-01', label: '2026-05', distanceKm: 12.5, runCount: 3, totalTimerTimeSeconds: 3900 },
      ],
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const page = fixture.nativeElement as HTMLElement;
    expect(page.querySelectorAll('.bar-hit-area').length).toBe(2);
    expect(page.querySelector('.distance-bar.zero-bar')).not.toBeNull();
    expect(Array.from(page.querySelectorAll('.grid-lines text')).map(node => node.textContent?.trim())).toEqual(['0', '5', '10', '15']);

    page.querySelector<SVGElement>('.bar-hit-area')!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    const detailRequest = http.expectOne(call => call.url === '/api/trends/consistency/activities'
      && call.params.get('startDate') === '2026-04-21'
      && call.params.get('endDate') === '2026-05-01');
    detailRequest.flush([]);
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page.textContent).toContain('No running activities were recorded in this period.');
    http.verify();
  });

  it('uses continuous time buckets and formats run duration', async () => {
    const fixture = TestBed.createComponent(RunningTrends);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({
      period: '6m', bucket: 'month', rangeStart: '2026-04-21', rangeEnd: '2026-10-22',
      buckets: [{ startDate: '2026-05-01', endDate: '2026-06-01', label: '2026-05', distanceKm: 12.5, runCount: 3, totalTimerTimeSeconds: 3900 }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    page.querySelector('.bar-hit-area')?.dispatchEvent(new Event('pointerenter', { bubbles: true }));
    fixture.detectChanges();
    expect(page.textContent).toContain('12.5 km');
    expect(page.textContent).toContain('3 runs');
    expect(page.textContent).toContain('1:05:00 timer time');
    http.verify();
  });

  it('keeps fractional kilometre ticks readable when distances are small', async () => {
    const fixture = TestBed.createComponent(RunningTrends);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({
      period: '6m', bucket: 'month', rangeStart: '2026-04-21', rangeEnd: '2026-10-22',
      buckets: [{ startDate: '2026-05-01', endDate: '2026-06-01', label: '2026-05', distanceKm: 0.6, runCount: 1, totalTimerTimeSeconds: 600 }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    expect(Array.from(page.querySelectorAll('.grid-lines text')).map(node => node.textContent?.trim())).toEqual(['0.0', '0.2', '0.4', '0.6']);
    http.verify();
  });

  it('loads performance efforts and rebuilds the cached results', async () => {
    const fixture = TestBed.createComponent(RunningTrends);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({
      period: '6m', bucket: 'month', rangeStart: '2026-04-21', rangeEnd: '2026-10-22', buckets: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[1].click();
    const performanceRequest = http.expectOne(call => call.url === '/api/trends/performance'
      && call.params.get('distanceMeters') === '5000' && call.params.get('period') === '6m');
    performanceRequest.flush({
      period: '6m', distanceMeters: 5000, rangeStart: '2026-04-21', rangeEnd: '2026-10-22', personalBestCount: 1,
      points: [{ activityId: 7, startedAt: '2026-08-01T09:00:00Z', title: 'Morning Run', distanceMeters: 5000,
        effortKind: 'RollingSegment', durationSeconds: 1500, startElapsedSeconds: 600, endElapsedSeconds: 2100,
        observationCount: 90, algorithmVersion: 1, isPersonalBest: true }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page.textContent).toContain('25:00');
    expect(page.textContent).toContain('5:00 /km');
    expect(page.textContent).toContain('★ New best');
    const paceTicks = Array.from(page.querySelectorAll('.performance-chart .grid-lines text')).map(node => node.textContent?.trim());
    expect(paceTicks[0]).toBe('4:54 /km');
    expect(paceTicks.at(-1)).toBe('5:06 /km');
    page.querySelector('.performance-chart a')!.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    fixture.detectChanges();
    const detailRequest = http.expectOne('/api/activities/7');
    detailRequest.flush({
      activity: { id: 7, startedAt: '2026-08-01T09:00:00Z', activityType: 'running', title: 'Morning Run',
        distanceMeters: 5000, elapsedTimeSeconds: 1500, movingTimeSeconds: 1500, averageHeartRateBpm: 150,
        maximumHeartRateBpm: 170, averagePaceSecondsPerKilometer: 300, bestPaceSecondsPerKilometer: 260,
        averageSpeedMetersPerSecond: 3.33, maximumSpeedMetersPerSecond: 3.85, calories: 300, lapCount: 0,
        totalAscentMeters: 20, source: 'FIT', hasRecordSamples: false },
      laps: [], records: [],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page.querySelector('.detail-panel')).not.toBeNull();
    expect(page.textContent).toContain('Morning Run');
    page.querySelector<HTMLButtonElement>('.close-button')!.click();
    fixture.detectChanges();
    expect(page.querySelector('.detail-panel')).toBeNull();

    page.querySelector<HTMLButtonElement>('.update-efforts')!.click();
    http.expectOne({ method: 'POST', url: '/api/trends/performance/rebuild' }).flush({
      activitiesProcessed: 10, effortsUpdated: 25, algorithmVersion: 1,
    });
    const refreshed = http.expectOne(call => call.url === '/api/trends/performance');
    refreshed.flush({
      period: '6m', distanceMeters: 5000, rangeStart: '2026-04-21', rangeEnd: '2026-10-22', personalBestCount: 1,
      points: [{ activityId: 7, startedAt: '2026-08-01T09:00:00Z', title: 'Morning Run', distanceMeters: 5000,
        effortKind: 'RollingSegment', durationSeconds: 1500, startElapsedSeconds: 600, endElapsedSeconds: 2100,
        observationCount: 90, algorithmVersion: 1, isPersonalBest: true }],
    });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(page.textContent).toContain('Updated 25 efforts from 10 runs.');
    http.verify();
  });
});
