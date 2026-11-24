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
  selector: 'app-local-email-confirmation',
  imports: [RouterLink],
  styles: ':host { display: contents; }',
  template: `
    <div class="app">
      <header class="topbar">
        <a class="brand" routerLink="/" aria-label="Verbulio, strona startowa">
          <img src="brand/logo-full.svg" alt="" />
        </a>
      </header>
      <main class="focus-screen">
        @if (confirmed()) {
          <div class="big-ic big-ic--ok" aria-hidden="true">
            <svg class="i" viewBox="0 0 20 20" focusable="false">
              <path
                d="M4 10.5l4 4 8-9"
                fill="none"
                stroke="currentColor"
                stroke-width="3.2"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </div>
        }
        <div class="stack">
          <h1 #heading class="h1" tabindex="-1">
            {{ confirmed() ? 'Adres potwierdzony' : 'Potwierdź adres e-mail' }}
          </h1>
          @if (confirmed()) {
            <p class="lead">Adres e-mail Twojego konta został potwierdzony.</p>
          } @else if (!hasToken()) {
            <p class="lead">Otwórz aktualny link z wiadomości potwierdzającej.</p>
          }
          @if (error()) {
            <p class="lead" role="alert">{{ error() }}</p>
          }
        </div>
      </main>
      @if (hasToken()) {
        <button
          class="key btn btn--primary"
          type="button"
          [disabled]="pending()"
          (click)="confirm()"
        >
          {{ pending() ? 'Potwierdzam…' : 'Potwierdź adres' }}
        </button>
      } @else {
        <a class="key btn btn--primary" routerLink="/logowanie">Przejdź do logowania</a>
      }
    </div>
    <footer class="legal-footer">
      <p>Copyright 2026 Verbulio</p>
      <nav aria-label="Dokumenty prawne">
        <a class="link" routerLink="/regulamin">Regulamin</a>
        <a class="link" routerLink="/prywatnosc">Polityka prywatności</a>
      </nav>
    </footer>
  `,
})
export class LocalEmailConfirmation {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly injector = inject(Injector);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');
  private readonly linkChanged = new Subject<void>();
  private token: string | null = null;

  protected readonly pending = signal(false);
  protected readonly confirmed = signal(false);
  protected readonly error = signal('');
  protected readonly hasToken = signal(false);

  constructor() {
    watchAccountLink((token) => {
      this.linkChanged.next();
      this.token = token;
      this.hasToken.set(token !== null);
      this.pending.set(false);
      this.confirmed.set(false);
      this.error.set('');
      afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
    });
    afterNextRender(() => this.heading().nativeElement.focus());
  }

  protected confirm(): void {
    if (!this.token || this.pending()) return;

    this.pending.set(true);
    this.error.set('');
    this.http
      .post(
        `${accountEndpoint}/email-confirmation/confirm`,
        { token: this.token },
        {
          ...accountRequestOptions,
          observe: 'response',
        },
      )
      .pipe(takeUntil(this.linkChanged), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.pending.set(false);
          if (response.status !== 204) {
            this.error.set('Nie udało się potwierdzić adresu. Spróbuj ponownie.');
            return;
          }

          this.token = null;
          this.hasToken.set(false);
          this.confirmed.set(true);
          afterNextRender(() => this.heading().nativeElement.focus(), { injector: this.injector });
        },
        error: (error: unknown) => {
          this.pending.set(false);
          if (
            error instanceof HttpErrorResponse &&
            error.status === 400 &&
            error.error?.code === 'invalid_confirmation_token'
          ) {
            this.token = null;
            this.hasToken.set(false);
            this.error.set('Link potwierdzający jest nieprawidłowy lub wygasł.');
          } else {
            this.error.set('Nie udało się potwierdzić adresu. Spróbuj ponownie.');
          }
        },
      });
  }
}
