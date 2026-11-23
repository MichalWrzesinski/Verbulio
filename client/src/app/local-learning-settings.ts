import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { clearAccountLearningStorage } from './account-learning-storage';
import {
  AccountSessionClient,
  accountEndpoint,
  accountRequestOptions,
} from './account-session-client';
import { LearningPreferences } from './learning-preferences';

@Component({
  selector: 'app-local-learning-settings',
  imports: [RouterLink],
  templateUrl: './local-learning-settings.html',
  styles: `
    :host {
      display: contents;
    }
    dialog.dialog:not([open]) {
      display: none;
    }
    dialog.dialog[open] {
      margin: auto;
      width: calc(100% - 32px);
      max-height: calc(100dvh - 32px);
      overflow: auto;
    }
    @media (max-width: 639px) {
      dialog.dialog[open] {
        margin-bottom: 16px;
      }
    }
    button.set-danger {
      align-self: flex-start;
    }
    #delete-error {
      color: var(--color-ink);
    }
    dialog::backdrop {
      background: var(--backdrop);
    }
  `,
})
export class LocalLearningSettings {
  private readonly http = inject(HttpClient);
  private readonly sessions = inject(AccountSessionClient);
  private readonly router = inject(Router);
  private readonly preferences = inject(LearningPreferences);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
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
  protected readonly deletionAvailable = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleted = signal(false);
  protected readonly deletionUnconfirmed = signal(false);
  protected readonly deleteError = signal('');
  protected readonly wrongPassword = signal(false);

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
    this.deletionAvailable.set(false);
    this.contactError.set(false);
    this.http
      .get<{ email: string; deletionAvailable: boolean }>(`${accountEndpoint}/settings/account`, {
        timeout: 10_000,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ email, deletionAvailable }) => {
          this.email.set(email);
          this.deletionAvailable.set(deletionAvailable === true);
        },
        error: (error: unknown) => {
          if (!this.showSignIn(error)) this.contactError.set(true);
        },
      });
  }

  protected async logout(): Promise<void> {
    if (this.signingOut() || this.deleting()) return;
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

  protected guardDeleteDialogCancel(event: Event): void {
    if (this.deleting()) event.preventDefault();
  }

  protected resetDeleteDialog(password: HTMLInputElement): void {
    password.value = '';
    this.deleteError.set('');
    this.wrongPassword.set(false);
  }

  protected async confirmDeletion(
    event: SubmitEvent,
    dialog: HTMLDialogElement,
    password: HTMLInputElement,
  ): Promise<void> {
    event.preventDefault();
    if (this.deleting() || this.signingOut() || !this.deletionAvailable() || !password.value)
      return;

    const value = password.value;
    this.resetDeleteDialog(password);
    this.deleting.set(true);
    try {
      const response = await firstValueFrom(
        this.http.post(
          `${accountEndpoint}/delete`,
          { password: value },
          {
            ...accountRequestOptions,
            observe: 'response',
          },
        ),
      );
      if (response.status !== 204) throw new Error('Unconfirmed deletion');
      clearAccountLearningStorage();
      if (this.destroyRef.destroyed) return;
      this.email.set(null);
      this.enabled.set(null);
      dialog.close();
      this.deleted.set(true);
      this.focusHeading();
    } catch (error) {
      if (this.destroyRef.destroyed) return;
      if (this.showSignIn(error)) {
        this.deletionUnconfirmed.set(true);
        dialog.close();
        this.focusHeading();
      } else if (
        error instanceof HttpErrorResponse &&
        error.status === 403 &&
        error.error?.code === 'invalid_account_password'
      ) {
        this.wrongPassword.set(true);
        this.deleteError.set('Nieprawidłowe hasło.');
      } else if (error instanceof HttpErrorResponse && error.status === 429) {
        this.deleteError.set('Za dużo prób. Spróbuj ponownie za chwilę.');
      } else if (error instanceof HttpErrorResponse && error.status === 503) {
        this.deleteError.set('Usunięcie konta jest chwilowo niedostępne.');
      } else {
        this.deleteError.set('Nie udało się potwierdzić usunięcia konta.');
      }
    } finally {
      this.deleting.set(false);
      if (dialog.open && !this.destroyRef.destroyed) password.focus();
    }
  }

  private focusHeading(): void {
    afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
  }

  private showSignIn(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse) || error.status !== 401) return false;

    clearAccountLearningStorage();
    this.email.set(null);
    this.enabled.set(null);
    this.deletionAvailable.set(false);
    this.signInRequired.set(true);
    return true;
  }

  private failed(error: unknown, message: string): void {
    this.pending.set(false);
    if (!this.showSignIn(error)) this.error.set(message);
  }
}
