import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../environments/environment';
import { MoodService, TodayResponse } from './mood.service';

describe('MoodService', () => {
  let service: MoodService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(MoodService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('FE-04 asks for today at environment.apiUrl, never a hard-coded host', () => {
    let result: TodayResponse | undefined;
    service.getToday().subscribe((r) => (result = r));

    const req = http.expectOne(`${environment.apiUrl}/moods/today`);
    expect(req.request.method).toBe('GET');
    req.flush({ logged: false, entry: null, today: '2026-09-23', timeZone: 'Australia/Melbourne' });

    expect(result?.logged).toBeFalse();
    expect(result?.today).toBe('2026-09-23');
  });

  it('FE-04 posts the numeric rating and the comment as JSON, with null for no comment', () => {
    service.create(3, null).subscribe();

    const req = http.expectOne(`${environment.apiUrl}/moods`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ rating: 3, comment: null });
    req.flush({ id: 4, rating: 3, comment: null, entryDate: '2026-09-23', createdAtUtc: '2026-09-22T23:14:05Z' });
  });

  it('carries no identity in the request: the cookie is the browser’s job', () => {
    service.create(2, 'meh').subscribe();

    const req = http.expectOne(`${environment.apiUrl}/moods`);
    expect(req.request.headers.keys()).not.toContain('X-Participant-Id');
    expect(Object.keys(req.request.body)).toEqual(['rating', 'comment']);
    req.flush({});
  });
});
