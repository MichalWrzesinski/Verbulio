import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { clearAccountLearningStorage } from './account-learning-storage';
import { AccountSessionClient, accountEndpoint } from './account-session-client';
import { LearningPreferences } from './learning-preferences';

@Component({
  selector: 'app-local-learning-settings',
  imports: [RouterLink],
  templateUrl: './local-learning-settings.html',
  styles: ':host { display: contents; }',
})
export class LocalLearningSettings {
  private readonly http = inject(HttpClient);
  private readonly sessions = inject(AccountSessionClient);
  private readonly router = inject(Router);
  private readonly preferences = inject(LearningPreferences);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  protected readonly enabled = signal<boolean | null>(null);
  protected readonly pending = signal(false);
  protected readonly signInRequired = signal(false);
  protected readonly error = signal('');
  protected readonly saved = signal(false);
  protected readonly email = signal<string | null>(null);
  protected readonly contactError = signal(false);
  protected readonly signingOut = signal(false);
  protected readonly logoutError = signal('');

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
    this.load();
    this.loadContact();
  }

  protected load(): void {
    this.pending.set(true);
    this.error.set('');
    this.saved.set(false);
    this.preferences
      .read()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ autoAdvance }) => {
          this.enabled.set(autoAdvance);
          this.pending.set(false);
        },
        error: (error: unknown) => this.failed(error, 'Nie udało się wczytać ustawienia.'),
      });
  }

  protected save(control: HTMLInputElement): void {
    const previous = this.enabled();
    if (previous === null || this.pending()) return;

    this.pending.set(true);
    this.saved.set(false);
    this.error.set('');
    this.enabled.set(control.checked);
    this.preferences
      .save(control.checked)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ autoAdvance }) => {
          this.enabled.set(autoAdvance);
          this.pending.set(false);
          this.saved.set(true);
        },
        error: (error: unknown) => {
          this.enabled.set(previous);
          this.failed(error, 'Nie udało się potwierdzić zapisu. Odśwież ustawienie.');
        },
      });
  }

  protected loadContact(): void {
    this.email.set(null);
    this.contactError.set(false);
    this.http
      .get<{ email: string }>(`${accountEndpoint}/settings/account`, { timeout: 10_000 })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ email }) => this.email.set(email),
        error: (error: unknown) => {
          if (!this.showSignIn(error)) this.contactError.set(true);
        },
      });
  }

  protected async logout(): Promise<void> {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.logoutError.set('');
    try {
      const remaining = await this.sessions.logout();
      if (this.destroyRef.destroyed) return;
      if (!remaining) {
        this.email.set(null);
        this.signInRequired.set(true);
        await this.router.navigateByUrl('/');
      } else {
        this.loadContact();
        this.load();
        this.logoutError.set('Sesja nadal trwa. Spróbuj wylogować się ponownie.');
      }
    } catch {
      if (!this.destroyRef.destroyed)
        this.logoutError.set('Nie udało się potwierdzić wylogowania.');
    } finally {
      this.signingOut.set(false);
    }
  }

  private showSignIn(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse) || error.status !== 401) return false;

    clearAccountLearningStorage();
    this.email.set(null);
    this.enabled.set(null);
    this.signInRequired.set(true);
    return true;
  }

  private failed(error: unknown, message: string): void {
    this.pending.set(false);
    if (!this.showSignIn(error)) this.error.set(message);
  }
}
