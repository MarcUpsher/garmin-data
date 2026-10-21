import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { DataManagement } from './data-management';

describe('DataManagement', () => {
  it('displays API problem details and avoids starting a second import', async () => {
    localStorage.removeItem('garmin-connect-export-directory');
    await TestBed.configureTestingModule({ imports: [DataManagement], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] }).compileComponents();
    const fixture = TestBed.createComponent(DataManagement); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/wellness/status').flush({ sleepRecords: 1, dailyWellnessRecords: 1, vo2MaxRecords: 1 });
    const page = fixture.nativeElement as HTMLElement;
    const input = page.querySelector<HTMLInputElement>('input')!;
    input.value = '/fixture/DI_CONNECT'; input.dispatchEvent(new Event('input'));
    const button = page.querySelector<HTMLButtonElement>('.path-row button')!;
    button.click(); button.click();
    http.expectOne('/api/wellness/import').flush({ detail: 'An export file contains invalid JSON. The import was not saved.' }, { status: 400, statusText: 'Bad Request' });
    await fixture.whenStable(); fixture.detectChanges();
    expect(page.querySelector('[role="alert"]')?.textContent).toContain('invalid JSON');
    expect(page.textContent).not.toContain('[object Object]');
    http.verify(); localStorage.removeItem('garmin-connect-export-directory');
  });
});
