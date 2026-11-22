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
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { clearAccountLearningStorage } from './account-learning-storage';

interface Account {
  id: string;
  startingLevel: string;
}

const endpoint = '/api/v1/local-account';
const options = { headers: { 'X-Verbulio-Client': 'local-learning' }, timeout: 10_000 };

@Component({
  selector: 'app-local-login',
  imports: [RouterLink],
  templateUrl: './local-login.html',
  styles: ':host { display: contents; }',
})
export class LocalLogin {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly secure = location.protocol === 'https:';
  protected readonly account = signal<Account | null>(null);
  protected readonly loading = signal(this.secure);
  protected readonly busy = signal(false);
  protected readonly visible = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly rejected = signal(false);

  constructor() {
    if (this.secure) void this.restore();
  }

  protected async login(event: SubmitEvent): Promise<void> {
    event.preventDefault();
    if (!this.secure || this.busy() || !(event.currentTarget instanceof HTMLFormElement)) return;

    const form = event.currentTarget;
    const data = new FormData(form);
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    this.rejected.set(false);
    this.visible.set(false);
    try {
      const account = await firstValueFrom(
        this.http.post<Account>(
          `${endpoint}/login`,
          {
            email: data.get('email'),
            password: data.get('password'),
          },
          options,
        ),
      );
      form.reset();
      this.account.set(account);
      this.focusHeading();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.rejected.set(true);
        this.error.set('Nieprawidłowy e-mail lub hasło.');
      } else if (error instanceof HttpErrorResponse && error.status === 429) {
        this.error.set('Za dużo prób logowania. Spróbuj ponownie za chwilę.');
      } else {
        try {
          const account = await this.readSession();
          if (account) {
            form.reset();
            this.account.set(account);
            this.notice.set(
              'Wyświetlam aktywną sesję tej przeglądarki. Nie udało się potwierdzić tej próby logowania.',
            );
            this.focusHeading();
            return;
          }
          if (error instanceof HttpErrorResponse && error.status === 409) {
            await firstValueFrom(this.http.post(`${endpoint}/session/reset`, null, options));
            this.error.set('Sesja uległa zmianie. Zaloguj się ponownie.');
            return;
          }
        } catch {
          // A second network failure leaves the result unknown.
        }
        this.error.set('Nie udało się potwierdzić logowania. Spróbuj ponownie.');
      }
    } finally {
      this.busy.set(false);
    }
  }

  protected async logout(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.notice.set('');
    try {
      await firstValueFrom(this.http.post(`${endpoint}/logout`, null, options));
      this.signedOut();
    } catch {
      try {
        const account = await this.readSession();
        if (account) {
          this.account.set(account);
          this.focusHeading();
        } else {
          this.signedOut();
        }
      } catch {
        this.error.set('Nie udało się potwierdzić wylogowania.');
      }
      if (this.account() && !this.error())
        this.error.set('Sesja nadal trwa. Spróbuj wylogować się ponownie.');
    } finally {
      this.busy.set(false);
    }
  }

  private signedOut(): void {
    clearAccountLearningStorage();
    this.account.set(null);
    this.focusHeading();
  }

  private async restore(): Promise<void> {
    try {
      const account = await this.readSession();
      if (!account) clearAccountLearningStorage();
      this.account.set(account);
    } catch {
      this.error.set('Nie udało się sprawdzić bieżącej sesji.');
    } finally {
      this.loading.set(false);
      this.focusHeading();
    }
  }

  private async readSession(): Promise<Account | null> {
    try {
      return await firstValueFrom(this.http.get<Account>(`${endpoint}/session`, options));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) return null;
      throw error;
    }
  }

  private focusHeading(): void {
    if (!this.destroyRef.destroyed) {
      afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
    }
  }
}
