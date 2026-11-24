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
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { watchAccountLink } from './account-link';
import { accountEndpoint, accountRequestOptions } from './account-session-client';

@Component({
  selector: 'app-local-password-reset',
  imports: [RouterLink],
  styles: ':host { display: contents; }',
  templateUrl: './local-password-reset.html',
})
export class LocalPasswordReset {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');
  private readonly passwordField = viewChild<ElementRef<HTMLInputElement>>('passwordField');
  private readonly linkChanged = new Subject<void>();
  private token: string | null = null;

  protected readonly secure = location.protocol === 'https:';
  protected readonly state = signal<'form' | 'invalid' | 'success'>('invalid');
  protected readonly pending = signal(false);
  protected readonly visible = signal(false);
  protected readonly error = signal('');
  protected readonly passwordError = signal('');
  protected readonly passwordHint = 'Użyj od 12 do 128 znaków.';

  constructor() {
    watchAccountLink((token) => {
      this.linkChanged.next();
      this.clearPassword();
      this.token = token;
      this.pending.set(false);
      this.error.set('');
      this.passwordError.set('');
      this.state.set(token ? 'form' : 'invalid');
      this.focusHeading();
    });
    this.focusHeading();
  }

  protected save(event: SubmitEvent, field: HTMLInputElement): void {
    event.preventDefault();
    if (!this.secure || !this.token || this.pending()) return;

    const password = field.value;
    const length = [...password].length;
    this.error.set('');
    this.passwordError.set('');
    if (password.length > 256 || length < 12 || length > 128) {
      this.passwordError.set(this.passwordHint);
      field.focus();
      return;
    }

    this.clearPassword();
    this.pending.set(true);
    this.http
      .post(
        `${accountEndpoint}/password-reset/complete`,
        { token: this.token, password },
        { ...accountRequestOptions, observe: 'response' },
      )
      .pipe(takeUntil(this.linkChanged), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.pending.set(false);
          if (response.status !== 204) {
            this.error.set(
              'Nie udało się potwierdzić zmiany hasła. Wpisz je ponownie i spróbuj jeszcze raz.',
            );
            return;
          }
          this.token = null;
          this.state.set('success');
          this.focusHeading();
        },
        error: (error: unknown) => {
          this.pending.set(false);
          if (
            error instanceof HttpErrorResponse &&
            error.status === 400 &&
            error.error?.code === 'invalid_password_reset_token'
          ) {
            this.token = null;
            this.state.set('invalid');
            this.focusHeading();
          } else if (
            error instanceof HttpErrorResponse &&
            error.status === 400 &&
            error.error?.code === 'invalid_new_password'
          ) {
            this.passwordError.set(this.passwordHint);
            field.focus();
          } else if (error instanceof HttpErrorResponse && error.status === 429) {
            this.error.set('Za dużo prób. Wpisz hasło ponownie i spróbuj za chwilę.');
          } else {
            this.error.set(
              'Nie udało się potwierdzić zmiany hasła. Wpisz je ponownie i spróbuj jeszcze raz.',
            );
          }
        },
      });
  }

  private clearPassword(): void {
    const field = this.passwordField()?.nativeElement;
    if (field) field.value = '';
    this.visible.set(false);
  }

  private focusHeading(): void {
    afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
  }
}
