import { Routes } from '@angular/router';

export const learningRoutes: Routes = [
  {
    path: 'test',
    title: 'Test słownictwa · Verbulio',
    loadComponent: () => import('./local-placement').then((module) => module.LocalPlacement),
  },
  {
    path: 'rejestracja',
    title: 'Załóż konto · Verbulio',
    loadComponent: () => import('./local-registration').then((module) => module.LocalRegistration),
  },
  {
    path: 'logowanie',
    title: 'Zaloguj się · Verbulio',
    loadComponent: () => import('./local-login').then((module) => module.LocalLogin),
  },
  {
    path: 'nauka',
    title: 'Sesja nauki · Verbulio',
    data: { accountOwned: true },
    loadComponent: () => import('./local-learning').then((module) => module.LocalLearning),
  },
  {
    path: 'profil',
    title: 'Twój profil · Verbulio',
    loadComponent: () => import('./local-profile').then((module) => module.LocalProfile),
  },
  {
    path: 'ustawienia',
    title: 'Ustawienia · Verbulio',
    loadComponent: () =>
      import('./local-learning-settings').then((module) => module.LocalLearningSettings),
  },
  {
    path: 'prywatnosc/eksport',
    title: 'Kopia Twoich danych · Verbulio',
    loadComponent: () =>
      import('./local-account-export').then((module) => module.LocalAccountExport),
  },
  {
    path: 'lokalna-nauka',
    title: 'Sesja nauki · Verbulio',
    loadComponent: () => import('./local-learning').then((module) => module.LocalLearning),
  },
];
