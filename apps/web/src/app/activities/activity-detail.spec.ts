import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject } from 'rxjs';
import { routes } from '../app.routes';
import { ActivityDetail } from './activity-detail';
import { ActivityService } from '../shared/activity.service';


describe('ActivityDetail', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('shows lap metrics, totals, and aligned run-profile series', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/activity/42', ActivityDetail);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/activities/42').flush({
      activity: {
        id: 42, startedAt: '2026-10-18T09:00:00Z', activityType: 'running', title: 'Morning run',
        distanceMeters: 5000, elapsedTimeSeconds: 1500, movingTimeSeconds: 1480,
        averageHeartRateBpm: 150, maximumHeartRateBpm: 176, averagePaceSecondsPerKilometer: 300,
        bestPaceSecondsPerKilometer: 250, averageSpeedMetersPerSecond: 3.33,
        maximumSpeedMetersPerSecond: 4, calories: 350, lapCount: 1, totalAscentMeters: 24,
        source: 'FIT', hasRecordSamples: true,
      },
      laps: [{ lapNumber: 1, startedAtUtc: '2026-10-18T09:00:00Z', elapsedTimeSeconds: 300,
        timerTimeSeconds: 298, distanceMeters: 1000, averageHeartRateBpm: 150, maximumHeartRateBpm: 170,
        averageSpeedMetersPerSecond: 3.33, maximumSpeedMetersPerSecond: 4 }],
      records: [
        { recordNumber: 0, timestampUtc: '2026-10-18T09:00:00Z', distanceMeters: 0, speedMetersPerSecond: 3.2, heartRateBpm: 140, altitudeMeters: 25 },
        { recordNumber: 1, timestampUtc: '2026-10-18T09:00:10Z', distanceMeters: 32, speedMetersPerSecond: 4, heartRateBpm: 150, altitudeMeters: 26 },
      ],
    });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const page = harness.routeNativeElement!;
    expect(page.querySelector('#detail-title')?.textContent).toContain('Morning run');
    expect(page.querySelector('.lap-table tbody')?.textContent).toContain('5:00 /km');
    expect(page.querySelector('.lap-table tbody')?.textContent).toContain('4:10 /km');
    expect(page.querySelector('.totals-grid')?.textContent).toContain('Max heart rate176 bpm');
    expect(page.querySelectorAll('.activity-chart .chart-line').length).toBe(2);
    expect(page.querySelector('.activity-chart .elevation-area')).not.toBeNull();
    expect(page.querySelector('.axis-toggle')?.textContent).toContain('Distance');
    http.verify();
  });

  it('handles older activities with no laps or record samples', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/activity/19', ActivityDetail);
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/activities/19').flush({
      activity: {
        id: 19, startedAt: null, activityType: 'running', title: 'Older run', distanceMeters: null,
        elapsedTimeSeconds: null, movingTimeSeconds: null, averageHeartRateBpm: null, maximumHeartRateBpm: null,
        averagePaceSecondsPerKilometer: null, bestPaceSecondsPerKilometer: null,
        averageSpeedMetersPerSecond: null, maximumSpeedMetersPerSecond: null,
        calories: null, lapCount: 0, totalAscentMeters: null, source: 'SUMMARY', hasRecordSamples: false,
      },
      laps: [], records: [],
    });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const page = harness.routeNativeElement!;
    expect(page.textContent).toContain('No lap splits were recorded');
    expect(page.textContent).toContain('No heart rate, pace, or elevation samples are available');
    expect(page.querySelector('.totals-grid')?.textContent).toContain('Distance—');
    http.verify();
  });

  it('keeps the selected run and clears section data when an older request completes', () => {
    const earlier = new Subject<any>();
    const current = new Subject<any>();
    TestBed.overrideProvider(ActivityService, { useValue: {
      get: (id: number) => id === 1 ? earlier : current,
    } });
    const detail = TestBed.runInInjectionContext(() => new ActivityDetail()) as any;
    detail.segment.set({ averageHeartRateBpm: 150 });
    detail.selection.set({ startX: 100, endX: 200 });
    detail.loadActivity(1);
    detail.loadActivity(2);
    expect(earlier.observed).toBe(false);
    current.next({ activity: { id: 2 }, laps: [], records: [] });
    earlier.next({ activity: { id: 1 }, laps: [], records: [] });
    expect(detail.data().activity.id).toBe(2);
    expect(detail.segment()).toBeNull();
    expect(detail.selection()).toBeNull();
  });

  it('cancels pending section statistics when the selection is cleared', () => {
    const pending = new Subject<any>();
    TestBed.overrideProvider(ActivityService, { useValue: {
      getSegmentSummary: () => pending,
    } });
    const detail = TestBed.runInInjectionContext(() => new ActivityDetail()) as any;
    detail.data.set({ activity: { id: 2 }, records: [] });
    detail.chart = () => ({ points: [
      { recordNumber: 0, x: 100 }, { recordNumber: 1, x: 200 },
    ] });
    detail.pointerX = (event: { clientX: number }) => event.clientX;
    detail.dragStart = 100;
    detail.selection.set({ startX: 100, endX: 200 });
    detail.chartPointerUp({ clientX: 200 } as PointerEvent);
    expect(pending.observed).toBe(true);
    detail.clearSelection();
    expect(pending.observed).toBe(false);
    pending.next({ averageHeartRateBpm: 150 });
    expect(detail.segment()).toBeNull();
    expect(detail.segmentLoading()).toBe(false);
  });

  it('plots samples that lack timestamps using elapsed time', () => {
    const detail = TestBed.runInInjectionContext(() => new ActivityDetail()) as any;
    const chart = detail.buildChart({ activity: { elapsedTimeSeconds: 60 }, records: [
      { recordNumber: 0, timestampUtc: null, distanceMeters: 0, speedMetersPerSecond: null, heartRateBpm: null, altitudeMeters: null },
      { recordNumber: 1, timestampUtc: null, distanceMeters: 100, speedMetersPerSecond: null, heartRateBpm: null, altitudeMeters: null },
    ] }, 'time');
    expect(chart.points).toHaveLength(2);
    expect(chart.points[0].x).toBeLessThan(chart.points[1].x);
  });
});
