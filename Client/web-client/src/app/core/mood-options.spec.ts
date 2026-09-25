import { MAX_COMMENT_LENGTH, MOOD_OPTIONS, moodLabel } from './mood-options';

describe('MOOD_OPTIONS', () => {
  it('FE-01 has exactly the four labels from the brief, worst to best, verbatim', () => {
    expect(MOOD_OPTIONS.map((o) => o.label)).toEqual([
      'Not good at all',
      'A bit “meh”',
      'Pretty good',
      'Feeling great',
    ]);
    expect(MOOD_OPTIONS.map((o) => o.value)).toEqual([1, 2, 3, 4]);
  });

  it('maps a value back to its label and is defensive about unknown values', () => {
    expect(moodLabel(3)).toBe('Pretty good');
    expect(moodLabel(0)).toBe('Unknown');
    expect(moodLabel(5)).toBe('Unknown');
  });

  it('agrees with the API on the comment limit', () => {
    expect(MAX_COMMENT_LENGTH).toBe(500);
  });
});
