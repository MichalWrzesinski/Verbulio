import { HttpErrorResponse } from '@angular/common/http';
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
import { RouterLink } from '@angular/router';
import { LearningPreferences } from './learning-preferences';

@Component({
  selector: 'app-local-learning-settings',
  imports: [RouterLink],
  templateUrl: './local-learning-settings.html',
  styles: ':host { display: contents; }',
})
export class LocalLearningSettings {
  private readonly preferences = inject(LearningPreferences);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  protected readonly enabled = signal<boolean | null>(null);
  protected readonly pending = signal(false);
  protected readonly signInRequired = signal(false);
  protected readonly error = signal('');
  protected readonly saved = signal(false);

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
    this.load();
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

  private failed(error: unknown, message: string): void {
    this.pending.set(false);
    if (error instanceof HttpErrorResponse && error.status === 401) {
      this.enabled.set(null);
      this.signInRequired.set(true);
      return;
    }
    this.error.set(message);
  }
}
