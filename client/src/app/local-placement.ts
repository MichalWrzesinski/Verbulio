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

interface Question {
  number: number;
  text: string;
  direction: 'EnglishToPolish' | 'PolishToEnglish';
  mode: 'Choices' | 'Typed';
  choices: string[];
}

interface Placement {
  id: string;
  test: {
    question: Question | null;
    answeredCount: number;
    totalCount: number;
    result: 'A1' | 'A2' | 'B1' | 'B2' | 'C1' | 'C2' | null;
  };
}

type Answer = { skip: true } | { text: string } | { choice: number };
interface Submission {
  testId: string;
  key: string;
  body: Answer & { questionNumber: number };
}

const endpoint = '/api/v1/local-placement/tests';
const headers = { 'X-Verbulio-Client': 'local-learning' };
const options = { headers, timeout: 10_000 };
const openedTestKey = 'verbulio.local-placement.opened-test';

@Component({
  selector: 'app-local-placement',
  imports: [RouterLink],
  templateUrl: './local-placement.html',
  styleUrl: './local-placement.css',
})
export class LocalPlacement {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');
  private pending: Submission | null = null;

  protected readonly placement = signal<Placement | null>(null);
  protected readonly resumePrompt = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal('');
  protected readonly retryAvailable = signal(false);
  protected readonly typed = signal('');

  constructor() {
    this.load();
  }

  protected load(): void {
    void this.run(async () => {
      await firstValueFrom(this.http.post('/api/v1/local-learning/identity', null, options));
      const current = await firstValueFrom(this.http.post<Placement>(endpoint, null, options));
      this.show(current, true);
    });
  }

  protected resume(): void {
    this.resumePrompt.set(false);
    this.focusHeading();
  }

  protected restart(): void {
    const current = this.placement();
    if (!current) return;

    void this.run(async () => {
      try {
        this.show(
          await firstValueFrom(
            this.http.post<Placement>(`${endpoint}/${current.id}/restart`, null, options),
          ),
        );
      } catch (error) {
        if (!(error instanceof HttpErrorResponse) || error.status !== 404) throw error;

        this.show(await firstValueFrom(this.http.post<Placement>(endpoint, null, options)), true);
        this.message.set('Test został już rozpoczęty od nowa w innej karcie.');
      }
    });
  }

  protected answer(value: Answer): void {
    const current = this.placement();
    const questionNumber = current?.test.question?.number;
    if (!current || !questionNumber || this.busy() || this.pending) return;

    this.send({ testId: current.id, key: crypto.randomUUID(), body: { ...value, questionNumber } });
  }

  protected submitTyped(event: SubmitEvent): void {
    event.preventDefault();
    if (this.typed().trim()) this.answer({ text: this.typed() });
  }

  protected retry(): void {
    if (this.pending) this.send(this.pending);
  }

  private send(submission: Submission): void {
    if (this.busy()) return;

    this.pending = submission;
    void this.run(async () => {
      try {
        const response = await firstValueFrom(
          this.http.post<Placement>(`${endpoint}/${submission.testId}/answers`, submission.body, {
            ...options,
            headers: { ...headers, 'Idempotency-Key': submission.key },
          }),
        );
        this.show(response);
      } catch (error) {
        this.retryAvailable.set(true);
        if (error instanceof HttpErrorResponse) {
          if (error.status === 401) {
            await firstValueFrom(this.http.post('/api/v1/local-learning/identity', null, options));
            this.show(await firstValueFrom(this.http.post<Placement>(endpoint, null, options)));
            this.message.set('Poprzednia sesja wygasła. Przygotowano nowy test.');
            return;
          }
          if (
            error.status === 404 ||
            (error.status === 409 && error.error?.code === 'answer_conflict')
          ) {
            this.show(await firstValueFrom(this.http.post<Placement>(endpoint, null, options)));
            this.message.set('Wyświetlono bieżące pytanie po zmianie w innej karcie.');
            return;
          }
          if (error.status >= 400 && error.status < 500) {
            this.pending = null;
            this.retryAvailable.set(false);
          }
        }
        throw error;
      }
    });
  }

  private show(placement: Placement, offerResume = false): void {
    this.pending = null;
    this.retryAvailable.set(false);
    this.typed.set('');
    const openedBefore = sessionStorage.getItem(openedTestKey) === placement.id;
    sessionStorage.setItem(openedTestKey, placement.id);
    this.placement.set(placement);
    this.resumePrompt.set(
      offerResume &&
        !!placement.test.question &&
        (openedBefore || placement.test.answeredCount > 0),
    );
    this.focusHeading();
  }

  private focusHeading(): void {
    if (!this.destroyRef.destroyed) {
      afterNextRender(() => this.heading()?.nativeElement.focus(), { injector: this.injector });
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.message.set('');
    try {
      await action();
    } catch (error) {
      const problem = error instanceof HttpErrorResponse ? error.error : null;
      this.message.set(
        typeof problem?.title === 'string' ? problem.title : 'Nie udało się połączyć z serwerem.',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
