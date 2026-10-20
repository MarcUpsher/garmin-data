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
  timestampUtc: string | null;
  distanceMeters: number | null;
  speedMetersPerSecond: number | null;
  heartRateBpm: number | null;
  altitudeMeters: number | null;
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
export interface SyncResult { imported: number; skipped: number; failed: number; recordCount: number; lapCount: number; filesScanned: number; filesHashed: number; errors: string[]; mount: string | null; }

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly http = inject(HttpClient);
  list(page: number, pageSize: number, type: string): Observable<ActivityPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (type) params = params.set('type', type);
    return this.http.get<ActivityPage>('/api/activities', { params });
  }
  get(id: number): Observable<ActivityDetailResponse> { return this.http.get<ActivityDetailResponse>(`/api/activities/${id}`); }
  syncWatch(): Observable<SyncResult> { return this.http.post<SyncResult>('/api/sync', {}); }
}
