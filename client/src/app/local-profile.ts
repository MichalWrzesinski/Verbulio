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
import { RouterLink } from '@angular/router';

interface ProfileProgress {
  startingLevel: string;
  newWords: number;
  learningWords: number;
  masteredWords: number;
  streak: { currentDays: number; bestDays: number };
}

@Component({
  selector: 'app-local-profile',
  imports: [RouterLink],
  templateUrl: './local-profile.html',
  styles: `
    :host {
      display: contents;
    }

    .streak-value {
      flex-wrap: wrap;
      white-space: normal;
    }
  `,
})
export class LocalProfile {
  private readonly http = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  protected readonly profile = signal<ProfileProgress | null>(null);
  protected readonly state = signal<'loading' | 'ready' | 'sign-in' | 'error'>('loading');

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
    this.load();
  }

  protected load(): void {
    this.state.set('loading');
    this.http
      .get<ProfileProgress>('/api/v1/local-account/profile', {
        headers: {
          'X-Verbulio-Time-Zone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC',
        },
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (profile) => {
          this.profile.set(profile);
          this.state.set('ready');
        },
        error: (error: unknown) => {
          this.state.set(
            error instanceof HttpErrorResponse && error.status === 401 ? 'sign-in' : 'error',
          );
        },
      });
  }
}
