import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface MoodEntry {
  id: number;
  rating: number;
  comment: string | null;
  /** yyyy-MM-dd, in the team's time zone. */
  entryDate: string;
  /** ISO-8601 instant. */
  createdAtUtc: string;
}

export interface TodayResponse {
  logged: boolean;
  entry: MoodEntry | null;
  /** The team-local date the server is using as "today". */
  today: string;
  timeZone: string;
}

/**
 * Participant API. Identity rides along as an HttpOnly cookie the browser attaches on its own —
 * there is deliberately no identity code on the client.
 */
@Injectable({ providedIn: 'root' })
export class MoodService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/moods`;

  getToday(): Observable<TodayResponse> {
    return this.http.get<TodayResponse>(`${this.base}/today`);
  }

  create(rating: number, comment: string | null): Observable<MoodEntry> {
    return this.http.post<MoodEntry>(this.base, { rating, comment });
  }
}
