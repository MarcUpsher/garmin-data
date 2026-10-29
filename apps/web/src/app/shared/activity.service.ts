import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ActivityRow, ActivityLap, ActivitySample, ActivitySegmentSummary, ActivityDetails, ActivityDetailResponse, ActivityPage, RunningConsistencyBucket, RunningConsistencyResponse, RunningPerformancePoint, RunningPerformanceResponse, PerformanceRebuildResult, SleepTrendPoint, SleepTrendResponse, Vo2TrendPoint, Vo2TrendResponse, WellnessImportResult, WellnessImportStatus, SyncResult } from './activity.models';

@Injectable({ providedIn: 'root' })
export class ActivityService {
  readonly listCount = signal<number | null>(null);
  private readonly http = inject(HttpClient);
  list(page: number, pageSize: number, type: string, dayOfWeek: number | '', hidden: boolean): Observable<ActivityPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (type) params = params.set('type', type);
    if (dayOfWeek !== '') params = params.set('dayOfWeek', dayOfWeek);
    params = params.set('hidden', hidden);
    return this.http.get<ActivityPage>('/api/activities', { params }).pipe(tap(page => this.listCount.set(page.totalCount)));
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
  importWellness(directoryPath: string): Observable<WellnessImportResult> { return this.http.post<WellnessImportResult>('/api/wellness/import', { directoryPath }); }
  getWellnessStatus(): Observable<WellnessImportStatus> { return this.http.get<WellnessImportStatus>('/api/wellness/status'); }
  getSleepTrend(period: '6m' | '1y' | 'all'): Observable<SleepTrendResponse> {
    return this.http.get<SleepTrendResponse>('/api/trends/sleep', { params: new HttpParams().set('period', period) });
  }
  getVo2Trend(period: '6m' | '1y' | 'all'): Observable<Vo2TrendResponse> {
    return this.http.get<Vo2TrendResponse>('/api/trends/vo2-max', { params: new HttpParams().set('period', period) });
  }
  setActivityHidden(id: number, isHidden: boolean): Observable<void> {
    return this.http.put<void>(`/api/activities/${id}/hidden`, { isHidden });
  }
  syncWatch(): Observable<SyncResult> { return this.http.post<SyncResult>('/api/sync', {}); }
}
