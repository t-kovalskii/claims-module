import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: '',
    loadComponent: () => import('./shell/shell.component').then((m) => m.ShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'claims', pathMatch: 'full' },
      {
        path: 'claims',
        loadComponent: () =>
          import('./features/claims-dashboard/claims-dashboard.component').then(
            (m) => m.ClaimsDashboardComponent
          )
      },
      {
        // Must come before 'claims/:id' — otherwise the ':id' segment would swallow 'new'.
        path: 'claims/new',
        loadComponent: () =>
          import('./features/fnol-wizard/fnol-wizard.component').then((m) => m.FnolWizardComponent)
      },
      {
        path: 'claims/:id',
        loadComponent: () =>
          import('./features/claim-detail/claim-detail.component').then((m) => m.ClaimDetailComponent)
      }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
