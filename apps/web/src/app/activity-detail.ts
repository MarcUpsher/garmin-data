import { DatePipe } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, OnInit, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ActivityDetailResponse, ActivitySample, ActivitySegmentSummary, ActivityService } from './activity.service';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { activityTypeLabel } from './activity-type-label';

interface ChartPoint {
  recordNumber: number;
  x: number;
  timestamp: number | null;
  distance: number | null;
  heartRate: number | null;
  pace: number | null;
  elevation: number | null;
}
interface ChartTick { x: number; label: string; }
interface ActivityChart {
  heartRatePath: string;
  pacePath: string;
  elevationPath: string;
  ticks: ChartTick[];
  points: ChartPoint[];
  hasSamples: boolean;
  hasHeartRate: boolean;
  hasPace: boolean;
  hasElevation: boolean;
  heartRateMin: string;
  heartRateMax: string;
  paceMin: string;
  paceMax: string;
  elevationMin: string;
  elevationMax: string;
  distanceAxisAvailable: boolean;
  axisTitle: string;
}

@Component({
  selector: 'app-activity-detail',
  imports: [DatePipe, MatButtonModule, MatCardModule],
  styleUrl: './activity-detail.scss',
  templateUrl: './activity-detail.html',
})
export class ActivityDetail implements OnInit, OnChanges, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ActivityService);
  protected readonly data = signal<ActivityDetailResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly missing = signal(false);
  @Input() activityId: number | null = null;
  @Input() drawerMode = false;
  @Input() returnLabel = 'Activities';
  @Output() closed = new EventEmitter<void>();
  private initialized = false;
  protected get isDrawer(): boolean { return this.drawerMode || this.route.snapshot.data['presentation'] === 'drawer'; }
  protected readonly axisMode = signal<'time' | 'distance'>('time');
  protected readonly hovered = signal<ChartPoint | null>(null);
  protected readonly selection = signal<{ startX: number; endX: number } | null>(null);
  protected readonly segment = signal<ActivitySegmentSummary | null>(null);
  protected readonly segmentLoading = signal(false);
  protected readonly chart = computed(() => this.buildChart(this.data(), this.axisMode()));
  private dragStart: number | null = null;
  private activityRequest?: Subscription;
  private segmentRequest?: Subscription;
  private routeRequest?: Subscription;

  ngOnInit(): void {
    this.initialized = true;
    if (this.activityId !== null) {
      this.loadActivity(this.activityId);
      return;
    }
    this.routeRequest = this.route.paramMap.subscribe((params) => {
      this.loadActivity(Number(params.get('id')));
    });
  }

  ngOnDestroy(): void {
    this.activityRequest?.unsubscribe();
    this.segmentRequest?.unsubscribe();
    this.routeRequest?.unsubscribe();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.initialized || !changes['activityId']) return;
    if (this.activityId === null) {
      this.resetActivityState();
      this.loading.set(false);
      this.missing.set(false);
    } else this.loadActivity(this.activityId);
  }

  private resetActivityState(): void {
    this.activityRequest?.unsubscribe();
    this.segmentRequest?.unsubscribe();
    this.data.set(null);
    this.hovered.set(null);
    this.selection.set(null);
    this.segment.set(null);
    this.segmentLoading.set(false);
    this.dragStart = null;
  }

  private loadActivity(id: number): void {
    this.resetActivityState();
    this.loading.set(true);
    this.missing.set(false);
    if (!Number.isSafeInteger(id) || id < 1) {
      this.loading.set(false);
      this.missing.set(true);
      return;
    }
    this.activityRequest = this.service.get(id).subscribe({
      next: (data) => { this.data.set(data); this.loading.set(false); },
      error: () => { this.loading.set(false); this.missing.set(true); },
    });
  }

  protected close(): void {
    if (this.activityId !== null) this.closed.emit();
    else void this.router.navigateByUrl('/');
  }
  protected label(): string {
    const activity = this.data()?.activity;
    return activity?.title || activityTypeLabel(activity?.activityType ?? null);
  }
  protected setAxis(mode: 'time' | 'distance'): void {
    if (mode === 'distance' && !this.chart().distanceAxisAvailable) return;
    this.segmentRequest?.unsubscribe();
    this.axisMode.set(mode);
    this.hovered.set(null);
    this.selection.set(null);
    this.segment.set(null);
  }
  protected formatDistance(meters: number | null): string {
    return meters === null ? '—' : `${(meters / 1000).toFixed(2)} km`;
  }
  protected formatDuration(seconds: number | null): string {
    if (seconds === null || !Number.isFinite(seconds)) return '—';
    const total = Math.max(0, Math.round(seconds));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const remaining = total % 60;
    return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remaining).padStart(2, '0')}` : `${minutes}:${String(remaining).padStart(2, '0')}`;
  }
  protected formatPace(secondsPerKm: number | null): string {
    if (secondsPerKm === null || !Number.isFinite(secondsPerKm) || secondsPerKm <= 0) return '—';
    const rounded = Math.round(secondsPerKm);
    return `${Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, '0')} /km`;
  }
  protected lapPace(speed: number | null, distance: number | null, seconds: number | null): number | null {
    if (speed !== null && speed > 0) return 1000 / speed;
    return distance !== null && distance > 0 && seconds !== null && seconds > 0 ? seconds * 1000 / distance : null;
  }
  protected heartRate(value: number | null | undefined): string {
    return value === null || value === undefined || !Number.isFinite(value) ? '—' : `${Math.round(value)} bpm`;
  }
  protected heartRateTransition(start: number | null, end: number | null): string {
    if (start === null || start === undefined || end === null || end === undefined
      || !Number.isFinite(start) || !Number.isFinite(end)) return '—';
    const change = end - start;
    const delta = `${change > 0 ? '+' : change < 0 ? '−' : ''}${Math.abs(change)}`;
    return `${start} → ${end} bpm (${delta})`;
  }
  protected elevation(value: number | null): string { return value === null ? '—' : `${value.toFixed(0)} m`; }
  protected elevationChange(value: number | null): string {
    if (value === null) return '—';
    const rounded = Math.round(value);
    return `${rounded > 0 ? '+' : rounded < 0 ? '−' : ''}${Math.abs(rounded)} m`;
  }
  protected pointerX(event: PointerEvent): number {
    const svg = event.currentTarget as SVGSVGElement;
    const rect = svg.getBoundingClientRect();
    return Math.max(92, Math.min(908, (event.clientX - rect.left) / rect.width * 1000));
  }
  protected chartPointerDown(event: PointerEvent): void {
    if (event.button !== 0) return;
    this.segmentRequest?.unsubscribe();
    this.segmentLoading.set(false);
    const x = this.pointerX(event);
    this.dragStart = x;
    this.selection.set({ startX: x, endX: x });
    this.segment.set(null);
    (event.currentTarget as SVGSVGElement).setPointerCapture(event.pointerId);
  }
  protected chartPointerMove(event: PointerEvent): void {
    const x = this.pointerX(event);
    const current = this.selection();
    if (this.dragStart !== null && current) {
      this.selection.set({ startX: this.dragStart, endX: x });
      return;
    }
    this.hovered.set(this.nearestPoint(x));
  }
  protected chartPointerUp(event: PointerEvent): void {
    if (this.dragStart === null) return;
    const startX = Math.min(this.dragStart, this.pointerX(event));
    const endX = Math.max(this.dragStart, this.pointerX(event));
    this.dragStart = null;
    if (endX - startX < 8) {
      this.selection.set(null);
      this.hovered.set(this.nearestPoint(endX));
      return;
    }
    this.selection.set({ startX, endX });
    const start = this.nearestPoint(startX);
    const end = this.nearestPoint(endX);
    const id = this.data()?.activity.id;
    if (!start || !end || !id || start.recordNumber === end.recordNumber) return;
    this.segmentRequest?.unsubscribe();
    this.segmentLoading.set(true);
    this.segmentRequest = this.service.getSegmentSummary(id, Math.min(start.recordNumber, end.recordNumber), Math.max(start.recordNumber, end.recordNumber)).subscribe({
      next: (result) => { this.segment.set(result); this.segmentLoading.set(false); },
      error: () => { this.segment.set(null); this.segmentLoading.set(false); },
    });
  }
  protected clearSelection(): void {
    this.segmentRequest?.unsubscribe();
    this.selection.set(null);
    this.segment.set(null);
    this.segmentLoading.set(false);
  }
  protected chartPointerLeave(): void { if (this.dragStart === null) this.hovered.set(null); }
  protected selectedLeft(): number { const value = this.selection(); return value ? Math.min(value.startX, value.endX) : 0; }
  protected selectedWidth(): number { const value = this.selection(); return value ? Math.abs(value.endX - value.startX) : 0; }
  protected hoverX(): number | null { return this.hovered()?.x ?? null; }
  protected axisPosition(point: ChartPoint): string {
    return this.axisMode() === 'distance'
      ? (point.distance === null ? '—' : this.formatDistance(point.distance))
      : (point.timestamp === null ? '—' : this.formatDuration((point.timestamp - (this.chart().points.find(item => item.timestamp !== null)?.timestamp ?? point.timestamp)) / 1000));
  }

  private nearestPoint(x: number): ChartPoint | null {
    const points = this.chart().points;
    if (!points.length) return null;
    return points.reduce((best, point) => Math.abs(point.x - x) < Math.abs(best.x - x) ? point : best);
  }

  private buildChart(data: ActivityDetailResponse | null, axis: 'time' | 'distance'): ActivityChart {
    const samples = data?.records ?? [];
    const timestamps = samples.map(sample => this.timestamp(sample.timestampUtc));
    const validTimes = timestamps.filter((value): value is number => value !== null);
    const firstTime = validTimes.length ? Math.min(...validTimes) : null;
    const distanceValues = samples.map(sample => sample.distanceMeters);
    const validDistances = distanceValues.filter((value): value is number => value !== null && Number.isFinite(value));
    const firstDistance = validDistances.length ? Math.min(...validDistances) : 0;
    const lastDistance = validDistances.length ? Math.max(...validDistances) : 0;
    const distanceAxisAvailable = validDistances.length > 1 && lastDistance > firstDistance;
    const useDistance = axis === 'distance' && distanceAxisAvailable;
    const fallbackSpan = Math.max(data?.activity.elapsedTimeSeconds ?? 0, 1);
    const xValues = samples.map((sample, index) => {
      if (useDistance) return sample.distanceMeters === null ? null : sample.distanceMeters - firstDistance;
      const time = timestamps[index];
      return time !== null && firstTime !== null ? (time - firstTime) / 1000
        : samples.length > 1 ? index / (samples.length - 1) * fallbackSpan : 0;
    });
    const lastX = xValues.filter((value): value is number => value !== null).reduce((max, value) => Math.max(max, value), 0) || fallbackSpan;
    const plotLeft = 92, plotRight = 908, plotTop = 32, plotBottom = 286;
    const points = samples.flatMap((sample, index): ChartPoint[] => {
      const rawX = xValues[index];
      if (rawX === null) return [];
      const speed = sample.speedMetersPerSecond;
      return [{
        recordNumber: sample.recordNumber,
        x: plotLeft + Math.max(0, Math.min(1, rawX / lastX)) * (plotRight - plotLeft),
        timestamp: timestamps[index], distance: sample.distanceMeters === null ? null : sample.distanceMeters - firstDistance,
        heartRate: sample.heartRateBpm,
        pace: speed !== null && speed > 0 ? 1000 / speed : null,
        elevation: sample.altitudeMeters,
      }];
    });
    const heartRates = points.flatMap(point => point.heartRate === null ? [] : [point.heartRate]);
    const paces = points.flatMap(point => point.pace === null ? [] : [point.pace]);
    const elevations = points.flatMap(point => point.elevation === null ? [] : [point.elevation]);
    const range = (values: number[], step: number) => {
      const min = values.length ? Math.min(...values) : 0;
      const max = values.length ? Math.max(...values) : 0;
      const padding = max === min ? step : (max - min) * .08;
      return { low: Math.floor((min - padding) / step) * step, high: Math.ceil((max + padding) / step) * step };
    };
    const hrRange = range(heartRates, 5), paceRange = range(paces, 30), elevationRange = range(elevations, 10);
    const plottedPaces = points.map((point, index) => {
      if (point.pace === null) return null;
      const window = points.slice(Math.max(0, index - 2), Math.min(points.length, index + 3))
        .flatMap(item => item.pace === null ? [] : [item.pace]).sort((a, b) => a - b);
      return window[Math.floor(window.length / 2)] ?? point.pace;
    });
    const mapLine = (key: 'heartRate' | 'pace') => points.flatMap((point, index) => {
      const value = key === 'pace' ? plottedPaces[index] : point.heartRate;
      if (value === null) return [];
      const bounds = key === 'heartRate' ? hrRange : paceRange;
      const normal = key === 'pace'
        ? Math.max(0, Math.min(1, (bounds.high - value) / Math.max(1, bounds.high - bounds.low)))
        : Math.max(0, Math.min(1, (value - bounds.low) / Math.max(1, bounds.high - bounds.low)));
      const y = plotBottom - normal * (plotBottom - plotTop);
      const previousValue = index === 0 ? null : key === 'pace' ? plottedPaces[index - 1] : points[index - 1].heartRate;
      return [`${previousValue === null ? 'M' : 'L'}${point.x.toFixed(1)},${y.toFixed(1)}`];
    }).join(' ');
    const elevationPoints = points.flatMap(point => {
      if (point.elevation === null) return [];
      const normal = Math.max(0, Math.min(1, (point.elevation - elevationRange.low) / Math.max(1, elevationRange.high - elevationRange.low)));
      return [`${point.x.toFixed(1)},${(367 - normal * 43).toFixed(1)}`];
    });
    const elevationPath = elevationPoints.length > 1 ? `M${plotLeft},370 L${elevationPoints.join(' L')} L${plotRight},370 Z` : '';
    const tickMax = useDistance ? lastDistance - firstDistance : lastX;
    const ticks = Array.from({ length: 5 }, (_, index) => {
      const portion = index / 4;
      const value = tickMax * portion;
      return { x: plotLeft + portion * (plotRight - plotLeft), label: useDistance ? `${(value / 1000).toFixed(value >= 1000 ? 1 : 2)} km` : this.axisTime(value) };
    });
    return {
      heartRatePath: mapLine('heartRate'), pacePath: mapLine('pace'), elevationPath, ticks, points,
      hasSamples: heartRates.length > 1 || paces.length > 1 || elevations.length > 1,
      hasHeartRate: heartRates.length > 1, hasPace: paces.length > 1, hasElevation: elevations.length > 1,
      heartRateMin: `${Math.round(hrRange.low)} bpm`, heartRateMax: `${Math.round(hrRange.high)} bpm`,
      paceMin: this.formatPace(paceRange.low).replace(' /km', ''), paceMax: this.formatPace(paceRange.high).replace(' /km', ''),
      elevationMin: `${Math.round(elevationRange.low)} m`, elevationMax: `${Math.round(elevationRange.high)} m`,
      distanceAxisAvailable, axisTitle: useDistance ? 'Distance' : 'Elapsed time',
    };
  }
  private timestamp(value: string | null): number | null {
    if (!value) return null;
    const parsed = Date.parse(value);
    return Number.isFinite(parsed) ? parsed : null;
  }
  private axisTime(seconds: number): string {
    const total = Math.round(seconds);
    const hours = Math.floor(total / 3600), minutes = Math.floor((total % 3600) / 60), remainder = total % 60;
    return hours > 0 ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}` : `${minutes}:${String(remainder).padStart(2, '0')}`;
  }
}
