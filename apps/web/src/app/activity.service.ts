import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface ActivityRow {
  id: number;
  startedAt: string | null;
  activityType: string | null;
  title: string | null;
  distanceMeters: number | null;
  elapsedTimeSeconds: number | null;
  movingTimeSeconds: number | null;
  averageHeartRateBpm: number | null;
  maximumHeartRateBpm: number | null;
  averagePaceSecondsPerKilometer: number | null;
  calories: number | null;
  lapCount: number;
  source: string;
  hasRecordSamples: boolean;
}

export interface ActivityLap {
  lapNumber: number;
  startedAtUtc: string | null;
  elapsedTimeSeconds: number | null;
  timerTimeSeconds: number | null;
  distanceMeters: number | null;
  averageHeartRateBpm: number | null;
  maximumHeartRateBpm: number | null;
  averageSpeedMetersPerSecond: number | null;
  maximumSpeedMetersPerSecond: number | null;
}

export interface ActivitySample {
  recordNumber: number;
  timestampUtc: string | null;
  distanceMeters: number | null;
  speedMetersPerSecond: number | null;
  heartRateBpm: number | null;
  altitudeMeters: number | null;
}

export interface ActivitySegmentSummary {
  startElapsedSeconds: number | null;
  endElapsedSeconds: number | null;
  startDistanceMeters: number | null;
  endDistanceMeters: number | null;
  distanceMeters: number | null;
  elevationGainMeters: number | null;
  elevationLossMeters: number | null;
  netElevationChangeMeters: number | null;
  averagePaceSecondsPerKilometer: number | null;
  startHeartRateBpm: number | null;
  endHeartRateBpm: number | null;
  peakHeartRateBpm: number | null;
  averageHeartRateBpm: number | null;
  durationSeconds: number | null;
}

export interface ActivityDetails extends ActivityRow {
  averagePaceSecondsPerKilometer: number | null;
  bestPaceSecondsPerKilometer: number | null;
  averageSpeedMetersPerSecond: number | null;
  maximumSpeedMetersPerSecond: number | null;
  totalAscentMeters: number | null;
}

export interface ActivityDetailResponse {
  activity: ActivityDetails;
  laps: ActivityLap[];
  records: ActivitySample[];
}

export interface ActivityPage { items: ActivityRow[]; totalCount: number; page: number; pageSize: number; }
export interface RunningConsistencyBucket {
  startDate: string;
  endDate: string;
  label: string;
  distanceKm: number;
  runCount: number;
  totalTimerTimeSeconds: number;
}
export interface RunningConsistencyResponse {
  period: '6m' | '1y' | 'all';
  bucket: 'month' | 'week';
  rangeStart: string;
  rangeEnd: string;
  buckets: RunningConsistencyBucket[];
}
export interface RunningPerformancePoint {
  activityId: number;
  startedAt: string;
  title: string | null;
  distanceMeters: number;
  effortKind: 'RollingSegment' | 'RecordedActivity';
  durationSeconds: number;
  startElapsedSeconds: number;
  endElapsedSeconds: number;
  observationCount: number;
  algorithmVersion: number;
  isPersonalBest: boolean;
}
export interface RunningPerformanceResponse {
  period: '6m' | '1y' | 'all';
  distanceMeters: number;
  rangeStart: string;
  rangeEnd: string;
  points: RunningPerformancePoint[];
  personalBestCount: number;
}
export interface PerformanceRebuildResult { activitiesProcessed: number; effortsUpdated: number; algorithmVersion: number; }
export interface SyncResult { imported: number; skipped: number; failed: number; recordCount: number; lapCount: number; filesScanned: number; filesHashed: number; errors: string[]; mount: string | null; }

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly http = inject(HttpClient);
  list(page: number, pageSize: number, type: string, dayOfWeek: number | '', hidden: boolean): Observable<ActivityPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (type) params = params.set('type', type);
    if (dayOfWeek !== '') params = params.set('dayOfWeek', dayOfWeek);
    params = params.set('hidden', hidden);
    return this.http.get<ActivityPage>('/api/activities', { params });
  }
  get(id: number): Observable<ActivityDetailResponse> { return this.http.get<ActivityDetailResponse>(`/api/activities/${id}`); }
  getSegmentSummary(id: number, startRecord: number, endRecord: number): Observable<ActivitySegmentSummary> {
    const params = new HttpParams().set('startRecord', startRecord).set('endRecord', endRecord);
    return this.http.get<ActivitySegmentSummary>(`/api/activities/${id}/segment-summary`, { params });
  }
  getRunningConsistency(period: '6m' | '1y' | 'all', bucket: 'month' | 'week'): Observable<RunningConsistencyResponse> {
    const params = new HttpParams().set('period', period).set('bucket', bucket);
    return this.http.get<RunningConsistencyResponse>('/api/trends/consistency', { params });
  }
  getConsistencyActivities(startDate: string, endDate: string): Observable<ActivityRow[]> {
    const params = new HttpParams().set('startDate', startDate).set('endDate', endDate);
    return this.http.get<ActivityRow[]>('/api/trends/consistency/activities', { params });
  }
  getRunningPerformance(distanceMeters: number, period: '6m' | '1y' | 'all'): Observable<RunningPerformanceResponse> {
    const params = new HttpParams().set('distanceMeters', distanceMeters).set('period', period);
    return this.http.get<RunningPerformanceResponse>('/api/trends/performance', { params });
  }
  rebuildRunningPerformance(): Observable<PerformanceRebuildResult> {
    return this.http.post<PerformanceRebuildResult>('/api/trends/performance/rebuild', {});
  }
  setActivityHidden(id: number, isHidden: boolean): Observable<void> {
    return this.http.put<void>(`/api/activities/${id}/hidden`, { isHidden });
  }
  syncWatch(): Observable<SyncResult> { return this.http.post<SyncResult>('/api/sync', {}); }
}
