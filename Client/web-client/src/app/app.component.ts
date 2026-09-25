import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a routerLink="/" class="brand">Mood Tracker</a>
      <nav aria-label="Primary">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Today</a>
        <a routerLink="/admin" routerLinkActive="active">Admin</a>
      </nav>
    </header>
    <main class="page">
      <router-outlet />
    </main>
  `,
})
export class AppComponent {}
