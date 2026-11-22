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

interface CompletedPlacement {
  id: string;
  test: { result: string | null };
}

interface RegisteredAccount {
  id: string;
  startingLevel: string;
}

const options = { headers: { 'X-Verbulio-Client': 'local-learning' }, timeout: 10_000 };

@Component({
  selector: 'app-local-registration',
  imports: [RouterLink],
  templateUrl: './local-registration.html',
  styles: ':host { display: contents; }',
})
export class LocalRegistration {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly placement = signal<CompletedPlacement | null>(null);
  protected readonly account = signal<RegisteredAccount | null>(null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly cannotRegister = signal(false);
  protected readonly error = signal('');
  protected readonly passwordError = signal('');
  protected readonly invalidFields = signal(false);
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly passwordVisible = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    void this.loadPlacement();
  }

  protected submit(event: SubmitEvent): void {
    event.preventDefault();
    const placement = this.placement();
    if (!placement?.test.result || this.saving() || this.account()) return;
    if (!(event.currentTarget instanceof HTMLFormElement)) return;

    const form = new FormData(event.currentTarget);
    this.email.set(String(form.get('email') ?? ''));
    this.password.set(String(form.get('password') ?? ''));
    this.error.set('');
    this.passwordError.set('');
    this.invalidFields.set(false);
    const size = [...this.password()].length;
    if (size < 12 || size > 128) {
      this.passwordError.set('Hasło musi mieć od 12 do 128 znaków.');
      return;
    }

    this.saving.set(true);
    void this.save(placement.id);
  }

  private async loadPlacement(): Promise<void> {
    try {
      const current = await firstValueFrom(
        this.http.post<CompletedPlacement>('/api/v1/local-placement/tests', null, options),
      );
      const account = current.test.result ? await this.readOutcome(current.id) : null;
      this.placement.set(current);
      this.cannotRegister.set(!current.test.result);
      if (account) this.showAccount(account);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.cannotRegister.set(true);
      } else {
        this.error.set('Nie udało się odczytać wyniku testu. Spróbuj ponownie.');
      }
    } finally {
      this.loading.set(false);
      this.focusHeading();
    }
  }

  private async save(placementTestId: string): Promise<void> {
    try {
      const account = await firstValueFrom(
        this.http.post<RegisteredAccount>(
          '/api/v1/local-registration/accounts',
          { placementTestId, email: this.email().trim(), password: this.password() },
          options,
        ),
      );
      this.showAccount(account);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.password.set('');
        this.passwordVisible.set(false);
        this.cannotRegister.set(true);
        this.focusHeading();
        return;
      }

      if (error instanceof HttpErrorResponse && error.status === 400) {
        this.invalidFields.set(true);
        this.error.set('Sprawdź adres e-mail i długość hasła.');
        return;
      }

      try {
        const account = await this.readOutcome(placementTestId);
        if (account) {
          this.showAccount(account);
          return;
        }
      } catch {
        // The account could have been committed even if the outcome request also failed.
      }

      this.error.set(
        error instanceof HttpErrorResponse && error.status === 409
          ? 'Nie udało się utworzyć konta z tymi danymi.'
          : 'Nie udało się potwierdzić zapisu konta. Możesz ponowić próbę.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private async readOutcome(placementTestId: string): Promise<RegisteredAccount | null> {
    try {
      return await firstValueFrom(
        this.http.get<RegisteredAccount>(
          `/api/v1/local-registration/outcomes/${placementTestId}`,
          options,
        ),
      );
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) return null;
      throw error;
    }
  }

  private showAccount(account: RegisteredAccount): void {
    this.password.set('');
    this.passwordVisible.set(false);
    this.account.set(account);
    this.focusHeading();
  }

  private focusHeading(): void {
    if (!this.destroyRef.destroyed) {
      afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
    }
  }
}
