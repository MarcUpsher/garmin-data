import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivityDetail } from './activity-detail';
import { ActivityRow, ActivityService, RunningConsistencyBucket, RunningConsistencyResponse, RunningPerformancePoint, RunningPerformanceResponse } from './activity.service';

interface ChartBar extends RunningConsistencyBucket {
  x: number;
  y: number;
  width: number;
  height: number;
  slotX: number;
  slotWidth: number;
  axisLabel: string;
}
interface DistanceTick { y: number; value: number; label: string; }
interface ConsistencyChart { bars: ChartBar[]; ticks: DistanceTick[]; maxDistance: number; }
interface PerformanceChartPoint extends RunningPerformancePoint { x: number; y: number; }
interface PerformanceTick { y: number; seconds: number; label: string; }
interface PerformanceDateTick { x: number; label: string; }
interface PerformanceChart { points: PerformanceChartPoint[]; ticks: PerformanceTick[]; dateTicks: PerformanceDateTick[]; }

@Component({
  selector: 'app-running-trends',
  imports: [ActivityDetail, DatePipe, DecimalPipe, RouterLink],
  styleUrl: './running-trends.scss',
  templateUrl: './running-trends.html',
})
export class RunningTrends implements OnInit {
  private readonly service = inject(ActivityService);
  protected readonly period = signal<'6m' | '1y' | 'all'>('6m');
  protected readonly bucketSize = signal<'month' | 'week'>('month');
  protected readonly response = signal<RunningConsistencyResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly hoveredIndex = signal<number | null>(null);
  protected readonly selectedBucket = signal<RunningConsistencyBucket | null>(null);
  protected readonly activities = signal<ActivityRow[]>([]);
  protected readonly activitiesLoading = signal(false);
  protected readonly chart = computed(() => this.buildChart(this.response()));
  protected readonly activeView = signal<'consistency' | 'performance'>('consistency');
  protected readonly performanceDistance = signal(5000);
  protected readonly performance = signal<RunningPerformanceResponse | null>(null);
  protected readonly performanceLoading = signal(false);
  protected readonly performanceError = signal<string | null>(null);
  protected readonly rebuilding = signal(false);
  protected readonly rebuildMessage = signal<string | null>(null);
  protected readonly performanceChart = computed(() => this.buildPerformanceChart(this.performance()));
  protected readonly selectedActivityId = signal<number | null>(null);

  ngOnInit(): void { this.load(); }

  protected load(): void {
    if (this.activeView() === 'performance') { this.loadPerformance(); return; }
    this.loading.set(true);
    this.error.set(null);
    this.selectedBucket.set(null);
    this.activities.set([]);
    this.service.getRunningConsistency(this.period(), this.bucketSize()).subscribe({
      next: value => { this.response.set(value); this.loading.set(false); },
      error: () => { this.error.set('Could not load running trends. Check that the local activity service is running.'); this.loading.set(false); },
    });
  }

  protected selectView(view: 'consistency' | 'performance'): void {
    this.activeView.set(view);
    if (view === 'performance') this.loadPerformance();
  }

  protected loadPerformance(): void {
    this.performanceLoading.set(true);
    this.performanceError.set(null);
    this.service.getRunningPerformance(this.performanceDistance(), this.period()).subscribe({
      next: value => { this.performance.set(value); this.performanceLoading.set(false); },
      error: () => { this.performanceError.set('Could not load performance efforts. Check that the local activity service is running.'); this.performanceLoading.set(false); },
    });
  }

  protected selectPerformanceDistance(distance: number): void {
    this.performanceDistance.set(distance);
    this.loadPerformance();
  }

  protected openActivity(activityId: number): void { this.selectedActivityId.set(activityId); }
  protected closeActivity(): void { this.selectedActivityId.set(null); }

  protected rebuildPerformance(): void {
    this.rebuilding.set(true);
    this.rebuildMessage.set(null);
    this.service.rebuildRunningPerformance().subscribe({
      next: result => {
        this.rebuilding.set(false);
        this.rebuildMessage.set(`Updated ${result.effortsUpdated} efforts from ${result.activitiesProcessed} runs.`);
        this.loadPerformance();
      },
      error: () => { this.rebuilding.set(false); this.performanceError.set('Could not update performance efforts.'); },
    });
  }

  protected selectPeriod(period: '6m' | '1y' | 'all'): void {
    this.period.set(period);
    if (period !== '6m') this.bucketSize.set('month');
    this.load();
  }

  protected selectBucketSize(bucket: 'month' | 'week'): void {
    if (bucket === 'week' && this.period() !== '6m') return;
    this.bucketSize.set(bucket);
    this.load();
  }

  protected hover(index: number | null): void { this.hoveredIndex.set(index); }
  protected hoveredBucket(): RunningConsistencyBucket | null {
    const index = this.hoveredIndex();
    return index === null ? null : this.response()?.buckets[index] ?? null;
  }

  protected chooseBucket(bucket: RunningConsistencyBucket): void {
    this.selectedBucket.set(bucket);
    this.activities.set([]);
    this.activitiesLoading.set(true);
    this.service.getConsistencyActivities(bucket.startDate, bucket.endDate).subscribe({
      next: value => { this.activities.set(value); this.activitiesLoading.set(false); },
      error: () => { this.activities.set([]); this.activitiesLoading.set(false); },
    });
  }

  protected chooseBucketByKey(event: KeyboardEvent, bucket: RunningConsistencyBucket): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.chooseBucket(bucket);
    }
  }

  protected formatDuration(seconds: number | null): string {
    if (seconds === null || !Number.isFinite(seconds)) return '—';
    const total = Math.max(0, Math.round(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const remainder = total % 60;
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
      : `${minutes}:${String(remainder).padStart(2, '0')}`;
  }

  protected formatPace(value: number | null): string {
    if (value === null || !Number.isFinite(value) || value <= 0) return '—';
    const seconds = Math.round(value);
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')} /km`;
  }

  protected formatDistance(value: number | null): string {
    return value === null ? '—' : `${(value / 1000).toFixed(2)} km`;
  }

  protected activityLabel(activity: ActivityRow): string { return activity.title || 'Run'; }

  private buildChart(response: RunningConsistencyResponse | null): ConsistencyChart {
    const buckets = response?.buckets ?? [];
    const maxValue = Math.max(0, ...buckets.map(item => item.distanceKm));
    const scale = this.distanceScale(maxValue);
    const maxDistance = scale.maximum;
    const left = 70, right = 990, top = 24, bottom = 288;
    const step = buckets.length ? (right - left) / buckets.length : right - left;
    const barWidth = Math.max(1.5, Math.min(34, step * .64));
    const bars = buckets.map((item, index): ChartBar => {
      const height = item.distanceKm > 0 ? (item.distanceKm / maxDistance) * (bottom - top) : 0;
      return {
        ...item,
        x: left + index * step + (step - barWidth) / 2,
        y: bottom - height,
        width: barWidth,
        height,
        slotX: left + index * step,
        slotWidth: step,
        axisLabel: this.axisLabel(item, index, buckets.length, response?.period ?? '6m', response?.bucket ?? 'month'),
      };
    });
    const tickCount = Math.round(maxDistance / scale.step);
    const ticks = Array.from({ length: tickCount + 1 }, (_, index) => {
      const value = Number((index * scale.step).toPrecision(10));
      return { y: bottom - (value / maxDistance) * (bottom - top), value, label: this.formatDistanceTick(value, scale.step) };
    });
    return { bars, ticks, maxDistance };
  }

  private axisLabel(bucket: RunningConsistencyBucket, index: number, count: number, period: '6m' | '1y' | 'all', size: 'month' | 'week'): string {
    if (size === 'week') return index % 4 === 0 || index === count - 1 ? bucket.label.replace('Week of ', '') : '';
    if (period === 'all') return bucket.label.endsWith('-01') || index === count - 1 ? bucket.label : '';
    if (period === '1y') return index % 3 === 0 || index === count - 1 ? bucket.label : '';
    return bucket.label;
  }

  private distanceScale(value: number): { step: number; maximum: number } {
    if (value <= 0) return { step: .25, maximum: 1 };
    const roughStep = value / 4;
    const magnitude = 10 ** Math.floor(Math.log10(roughStep));
    const normalized = roughStep / magnitude;
    const preferredStep = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
    const step = preferredStep * magnitude;
    const maximum = Math.ceil(value / step - 1e-10) * step;
    return { step, maximum };
  }

  private formatDistanceTick(value: number, step: number): string {
    const places = Number.isInteger(step) ? 0 : Number.isInteger(step * 10) ? 1 : Number.isInteger(step * 100) ? 2 : 3;
    return value.toFixed(places);
  }

  private buildPerformanceChart(response: RunningPerformanceResponse | null): PerformanceChart {
    const points = response?.points ?? [];
    if (!points.length) return { points: [], ticks: [], dateTicks: [] };
    const dates = points.map(point => new Date(point.startedAt).getTime()).filter(Number.isFinite);
    const start = response?.period === 'all' ? Math.min(...dates) : new Date(response!.rangeStart).getTime();
    const end = Math.max(start + 86400000, new Date(response!.rangeEnd).getTime());
    const durations = points.map(point => point.durationSeconds);
    const min = Math.min(...durations), max = Math.max(...durations);
    const padding = Math.max(30, (max - min) * .12);
    const lower = Math.max(0, Math.floor((min - padding) / 30) * 30);
    const upper = Math.ceil((max + padding) / 30) * 30 || lower + 30;
    const left = 82, right = 990, top = 26, bottom = 288;
    const mapped = points.map(point => ({
      ...point,
      x: left + ((new Date(point.startedAt).getTime() - start) / (end - start)) * (right - left),
      // Faster efforts (lower times) appear higher on the chart.
      y: bottom - ((upper - point.durationSeconds) / (upper - lower)) * (bottom - top),
    }));
    const ticks = Array.from({ length: 5 }, (_, index) => {
      // The point mapping puts shorter (faster) times higher, so labels must
      // increase from the top tick to the bottom tick.
      const seconds = lower + (upper - lower) * index / 4;
      return {
        y: top + (bottom - top) * index / 4,
        seconds,
        label: this.formatPace(seconds * 1000 / response!.distanceMeters),
      };
    });
    const dateTicks = Array.from({ length: 5 }, (_, index) => {
      const time = start + (end - start) * index / 4;
      const label = new Intl.DateTimeFormat('en-GB', response?.period === 'all'
        ? { year: 'numeric' }
        : { month: 'short', year: '2-digit' }).format(new Date(time));
      return { x: left + (right - left) * index / 4, label };
    });
    return { points: mapped, ticks, dateTicks };
  }
}
