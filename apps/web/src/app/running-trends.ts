import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivityDetail } from './activity-detail';
import { ActivityRow, ActivityService, RunningConsistencyBucket, RunningConsistencyResponse, RunningPerformancePoint, RunningPerformanceResponse, SleepTrendPoint, SleepTrendResponse, Vo2TrendPoint, Vo2TrendResponse } from './activity.service';

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
interface MetricChartPoint { x: number; y: number; label: string; value: number; detail: string; }
interface MetricChart { points: MetricChartPoint[]; ticks: { y: number; label: string }[]; dateTicks: { x: number; label: string }[]; }

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
  protected readonly activeView = signal<'consistency' | 'performance' | 'sleep' | 'vo2'>('consistency');
  protected readonly performanceDistance = signal(5000);
  protected readonly performance = signal<RunningPerformanceResponse | null>(null);
  protected readonly performanceLoading = signal(false);
  protected readonly performanceError = signal<string | null>(null);
  protected readonly rebuilding = signal(false);
  protected readonly rebuildMessage = signal<string | null>(null);
  protected readonly performanceChart = computed(() => this.buildPerformanceChart(this.performance()));
  protected readonly selectedActivityId = signal<number | null>(null);
  protected readonly sleep = signal<SleepTrendResponse | null>(null);
  protected readonly vo2 = signal<Vo2TrendResponse | null>(null);
  protected readonly metricLoading = signal(false);
  protected readonly metricError = signal<string | null>(null);
  protected readonly sleepChart = computed(() => this.buildMetricChart(this.sleep()?.points ?? [], 'averageSleepHours', 'month', 0));
  protected readonly vo2Chart = computed(() => this.buildMetricChart(this.vo2()?.points ?? [], 'value', 'date', 20));

  ngOnInit(): void { this.load(); }

  protected load(): void {
    if (this.activeView() === 'performance') { this.loadPerformance(); return; }
    if (this.activeView() === 'sleep') { this.loadSleep(); return; }
    if (this.activeView() === 'vo2') { this.loadVo2(); return; }
    this.loading.set(true);
    this.error.set(null);
    this.selectedBucket.set(null);
    this.activities.set([]);
    this.service.getRunningConsistency(this.period(), this.bucketSize()).subscribe({
      next: value => { this.response.set(value); this.loading.set(false); },
      error: () => { this.error.set('Could not load running trends. Check that the local activity service is running.'); this.loading.set(false); },
    });
  }

  protected selectView(view: 'consistency' | 'performance' | 'sleep' | 'vo2'): void {
    this.activeView.set(view);
    if (view === 'performance') this.loadPerformance();
    if (view === 'sleep') this.loadSleep();
    if (view === 'vo2') this.loadVo2();
  }

  protected loadSleep(): void {
    this.metricLoading.set(true); this.metricError.set(null);
    this.service.getSleepTrend(this.period()).subscribe({ next: value => { this.sleep.set(value); this.metricLoading.set(false); }, error: () => { this.metricError.set('Could not load sleep history.'); this.metricLoading.set(false); } });
  }
  protected loadVo2(): void {
    this.metricLoading.set(true); this.metricError.set(null);
    this.service.getVo2Trend(this.period()).subscribe({ next: value => { this.vo2.set(value); this.metricLoading.set(false); }, error: () => { this.metricError.set('Could not load VO₂ Max history.'); this.metricLoading.set(false); } });
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

  private buildMetricChart<T extends SleepTrendPoint | Vo2TrendPoint>(points: T[], valueKey: 'averageSleepHours' | 'value', dateKey: 'month' | 'date', floor: number): MetricChart {
    let values = points.map(point => ({ dateText: 'month' in point ? point.month : point.date, date: new Date('month' in point ? point.month : point.date).getTime(), value: ('averageSleepHours' in point ? point.averageSleepHours : point.value) as number | null, point, repeated: 1 }))
      .filter((item): item is { dateText: string; date: number; value: number; point: T; repeated: number } => Number.isFinite(item.date) && item.value !== null && Number.isFinite(item.value));
    if (valueKey === 'value') {
      const distinct = new Map<string, typeof values[number]>();
      for (const item of values) {
        const point = item.point as Vo2TrendPoint;
        const key = `${item.dateText}|${item.value}|${point.sport ?? ''}|${point.subSport ?? ''}|${point.deviceId ?? ''}|${point.isCalibrated ?? ''}`;
        const prior = distinct.get(key);
        if (prior) prior.repeated++;
        else distinct.set(key, { ...item });
      }
      values = [...distinct.values()];
    }
    if (!values.length) return { points: [], ticks: [], dateTicks: [] };
    const dates = values.map(item => item.date);
    const maxDate = Math.max(...dates), actualMin = Math.min(...dates);
    const rangeMin = this.period() === 'all' ? actualMin : Math.min(actualMin, new Date(Date.now() - (this.period() === '6m' ? 183 : 365) * 86400000).getTime());
    const rangeMax = Math.max(maxDate, rangeMin + 86400000);
    const maxValue = Math.max(...values.map(x => x.value));
    const minValue = valueKey === 'value' ? Math.min(floor, ...values.map(x => x.value)) : 0;
    const pad = valueKey === 'value' ? Math.max(1, (maxValue - minValue) * .12) : .4;
    const low = Math.max(valueKey === 'value' ? 0 : 0, minValue - (valueKey === 'value' ? pad : 0));
    const high = Math.ceil((maxValue + pad) * (valueKey === 'value' ? 1 : 2)) / (valueKey === 'value' ? 1 : 2);
    const top = 24, bottom = 288, left = 82, right = 990;
    const chartPoints = values.map(({ date, value, point, repeated, dateText }) => {
      const detail = valueKey === 'value'
        ? `${(point as Vo2TrendPoint).sport ?? 'Unknown sport'} · ${(point as Vo2TrendPoint).subSport ?? 'unspecified'}${(point as Vo2TrendPoint).isCalibrated === false ? ' · uncalibrated' : ''}${repeated > 1 ? ` · grouped ${repeated} identical export records` : ''}`
        : `${(point as SleepTrendPoint).nightCount} nights · avg sleep ${(point as SleepTrendPoint).averageSleepHours!.toFixed(1)} h · deep ${((point as SleepTrendPoint).averageDeepSeconds ?? 0) / 60 | 0} min · REM ${((point as SleepTrendPoint).averageRemSeconds ?? 0) / 60 | 0} min`;
      return { x: left + ((date - rangeMin) / (rangeMax - rangeMin)) * (right - left), y: bottom - ((value - low) / (high - low || 1)) * (bottom - top), label: valueKey === 'value' ? new Date(date).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) : new Date(date).toLocaleDateString(undefined, { year: 'numeric', month: 'short' }), dateText, value, detail };
    });
    if (valueKey === 'value') {
      const byDate = new Map<string, typeof chartPoints>();
      for (const point of chartPoints) byDate.set(point.dateText, [...(byDate.get(point.dateText) ?? []), point]);
      for (const dayPoints of byDate.values()) {
        dayPoints.sort((a, b) => a.value - b.value || a.detail.localeCompare(b.detail));
        dayPoints.forEach((point, index) => point.x += (index - (dayPoints.length - 1) / 2) * 12);
      }
    }
    const tickCount = 4;
    const dateTicks = Array.from({ length: 5 }, (_, i) => {
      const date = new Date(rangeMin + (rangeMax - rangeMin) * i / 4);
      return { x: left + (right - left) * i / 4, label: date.toLocaleDateString(undefined, { year: '2-digit', month: 'short' }) };
    });
    return { points: chartPoints, ticks: Array.from({ length: tickCount + 1 }, (_, i) => { const value = low + (high - low) * i / tickCount; return { y: bottom - i * (bottom - top) / tickCount, label: value.toFixed(valueKey === 'value' ? 0 : 1) }; }), dateTicks };
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
