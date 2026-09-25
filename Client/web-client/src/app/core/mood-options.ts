/**
 * The four moods, worst to best, displayed exactly as the brief lists them. This is the only place
 * the labels exist — the API speaks numbers (1–4), never strings.
 */
export type MoodValue = 1 | 2 | 3 | 4;

export interface MoodOption {
  readonly value: MoodValue;
  readonly label: string;
}

export const MOOD_OPTIONS: readonly MoodOption[] = [
  { value: 1, label: 'Not good at all' },
  { value: 2, label: 'A bit “meh”' },
  { value: 3, label: 'Pretty good' },
  { value: 4, label: 'Feeling great' },
];

export function moodLabel(value: number): string {
  return MOOD_OPTIONS.find((o) => o.value === value)?.label ?? 'Unknown';
}

export const MAX_COMMENT_LENGTH = 500;
