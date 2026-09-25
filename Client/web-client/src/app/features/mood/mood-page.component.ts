import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

import { MoodFaceComponent } from '../../core/mood-face.component';
import { MAX_COMMENT_LENGTH, MOOD_OPTIONS, moodLabel } from '../../core/mood-options';
import { MoodEntry, MoodService } from '../../core/mood.service';
import { TeamTimePipe } from '../../core/team-time.pipe';

export type MoodViewState = 'loading' | 'form' | 'locked' | 'unavailable';

export const ALREADY_LOGGED_MESSAGE = 'You’ve already logged your mood for today. Come back tomorrow.';
export const NETWORK_MESSAGE = 'We couldn’t save that. Check your connection and try again.';

/**
 * The one participant screen, in three states: the form, the locked view once today is logged,
 * and the locked view with an error banner when a second attempt is refused (409).
 */
@Component({
  selector: 'app-mood-page',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, MoodFaceComponent, TeamTimePipe],
  templateUrl: './mood-page.component.html',
})
export class MoodPageComponent implements OnInit {
  private readonly moods = inject(MoodService);

  readonly options = MOOD_OPTIONS;
  readonly maxComment = MAX_COMMENT_LENGTH;
  readonly label = moodLabel;

  state: MoodViewState = 'loading';
  today: string | null = null;
  timeZone: string | null = null;
  entry: MoodEntry | null = null;
  error: string | null = null;
  submitting = false;

  readonly form = new FormGroup({
    rating: new FormControl<number | null>(null, { validators: [Validators.required] }),
    comment: new FormControl<string>('', { nonNullable: true, validators: [Validators.maxLength(MAX_COMMENT_LENGTH)] }),
  });

  get commentLength(): number {
    return this.form.controls.comment.value.length;
  }

  ngOnInit(): void {
    this.load();
  }

  /** Ask the server which view to render. Never renders the form to someone who can't submit. */
  load(): void {
    this.state = 'loading';
    this.moods.getToday().subscribe({
      next: (res) => {
        this.today = res.today;
        this.timeZone = res.timeZone;
        if (res.logged && res.entry) {
          this.entry = res.entry;
          this.state = 'locked';
        } else {
          this.state = 'form';
        }
      },
      error: () => {
        this.state = 'unavailable';
      },
    });
  }

  submit(): void {
    if (this.form.invalid || this.submitting) {
      return;
    }

    const { rating, comment } = this.form.getRawValue();
    if (rating === null) {
      return;
    }

    this.submitting = true;
    this.error = null;

    const trimmed = comment.trim();
    this.moods.create(rating, trimmed.length ? trimmed : null).subscribe({
      next: (entry) => {
        this.submitting = false;
        this.entry = entry;
        this.state = 'locked';
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        if (err.status === 409) {
          // Someone (this browser, another tab) got there first. Show the error over the locked view.
          this.error = typeof err.error?.detail === 'string' ? err.error.detail : ALREADY_LOGGED_MESSAGE;
          this.load();
        } else if (err.status === 400) {
          this.error = firstValidationMessage(err) ?? 'That didn’t look right — check the form and try again.';
        } else {
          this.error = NETWORK_MESSAGE;
        }
      },
    });
  }
}

function firstValidationMessage(err: HttpErrorResponse): string | null {
  const errors = err.error?.errors;
  if (errors && typeof errors === 'object') {
    for (const key of Object.keys(errors)) {
      const messages = errors[key];
      if (Array.isArray(messages) && messages.length) {
        return String(messages[0]);
      }
    }
  }
  return null;
}
