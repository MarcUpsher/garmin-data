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

  it('keeps inactive time visible and scales VO₂ Max to the observed values', async () => {
    const fixture = TestBed.createComponent(RunningTrends);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({ buckets: [] });
    const page = fixture.nativeElement as HTMLElement;
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[3].click();
    http.expectOne(call => call.url === '/api/trends/vo2-max').flush({
      rangeStart: '2026-04-21', rangeEnd: '2026-10-22', recordCount: 1,
      points: [{ id: 1, date: '2026-05-01', value: 52, sport: 'RUNNING', deviceId: 'test' }],
    });
    await fixture.whenStable(); fixture.detectChanges();
    const point = page.querySelector<SVGCircleElement>('.metric-chart circle')!;
    expect(Number(point.getAttribute('cx'))).toBeLessThan(200);
    expect(Array.from(page.querySelectorAll('.metric-chart .grid-lines text')).map(x => x.textContent?.trim()))
      .toEqual(['51.0', '51.5', '52.0', '52.5', '53.0']);
    expect(page.querySelector('.metric-chart')?.textContent).toContain('Oct');
    http.verify();
  });

  it('groups repeated VO₂ rows while preserving distinct devices and values', async () => {
    const fixture = TestBed.createComponent(RunningTrends); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({ buckets: [] });
    const page = fixture.nativeElement as HTMLElement;
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[3].click();
    const point = { date: '2026-05-01', value: 52, sport: 'RUNNING', deviceId: 'test', subSport: null, isCalibrated: true };
    http.expectOne(call => call.url === '/api/trends/vo2-max').flush({
      rangeStart: '2026-04-21', rangeEnd: '2026-10-22', recordCount: 4,
      points: [{ ...point, id: 1 }, { ...point, id: 2 }, { ...point, id: 3, deviceId: 'another' }, { ...point, id: 4, value: 51 }],
    });
    await fixture.whenStable(); fixture.detectChanges();
    const points = [...page.querySelectorAll('.metric-chart circle')];
    expect(points).toHaveLength(3);
    expect(new Set(points.map(p => p.getAttribute('cx'))).size).toBe(3);
    expect(page.querySelector('.metric-chart')?.textContent).toContain('grouped 2');
    http.verify();
  });

  it('sizes monthly sleep bars to fit years of history', async () => {
    const fixture = TestBed.createComponent(RunningTrends); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({ buckets: [] });
    const page = fixture.nativeElement as HTMLElement;
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[2].click();
    http.expectOne(call => call.url === '/api/trends/sleep').flush({
      rangeStart: '2020-01-01', rangeEnd: '2026-10-22', recordCount: 2,
      points: ['2020-01-01', '2020-02-01'].map(month => ({ month, averageSleepHours: 7, nightCount: 1, excludedNightCount: 0, averageDeepSeconds: 3600, averageLightSeconds: 21600, averageRemSeconds: null, averageRespiration: null })),
    });
    await fixture.whenStable(); fixture.detectChanges();
    const bars = [...page.querySelectorAll('.metric-bar')];
    expect(bars).toHaveLength(2);
    expect(Number(bars[0].getAttribute('x')) + Number(bars[0].getAttribute('width')))
      .toBeLessThan(Number(bars[1].getAttribute('x')));
    expect(page.querySelector('.metric-chart')?.textContent).toContain('REM —');
    http.verify();
  });

  it('cancels the previous metric request when switching views', async () => {
    const fixture = TestBed.createComponent(RunningTrends); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(call => call.url === '/api/trends/consistency').flush({ buckets: [] });
    const page = fixture.nativeElement as HTMLElement;
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[2].click();
    const sleep = http.expectOne(call => call.url === '/api/trends/sleep');
    page.querySelectorAll<HTMLButtonElement>('.view-tabs button')[3].click();
    expect(sleep.cancelled).toBe(true);
    http.expectOne(call => call.url === '/api/trends/vo2-max').flush({ points: [], recordCount: 0, rangeStart: '2026-04-21', rangeEnd: '2026-10-22' });
    await fixture.whenStable(); fixture.detectChanges();
    expect(page.textContent).toContain('No VO₂ Max records in this range');
    http.verify();
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
