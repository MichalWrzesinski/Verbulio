import { afterNextRender, Component, ElementRef, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-local-account-export',
  imports: [RouterLink],
  styles: ':host { display: contents; }',
  template: `
    <div class="app">
      <header class="form-top">
        <a class="link back" routerLink="/ustawienia">
          <svg class="i" viewBox="0 0 20 20" aria-hidden="true" focusable="false">
            <path
              d="M12.5 4.5L7 10l5.5 5.5"
              fill="none"
              stroke="currentColor"
              stroke-width="2.8"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
          Ustawienia
        </a>
      </header>
      <main class="doc doc--text">
        <h1 #heading class="h1" tabindex="-1">Kopia Twoich danych</h1>
        <p>W tej wersji testowej obsługa wniosków o kopię danych jest w przygotowaniu.</p>
        <p>
          Kopia w formacie JSON obejmuje adres <span class="nw">e-mail</span>, ustawienia konta,
          odpowiedzi z testu poziomu i historię nauki.
        </p>
      </main>
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
export class LocalAccountExport {
  private readonly heading = viewChild.required<ElementRef<HTMLElement>>('heading');

  constructor() {
    afterNextRender(() => this.heading().nativeElement.focus());
  }
}
