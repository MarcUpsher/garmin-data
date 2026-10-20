import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ActivityDetailResponse, ActivitySample, ActivityService } from './activity.service';

interface ChartLane {
  key: string;
  label: string;
  color: string;
  top: number;
  bottom: number;
  points: string;
  minimum: string;
  maximum: string;
  available: boolean;
}

interface ChartTick { x: number; label: string; }
interface ActivityChart { lanes: ChartLane[]; ticks: ChartTick[]; hasSamples: boolean; }

@Component({
  selector: 'app-activity-detail',
  imports: [DatePipe],
  styleUrl: './activity-detail.scss',
  templateUrl: './activity-detail.html',
})
export class ActivityDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(ActivityService);
  protected readonly data = signal<ActivityDetailResponse | null>(null);
  protected readonly loading = signal(true);
  protected readonly missing = signal(false);
  protected readonly isDrawer = this.route.snapshot.data['presentation'] === 'drawer';
  protected readonly chart = computed(() => this.buildChart(this.data()));

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      this.data.set(null);
      this.loading.set(true);
      this.missing.set(false);
      if (!Number.isSafeInteger(id) || id < 1) {
        this.loading.set(false);
        this.missing.set(true);
        return;
      }
      this.service.get(id).subscribe({
        next: (data) => { this.data.set(data); this.loading.set(false); },
        error: () => { this.loading.set(false); this.missing.set(true); },
      });
    });
  }

  protected close(): void { void this.router.navigateByUrl('/'); }
  protected label(): string {
    const activity = this.data()?.activity;
    return activity?.title || activity?.activityType || 'Activity';
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
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remaining).padStart(2, '0')}`
      : `${minutes}:${String(remaining).padStart(2, '0')}`;
  }

  protected formatPace(secondsPerKm: number | null): string {
    if (secondsPerKm === null || !Number.isFinite(secondsPerKm) || secondsPerKm <= 0) return '—';
    const rounded = Math.round(secondsPerKm);
    return `${Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, '0')} /km`;
  }

  protected lapPace(speed: number | null, distance: number | null, seconds: number | null): number | null {
    if (speed !== null && speed > 0) return 1000 / speed;
    return distance !== null && distance > 0 && seconds !== null && seconds > 0
      ? seconds * 1000 / distance
      : null;
  }

  protected heartRate(value: number | null): string { return value === null ? '—' : `${value} bpm`; }

  private buildChart(data: ActivityDetailResponse | null): ActivityChart {
    const samples = data?.records ?? [];
    const timestamps = samples.map((sample) => this.timestamp(sample.timestampUtc));
    const validTimes = timestamps.filter((value): value is number => value !== null);
    const firstTime = validTimes.length ? Math.min(...validTimes) : null;
    const lastTime = validTimes.length ? Math.max(...validTimes) : null;
    const sampleDuration = firstTime !== null && lastTime !== null ? (lastTime - firstTime) / 1000 : 0;
    const duration = Math.max(data?.activity.elapsedTimeSeconds ?? 0, sampleDuration, 1);
    const plotLeft = 94;
    const plotRight = 978;
    const plotWidth = plotRight - plotLeft;
    const definitions = [
      { key: 'heartRate', label: 'Heart rate', color: '#f17878', top: 18, bottom: 86, values: samples.map((sample) => sample.heartRateBpm), unit: 'bpm' },
      { key: 'pace', label: 'Pace', color: '#64a9f5', top: 118, bottom: 186, values: samples.map((sample) => sample.speedMetersPerSecond !== null && sample.speedMetersPerSecond > 0 ? 1000 / sample.speedMetersPerSecond : null), unit: 'pace' },
      { key: 'elevation', label: 'Elevation', color: '#d9a45b', top: 218, bottom: 286, values: samples.map((sample) => sample.altitudeMeters), unit: 'm' },
    ];

    const lanes = definitions.map((definition): ChartLane => {
      const numbers = definition.values.filter((value): value is number => value !== null && Number.isFinite(value));
      const minimum = numbers.length ? Math.min(...numbers) : 0;
      const maximum = numbers.length ? Math.max(...numbers) : 0;
      const padding = maximum === minimum ? Math.max(Math.abs(minimum) * 0.03, 1) : (maximum - minimum) * 0.08;
      const low = this.roundRange(minimum - padding, definition.unit, false);
      const high = this.roundRange(maximum + padding, definition.unit, true);
      const spread = Math.max(high - low, 1);
      const points = samples.flatMap((sample, index) => {
        const value = definition.values[index];
        if (value === null || !Number.isFinite(value)) return [];
        const timestamp = timestamps[index];
        const elapsed = timestamp !== null && firstTime !== null
          ? (timestamp - firstTime) / 1000
          : samples.length > 1 ? index / (samples.length - 1) * duration : 0;
        const x = plotLeft + Math.max(0, Math.min(1, elapsed / duration)) * plotWidth;
        const normalized = Math.max(0, Math.min(1, (value - low) / spread));
        const y = definition.key === 'pace'
          ? definition.top + normalized * (definition.bottom - definition.top)
          : definition.bottom - normalized * (definition.bottom - definition.top);
        return [`${x.toFixed(1)},${y.toFixed(1)}`];
      }).join(' ');

      return {
        key: definition.key,
        label: definition.label,
        color: definition.color,
        top: definition.top,
        bottom: definition.bottom,
        points,
        minimum: numbers.length ? this.chartLabel(low, definition.unit) : '—',
        maximum: numbers.length ? this.chartLabel(high, definition.unit) : '—',
        available: numbers.length > 1,
      };
    });

    const ticks = Array.from({ length: 5 }, (_, index) => {
      const portion = index / 4;
      return { x: plotLeft + portion * plotWidth, label: this.axisTime(duration * portion) };
    });
    return { lanes, ticks, hasSamples: lanes.some((lane) => lane.available) };
  }

  private timestamp(value: string | null): number | null {
    if (!value) return null;
    const parsed = Date.parse(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  private roundRange(value: number, unit: string, up: boolean): number {
    const step = unit === 'bpm' ? 5 : unit === 'pace' ? 30 : unit === 'm' ? 10 : 1;
    return (up ? Math.ceil(value / step) : Math.floor(value / step)) * step;
  }

  private chartLabel(value: number, unit: string): string {
    if (unit === 'pace') return this.formatPace(value).replace(' /km', '');
    return unit === 'm' ? `${Math.round(value)} m` : `${Math.round(value)} bpm`;
  }

  private axisTime(seconds: number): string {
    const total = Math.round(seconds);
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const remainder = total % 60;
    return hours > 0
      ? `${hours}:${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
      : `${minutes}:${String(remainder).padStart(2, '0')}`;
  }
}
