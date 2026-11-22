import { Routes } from '@angular/router';

export const learningRoutes: Routes = [
  {
    path: 'test',
    title: 'Test słownictwa · Verbulio',
    loadComponent: () => import('./local-placement').then((module) => module.LocalPlacement),
  },
  {
    path: 'lokalna-nauka',
    title: 'Sesja nauki · Verbulio',
    loadComponent: () => import('./local-learning').then((module) => module.LocalLearning),
  },
];
