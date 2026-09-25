import { Pipe, PipeTransform } from '@angular/core';

export type TeamTimeMode = 'time' | 'date' | 'datetime';

/**
 * Formats a UTC instant in the team's time zone (an IANA id the API reports), so the admin list and
 * the "Logged 09:14" stamp agree with the team's day regardless of where the viewer is sitting.
 */
@Pipe({ name: 'teamTime', standalone: true })
export class TeamTimePipe implements PipeTransform {
  transform(value: string | Date | null | undefined, timeZone: string | null | undefined, mode: TeamTimeMode = 'datetime'): string {
    if (!value) {
      return '';
    }
    const date = typeof value === 'string' ? new Date(value) : value;
    if (Number.isNaN(date.getTime())) {
      return '';
    }

    const base: Intl.DateTimeFormatOptions = { timeZone: timeZone || undefined, hourCycle: 'h23' };
    const options: Intl.DateTimeFormatOptions =
      mode === 'time'
        ? { ...base, hour: '2-digit', minute: '2-digit', timeZoneName: 'short' }
        : mode === 'date'
          ? { ...base, day: 'numeric', month: 'short' }
          : { ...base, day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' };

    try {
      return new Intl.DateTimeFormat('en-AU', options).format(date);
    } catch {
      // Unknown time zone on this browser: fall back to the viewer's zone rather than showing nothing.
      return new Intl.DateTimeFormat('en-AU', { ...options, timeZone: undefined }).format(date);
    }
  }
}
