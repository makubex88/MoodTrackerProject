import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';

import { AdminAuthService } from '../../core/admin-auth.service';
import { AdminMoodPage, AdminMoodsService } from '../../core/admin-moods.service';
import { MoodFaceComponent } from '../../core/mood-face.component';
import { MOOD_OPTIONS, moodLabel } from '../../core/mood-options';
import { TeamTimePipe } from '../../core/team-time.pipe';

export type AdminViewState = 'loading' | 'ready' | 'unavailable';

/**
 * Every entry, newest first, exactly as the server ordered it. The four counts above the table are
 * over all rows; the table shows the first page of 50.
 */
@Component({
  selector: 'app-admin-page',
  standalone: true,
  imports: [MoodFaceComponent, TeamTimePipe],
  templateUrl: './admin-page.component.html',
})
export class AdminPageComponent implements OnInit {
  private readonly moods = inject(AdminMoodsService);
  private readonly auth = inject(AdminAuthService);
  private readonly router = inject(Router);

  readonly options = MOOD_OPTIONS;
  readonly label = moodLabel;

  state: AdminViewState = 'loading';
  page: AdminMoodPage | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state = 'loading';
    this.moods.list(1, 50).subscribe({
      next: (page) => {
        this.page = page;
        this.state = 'ready';
      },
      error: (err) => {
        if (err?.status === 401) {
          void this.router.navigateByUrl('/admin/login');
          return;
        }
        this.state = 'unavailable';
      },
    });
  }

  count(rating: number): number {
    return this.page?.countsByRating[String(rating)] ?? 0;
  }

  /** Participant ids identify browsers, not people; a short form is enough to spot "the same one, three days running". */
  shortId(id: string): string {
    return id.length > 12 ? `${id.slice(0, 4)}…${id.slice(-4)}` : id;
  }

  signOut(): void {
    this.auth.logout().subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: () => void this.router.navigateByUrl('/'),
    });
  }
}
