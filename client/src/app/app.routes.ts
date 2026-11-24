import { Routes } from '@angular/router';
import { Landing } from './landing';
import { learningRoutes } from './learning.routes';
import { PendingPage } from './pending-page';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    component: Landing,
    data: { learningAvailable: learningRoutes.some((route) => route.path === 'test') },
  },
  ...learningRoutes,
  {
    path: 'test',
    title: 'Test słownictwa · Verbulio',
    component: PendingPage,
    data: { heading: 'Test słownictwa' },
  },
  {
    path: 'logowanie',
    title: 'Zaloguj się · Verbulio',
    component: PendingPage,
    data: { heading: 'Zaloguj się' },
  },
  {
    path: 'regulamin',
    title: 'Regulamin · Verbulio',
    component: PendingPage,
    data: { heading: 'Regulamin' },
  },
  {
    path: 'prywatnosc',
    title: 'Polityka prywatności · Verbulio',
    component: PendingPage,
    data: { heading: 'Polityka prywatności' },
  },
  { path: '**', redirectTo: '' },
];
