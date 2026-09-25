import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AdminMoodPage } from '../../core/admin-moods.service';
import { AdminPageComponent } from './admin-page.component';

describe('AdminPageComponent', () => {
  let fixture: ComponentFixture<AdminPageComponent>;
  let http: HttpTestingController;
  let router: Router;

  const page: AdminMoodPage = {
    items: [
      { id: 5, participantId: '2d91f0a3-6c4e-4b18-9a5d-7e2b1c9fe4af', rating: 2, comment: 'Second thoughts.', entryDate: '2026-09-23', createdAtUtc: '2026-09-23T01:02:00Z' },
      { id: 4, participantId: '8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21', rating: 3, comment: 'Release went out clean.', entryDate: '2026-09-23', createdAtUtc: '2026-09-22T23:14:05Z' },
      { id: 1, participantId: 'a3f91c2b-1111-4222-8333-44445555c4c1', rating: 1, comment: null, entryDate: '2026-09-22', createdAtUtc: '2026-09-22T07:40:00Z' },
    ],
    total: 3,
    page: 1,
    pageSize: 50,
    countsByRating: { '1': 1, '2': 1, '3': 1, '4': 0 },
    timeZone: 'Australia/Melbourne',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl').and.resolveTo(true);

    fixture = TestBed.createComponent(AdminPageComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  const rows = (): HTMLTableRowElement[] => Array.from(fixture.nativeElement.querySelectorAll('tbody tr'));

  it('FE-09 asks for the first page of 50 and renders rows in the server’s order without re-sorting', () => {
    const req = http.expectOne((r) => r.url === '/api/admin/moods');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush(page);
    fixture.detectChanges();

    expect(rows().length).toBe(3);
    expect(rows().map((r) => r.cells[2].textContent?.trim())).toEqual(['Second thoughts.', 'Release went out clean.', '—']);
    expect(rows().map((r) => r.cells[1].textContent?.trim())).toEqual(['A bit “meh”', 'Pretty good', 'Not good at all']);
  });

  it('FE-09 formats timestamps in the team zone and shortens participant ids with the full id on hover', () => {
    http.expectOne((r) => r.url === '/api/admin/moods').flush(page);
    fixture.detectChanges();

    const first = rows()[1];
    expect(first.cells[0].textContent).toContain('23 Sep');
    expect(first.cells[0].textContent).toContain('09:14');
    expect(first.cells[3].textContent?.trim()).toBe('8f3c…7c21');
    expect(first.cells[3].getAttribute('title')).toBe('8f3c1a2e-5d7b-4e9a-b2c1-0f6d3a8e7c21');
  });

  it('shows the four counts over all rows, in the brief’s order', () => {
    http.expectOne((r) => r.url === '/api/admin/moods').flush(page);
    fixture.detectChanges();

    const stats: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('.stat'));
    expect(stats.map((s) => s.querySelector('.stat-label')?.textContent?.trim())).toEqual([
      'Not good at all', 'A bit “meh”', 'Pretty good', 'Feeling great',
    ]);
    expect(stats.map((s) => s.querySelector('.stat-value')?.textContent?.trim())).toEqual(['1', '1', '1', '0']);
  });

  it('a 401 from the list sends the visitor to the sign-in gate', () => {
    http.expectOne((r) => r.url === '/api/admin/moods').flush(null, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/admin/login');
  });

  it('any other failure shows the retry state', () => {
    http.expectOne((r) => r.url === '/api/admin/moods').flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('couldn’t load');
    expect(rows().length).toBe(0);
  });

  it('an empty log says so rather than rendering an empty table', () => {
    http.expectOne((r) => r.url === '/api/admin/moods').flush({ ...page, items: [], total: 0, countsByRating: { '1': 0, '2': 0, '3': 0, '4': 0 } });
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('table')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('No moods have been logged yet');
  });
});
