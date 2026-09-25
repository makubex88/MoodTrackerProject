import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';

import { AdminAuthService } from '../../core/admin-auth.service';

/** The sign-in gate. One field; the password is exchanged for an HttpOnly ticket cookie and never stored here. */
@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [ReactiveFormsModule],
  template: `
    <section class="card gate-card">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h1 class="title">Admin sign-in</h1>
        <p class="subtitle">This page lists every mood entry. Enter the admin password to continue.</p>

        @if (error) {
          <div class="alert" role="alert">{{ error }}</div>
        }

        <label class="field-label" for="password">Password</label>
        <input
          id="password"
          class="input"
          type="password"
          formControlName="password"
          autocomplete="current-password"
          [class.invalid]="error !== null" />

        <button type="submit" class="btn primary" [disabled]="form.invalid || submitting">
          {{ submitting ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
    </section>
  `,
})
export class AdminLoginComponent {
  private readonly auth = inject(AdminAuthService);
  private readonly router = inject(Router);

  readonly form = new FormGroup({
    password: new FormControl<string>('', { nonNullable: true, validators: [Validators.required] }),
  });

  error: string | null = null;
  submitting = false;

  submit(): void {
    if (this.form.invalid || this.submitting) {
      return;
    }
    this.submitting = true;
    this.error = null;

    this.auth.login(this.form.controls.password.value).subscribe({
      next: () => {
        this.submitting = false;
        void this.router.navigateByUrl('/admin');
      },
      error: (err: HttpErrorResponse) => {
        this.submitting = false;
        this.error = err.status === 401
          ? 'That password isn’t right.'
          : 'We couldn’t sign you in. Check your connection and try again.';
        this.form.controls.password.reset('');
      },
    });
  }
}
