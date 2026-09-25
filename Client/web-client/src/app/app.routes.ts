import { Routes } from '@angular/router';

import { adminGuard } from './core/admin.guard';
import { AdminLoginComponent } from './features/admin/admin-login.component';
import { AdminPageComponent } from './features/admin/admin-page.component';
import { MoodPageComponent } from './features/mood/mood-page.component';

export const routes: Routes = [
  { path: '', component: MoodPageComponent, title: 'Mood Tracker' },
  { path: 'admin/login', component: AdminLoginComponent, title: 'Admin sign-in · Mood Tracker' },
  { path: 'admin', component: AdminPageComponent, canActivate: [adminGuard], title: 'All mood entries · Mood Tracker' },
  { path: '**', redirectTo: '' },
];
