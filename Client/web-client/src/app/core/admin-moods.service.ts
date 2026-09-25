import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

export interface AdminMoodEntry {
  id: number;
  participantId: string;
  rating: number;
  comment: string | null;
  entryDate: string;
  createdAtUtc: string;
}

export interface AdminMoodPage {
  items: AdminMoodEntry[];
  total: number;
  page: number;
  pageSize: number;
  /** Keys are the rating values "1".."4"; counts are over every row, not just this page. */
  countsByRating: Record<string, number>;
  timeZone: string;
}

@Injectable({ providedIn: 'root' })
export class AdminMoodsService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin/moods`;

  list(page = 1, pageSize = 50): Observable<AdminMoodPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<AdminMoodPage>(this.base, { params });
  }
}
