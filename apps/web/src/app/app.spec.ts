import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    localStorage.removeItem('garmin-theme');
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('loads and displays the activity list', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne((request) => request.url === '/api/activities').flush({
      items: [], totalCount: 753, page: 1, pageSize: 25,
    });
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Activities');
    expect(compiled.querySelector('.count-number')?.textContent).toContain('753');
    expect(compiled.querySelector('.app-shell')?.classList.contains('dark-theme')).toBe(true);
  });

  it('toggles and remembers the light theme preference', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne((request) => request.url === '/api/activities').flush({
      items: [], totalCount: 0, page: 1, pageSize: 25,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    const themeButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.theme-toggle')!;
    themeButton.click();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.app-shell')?.classList.contains('dark-theme')).toBe(false);
    expect(localStorage.getItem('garmin-theme')).toBe('light');
    http.verify();
  });

  it('syncs the watch and reloads the activity list', async () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne((request) => request.url === '/api/activities').flush({
      items: [], totalCount: 0, page: 1, pageSize: 25,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.sync-button')!.click();
    fixture.detectChanges();
    const syncRequest = http.expectOne('/api/sync');
    expect(syncRequest.request.method).toBe('POST');
    syncRequest.flush({ imported: 1, skipped: 4, failed: 0, recordCount: 10, lapCount: 2, filesScanned: 5, filesHashed: 1, errors: [], mount: '/media/watch' });
    http.expectOne((request) => request.url === '/api/activities').flush({
      items: [], totalCount: 1, page: 1, pageSize: 25,
    });
    await fixture.whenStable();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.sync-notice')?.textContent)
      .toContain('imported 1 new activity');
    http.verify();
  });
});
