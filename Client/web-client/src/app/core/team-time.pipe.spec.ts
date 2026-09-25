import { TeamTimePipe } from './team-time.pipe';

describe('TeamTimePipe', () => {
  const pipe = new TeamTimePipe();

  it('renders a UTC instant in the team zone (09:14 AEST for 23:14Z the previous day)', () => {
    const out = pipe.transform('2026-09-22T23:14:05Z', 'Australia/Melbourne', 'time');

    expect(out).toContain('09:14');
    expect(out).not.toContain('23:14');
  });

  it('shows the team-local date, not the UTC date', () => {
    const out = pipe.transform('2026-09-22T23:14:05Z', 'Australia/Melbourne', 'datetime');

    expect(out).toContain('23 Sep');
  });

  it('honours daylight saving after the October switch (+11)', () => {
    const out = pipe.transform('2026-10-10T13:00:00Z', 'Australia/Melbourne', 'time');

    expect(out).toContain('00:00');
  });

  it('is empty for nothing and for an unparseable value', () => {
    expect(pipe.transform(null, 'Australia/Melbourne')).toBe('');
    expect(pipe.transform('', 'Australia/Melbourne')).toBe('');
    expect(pipe.transform('not a date', 'Australia/Melbourne')).toBe('');
  });

  it('falls back to the viewer’s zone rather than throwing when the zone id is unknown', () => {
    expect(() => pipe.transform('2026-09-22T23:14:05Z', 'Mars/Olympus_Mons', 'time')).not.toThrow();
    expect(pipe.transform('2026-09-22T23:14:05Z', 'Mars/Olympus_Mons', 'time')).not.toBe('');
  });
});
