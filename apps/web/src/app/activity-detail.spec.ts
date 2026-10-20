import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { ActivityDetail } from './activity-detail';

describe('ActivityDetail', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('shows lap metrics, totals, and aligned chart series', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/activities/42', ActivityDetail);
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
        { timestampUtc: '2026-10-18T09:00:00Z', distanceMeters: 0, speedMetersPerSecond: 3.2, heartRateBpm: 140, altitudeMeters: 25 },
        { timestampUtc: '2026-10-18T09:00:10Z', distanceMeters: 32, speedMetersPerSecond: 4, heartRateBpm: 150, altitudeMeters: 26 },
      ],
    });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const page = harness.routeNativeElement!;
    expect(page.querySelector('#detail-title')?.textContent).toContain('Morning run');
    expect(page.querySelector('.lap-table tbody')?.textContent).toContain('5:00 /km');
    expect(page.querySelector('.lap-table tbody')?.textContent).toContain('4:10 /km');
    expect(page.querySelector('.totals-grid')?.textContent).toContain('Max heart rate176 bpm');
    expect(page.querySelectorAll('.activity-chart polyline').length).toBe(3);
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
});
