import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  afterNextRender,
  Component,
  DestroyRef,
  computed,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { accountLearningStoragePrefix } from './account-learning-storage';

interface Question {
  number: number;
  text: string;
  hint: string | null;
  direction: 'EnglishToPolish' | 'PolishToEnglish';
  mode: 'Choices' | 'Typed';
  choices: string[];
  baseFormRetry: boolean;
}

interface Summary {
  firstTryCorrect: number;
  wordCount: number;
  wordsResolved: number;
  completed: boolean;
}

interface Session {
  id: string;
  question: Question | null;
  summary: Summary;
}

interface Feedback {
  assessment: {
    grade: 'Correct' | 'CorrectWithSpelling' | 'RetryWithBaseForm' | 'Incorrect';
    suggestedSpelling: string | null;
  };
  correctTranslations: string[];
}

interface AnswerResponse {
  feedback: Feedback;
  session: Session;
}

interface FeedbackCheckpoint {
  before: Session;
  after: Session;
  feedback: Feedback;
  selected: number | null;
  typed: string;
}

interface Attempt {
  key: string;
  answer: { questionNumber: number; text?: string; choice?: number };
}

const clientHeaders = { 'X-Verbulio-Client': 'local-learning' };

@Component({
  selector: 'app-local-learning',
  imports: [RouterLink],
  templateUrl: './local-learning.html',
  styleUrl: './local-learning.css',
})
export class LocalLearning {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly accountOwned = inject(ActivatedRoute).snapshot.data['accountOwned'] === true;
  private readonly api = this.accountOwned
    ? '/api/v1/local-account/learning'
    : '/api/v1/local-learning';
  private readonly storagePrefix = this.accountOwned
    ? accountLearningStoragePrefix
    : 'verbulio.local-learning';
  private readonly activeSessionKey = this.storagePrefix + '.active-session';
  private readonly feedbackCheckpointKey = this.storagePrefix + '.feedback';
  private readonly main = viewChild<ElementRef<HTMLElement>>('main');
  private readonly next = viewChild<ElementRef<HTMLButtonElement>>('next');
  private latestSession: Session | null = null;
  private pendingAttempt: Attempt | null = null;

  protected readonly session = signal<Session | null>(null);
  protected readonly feedback = signal<Feedback | null>(null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  protected readonly typed = signal('');
  protected readonly selected = signal<number | null>(null);
  protected readonly canRetry = signal(false);
  protected readonly signInRequired = signal(false);

  protected readonly question = computed(() => this.session()?.question ?? null);
  protected readonly targetLanguage = computed(() =>
    this.question()?.direction === 'EnglishToPolish' ? 'pl' : 'en',
  );
  protected readonly instruction = computed(() => {
    const verb = this.question()?.mode === 'Choices' ? 'Wybierz' : 'Wpisz';
    return `${verb} po ${this.targetLanguage() === 'pl' ? 'polsku' : 'angielsku'}`;
  });

  constructor() {
    const saved = sessionStorage.getItem(this.activeSessionKey);
    if (saved) {
      void this.run(async () => {
        try {
          const current = await firstValueFrom(
            this.http.get<Session>(`${this.api}/sessions/${saved}`),
          );
          this.restoreSession(current);
        } catch (error) {
          if (!(error instanceof HttpErrorResponse) || ![401, 404].includes(error.status)) {
            throw error;
          }
          sessionStorage.removeItem(this.activeSessionKey);
          sessionStorage.removeItem(this.feedbackCheckpointKey);
          if (error.status === 401 && this.accountOwned) this.requireSignIn();
        }
      });
    }
  }

  protected start(): void {
    void this.run(async () => {
      if (!this.accountOwned) {
        await firstValueFrom(
          this.http.post(`${this.api}/identity`, null, { headers: clientHeaders }),
        );
      }
      const session = await firstValueFrom(
        this.http.post<Session>(`${this.api}/sessions`, null, { headers: clientHeaders }),
      );
      sessionStorage.setItem(this.activeSessionKey, session.id);
      this.restoreSession(session);
      this.focusMain();
    });
  }

  protected choose(index: number): void {
    const question = this.question();
    if (!question || this.feedback() || this.busy() || this.pendingAttempt) return;

    this.selected.set(index);
    this.submit({
      key: crypto.randomUUID(),
      answer: { questionNumber: question.number, choice: index },
    });
  }

  protected write(event: SubmitEvent): void {
    event.preventDefault();
    const question = this.question();
    if (!question || !this.typed().trim() || this.feedback() || this.busy() || this.pendingAttempt)
      return;

    this.submit({
      key: crypto.randomUUID(),
      answer: { questionNumber: question.number, text: this.typed() },
    });
  }

  protected retry(): void {
    if (this.pendingAttempt) this.submit(this.pendingAttempt);
  }

  protected advance(): void {
    if (!this.latestSession) return;

    this.session.set(this.latestSession);
    sessionStorage.removeItem(this.feedbackCheckpointKey);
    this.latestSession = null;
    this.feedback.set(null);
    this.typed.set('');
    this.selected.set(null);
    this.focusMain();
  }

  protected async end(dialog: HTMLDialogElement): Promise<void> {
    const session = this.session();
    if (!session) return;
    dialog.close();
    await this.run(async () => {
      const updated = await firstValueFrom(
        this.http.post<Session>(`${this.api}/sessions/${session.id}/end`, null, {
          headers: clientHeaders,
        }),
      );
      this.session.set(updated);
      sessionStorage.removeItem(this.feedbackCheckpointKey);
      this.feedback.set(null);
      this.latestSession = null;
      this.pendingAttempt = null;
      this.canRetry.set(false);
      this.focusMain();
    });
  }

  protected reset(): void {
    sessionStorage.removeItem(this.activeSessionKey);
    sessionStorage.removeItem(this.feedbackCheckpointKey);
    this.session.set(null);
    this.typed.set('');
    this.selected.set(null);
    this.focusMain();
  }

  private submit(attempt: Attempt): void {
    const session = this.session();
    if (!session) return;

    this.pendingAttempt = attempt;
    void this.run(async () => {
      try {
        const response = await firstValueFrom(
          this.http.post<AnswerResponse>(
            `${this.api}/sessions/${session.id}/answers`,
            attempt.answer,
            {
              headers: { ...clientHeaders, 'Idempotency-Key': attempt.key },
            },
          ),
        );
        const checkpoint: FeedbackCheckpoint = {
          before: session,
          after: response.session,
          feedback: response.feedback,
          selected: this.selected(),
          typed: this.typed(),
        };
        sessionStorage.setItem(this.feedbackCheckpointKey, JSON.stringify(checkpoint));
        this.showFeedback(checkpoint);
        this.pendingAttempt = null;
        this.canRetry.set(false);
      } catch (error) {
        if (error instanceof HttpErrorResponse && error.status >= 400 && error.status < 500) {
          this.pendingAttempt = null;
          this.canRetry.set(false);
          this.selected.set(null);
          if (error.status === 409 && error.error?.code === 'answer_conflict') {
            const current = await firstValueFrom(
              this.http.get<Session>(`${this.api}/sessions/${session.id}`),
            );
            this.session.set(current);
            this.typed.set('');
            this.error.set('Pytanie zmieniło się w innej karcie. Sesja została odświeżona.');
            this.focusMain();
            return;
          }
        } else {
          this.canRetry.set(true);
        }
        throw error;
      }
    });
  }

  private restoreSession(current: Session): void {
    const saved = sessionStorage.getItem(this.feedbackCheckpointKey);
    if (saved) {
      try {
        const checkpoint: FeedbackCheckpoint = JSON.parse(saved);
        const matches =
          checkpoint.after.id === current.id &&
          checkpoint.after.question?.number === current.question?.number &&
          checkpoint.after.summary.completed === current.summary.completed;
        if (matches) {
          this.showFeedback(checkpoint);
          return;
        }
      } catch {
        // Drop unreadable browser state and use the persisted API state.
      }
      sessionStorage.removeItem(this.feedbackCheckpointKey);
    }
    this.session.set(current);
  }

  private showFeedback(checkpoint: FeedbackCheckpoint): void {
    this.session.set(checkpoint.before);
    this.latestSession = checkpoint.after;
    this.feedback.set(checkpoint.feedback);
    this.selected.set(checkpoint.selected);
    this.typed.set(checkpoint.typed);
    if (!this.destroyRef.destroyed) {
      afterNextRender(() => this.next()?.nativeElement.focus(), { injector: this.injector });
    }
  }

  private focusMain(): void {
    if (!this.destroyRef.destroyed) {
      afterNextRender(() => this.main()?.nativeElement.focus(), { injector: this.injector });
    }
  }

  private requireSignIn(): void {
    this.reset();
    this.latestSession = null;
    this.pendingAttempt = null;
    this.feedback.set(null);
    this.canRetry.set(false);
    this.signInRequired.set(true);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      if (this.accountOwned && error instanceof HttpErrorResponse && error.status === 401) {
        this.requireSignIn();
        return;
      }

      const apiError = error instanceof HttpErrorResponse ? error.error : null;
      this.error.set(
        typeof apiError?.title === 'string' ? apiError.title : 'Nie udało się połączyć z serwerem.',
      );
    } finally {
      this.busy.set(false);
    }
  }
}
