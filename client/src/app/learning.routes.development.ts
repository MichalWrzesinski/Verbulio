import { Routes } from '@angular/router';

export const learningRoutes: Routes = [
  {
    path: 'lokalna-nauka',
    title: 'Sesja nauki · Verbulio',
    loadComponent: () => import('./local-learning').then((module) => module.LocalLearning),
  },
];
