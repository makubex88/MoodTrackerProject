import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MoodEntry } from '../../core/mood.service';
import { ALREADY_LOGGED_MESSAGE, MoodPageComponent, NETWORK_MESSAGE } from './mood-page.component';

describe('MoodPageComponent', () => {
  let fixture: ComponentFixture<MoodPageComponent>;
  let http: HttpTestingController;

  const entry: MoodEntry = {
    id: 4,
    rating: 3,
    comment: 'Release went out clean.',
    entryDate: '2026-09-23',
    createdAtUtc: '2026-09-22T23:14:05Z',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MoodPageComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(MoodPageComponent);
    fixture.detectChanges(); // ngOnInit → GET today
  });

  afterEach(() => http.verify());

  const el = <T extends Element>(selector: string): T | null => fixture.nativeElement.querySelector(selector);
  const all = <T extends Element>(selector: string): T[] => Array.from(fixture.nativeElement.querySelectorAll(selector));

  function answerToday(logged: boolean, e: MoodEntry | null = null): void {
    http.expectOne('/api/moods/today').flush({ logged, entry: e, today: '2026-09-23', timeZone: 'Australia/Melbourne' });
    fixture.detectChanges();
  }

  it('shows a loading state until the server says which view to render', () => {
    expect(el('[role="status"]')).not.toBeNull();
    expect(el('form')).toBeNull();
    answerToday(false);
  });

  it('FE-01 renders exactly four native radio options with the brief’s labels, in order', () => {
    answerToday(false);

    const radios = all<HTMLInputElement>('input[type="radio"]');
    expect(radios.length).toBe(4);
    expect(new Set(radios.map((r) => r.name)).size)
      .withContext('all four share one name so arrow keys move between them')
      .toBe(1);
    expect(all('.opt-label').map((l) => l.textContent?.trim())).toEqual([
      'Not good at all',
      'A bit “meh”',
      'Pretty good',
      'Feeling great',
    ]);
    expect(el('fieldset legend')).not.toBeNull();
  });

  it('FE-02 submit stays disabled until a mood is chosen', () => {
    answerToday(false);
    const button = el<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBeTrue();

    fixture.componentInstance.form.controls.rating.setValue(3);
    fixture.detectChanges();

    expect(button.disabled).toBeFalse();
  });

  it('FE-03 the comment is optional, the counter tracks its length, and 501 characters block submission', () => {
    answerToday(false);
    const { form } = fixture.componentInstance;
    form.controls.rating.setValue(2);
    fixture.detectChanges();
    expect(el<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeFalse();
    expect(el('.counter')!.textContent).toContain('0 / 500');

    form.controls.comment.setValue('x'.repeat(500));
    fixture.detectChanges();
    expect(el('.counter')!.textContent).toContain('500 / 500');
    expect(el<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeFalse();

    form.controls.comment.setValue('x'.repeat(501));
    fixture.detectChanges();
    expect(el<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeTrue();
    expect(el('.counter')!.classList).toContain('over');
  });

  it('submits the numeric rating and a trimmed comment, then swaps the form for the locked view', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(3);
    component.form.controls.comment.setValue('  Release went out clean.  ');

    component.submit();
    const req = http.expectOne('/api/moods');
    expect(req.request.body).toEqual({ rating: 3, comment: 'Release went out clean.' });
    req.flush(entry, { status: 201, statusText: 'Created' });
    fixture.detectChanges();

    expect(el('form')).toBeNull();
    expect(el('.locked')).not.toBeNull();
    expect(el('.chip')!.textContent).toContain('Pretty good');
    expect(el('.quote')!.textContent).toContain('Release went out clean.');
  });

  it('sends null, not an empty string, when the comment is blank', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(1);
    component.form.controls.comment.setValue('   ');

    component.submit();
    const req = http.expectOne('/api/moods');
    expect(req.request.body).toEqual({ rating: 1, comment: null });
    req.flush(entry, { status: 201, statusText: 'Created' });
  });

  it('FE-05 a 409 shows the server’s message and locks the view with the existing entry', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(4);

    component.submit();
    http.expectOne('/api/moods').flush(
      { title: 'Mood already logged', status: 409, detail: ALREADY_LOGGED_MESSAGE, entryDate: '2026-09-23' },
      { status: 409, statusText: 'Conflict' },
    );
    fixture.detectChanges();
    // The component re-asks the server which view to render and gets the entry that won.
    answerToday(true, entry);

    expect(el('form')).toBeNull();
    expect(el('[role="alert"]')!.textContent).toContain(ALREADY_LOGGED_MESSAGE);
    expect(el('.locked')).not.toBeNull();
    expect(el('.chip')!.textContent).toContain('Pretty good');
  });

  it('FE-06 when today is already logged the locked view renders on load and the form never appears', () => {
    answerToday(true, entry);

    expect(el('form')).toBeNull();
    expect(el('.locked')).not.toBeNull();
    expect(el('h1')!.textContent).toContain('all set for today');
    expect(el('[role="alert"]')).toBeNull();
  });

  it('FE-07 a network failure on submit shows the retry message and keeps the form usable', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(2);

    component.submit();
    http.expectOne('/api/moods').error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(el('form')).not.toBeNull();
    expect(el('[role="alert"]')!.textContent).toContain(NETWORK_MESSAGE);
    expect(el<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeFalse();
  });

  it('a 400 surfaces the first validation message from the API', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(2);

    component.submit();
    http.expectOne('/api/moods').flush(
      { status: 400, errors: { Comment: ['Comments are limited to 500 characters.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    fixture.detectChanges();

    expect(el('[role="alert"]')!.textContent).toContain('Comments are limited to 500 characters.');
  });

  it('disables submit while a request is in flight so an impatient second click cannot double-post', () => {
    answerToday(false);
    const component = fixture.componentInstance;
    component.form.controls.rating.setValue(3);

    component.submit();
    fixture.detectChanges();
    expect(el<HTMLButtonElement>('button[type="submit"]')!.disabled).toBeTrue();
    expect(el('button[type="submit"]')!.textContent).toContain('Saving');

    component.submit(); // second click while pending
    http.expectOne('/api/moods').flush(entry, { status: 201, statusText: 'Created' });
    http.expectNone('/api/moods');
  });

  it('shows an unavailable state with a retry when the first call fails', () => {
    http.expectOne('/api/moods/today').error(new ProgressEvent('error'), { status: 0 });
    fixture.detectChanges();

    expect(el('form')).toBeNull();
    expect(el('[role="alert"]')!.textContent).toContain('reach the server');

    fixture.componentInstance.load();
    answerToday(false);
    expect(el('form')).not.toBeNull();
  });
});
