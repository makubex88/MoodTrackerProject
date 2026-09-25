import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../environments/environment';

/**
 * Admin sign-in. The ticket is an HttpOnly cookie issued by the server; nothing about the session is
 * stored on the client, which is why "am I signed in?" is a request rather than a lookup.
 */
@Injectable({ providedIn: 'root' })
export class AdminAuthService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  login(password: string): Observable<void> {
    return this.http.post<void>(`${this.base}/login`, { password });
  }

  me(): Observable<void> {
    return this.http.get<void>(`${this.base}/me`);
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.base}/logout`, {});
  }
}
