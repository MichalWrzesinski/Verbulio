import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  imports: [RouterLink],
  template: `
    <div class="site site--entry">
      <main class="entry">
        <h1 class="h1">{{ heading }}</h1>
        <p>Ten ekran jest w przygotowaniu.</p>
        <a class="link" routerLink="/">Wróć na stronę startową</a>
      </main>
    </div>
  `,
})
export class PendingPage {
  protected readonly heading: string = inject(ActivatedRoute).snapshot.data['heading'];
}
