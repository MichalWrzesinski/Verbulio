import { afterNextRender, DestroyRef, inject, Injector } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

export function watchAccountLink(onLink: (token: string | null) => void): void {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const injector = inject(Injector);

  route.fragment.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((fragment) => {
    if (fragment === null) return;

    onLink(fragment.match(/^token=([A-Za-z0-9_-]{43})$/)?.[1] ?? null);
    afterNextRender(
      () => {
        if (route.snapshot.fragment !== fragment) return;

        void router.navigate([], {
          relativeTo: route,
          queryParamsHandling: 'preserve',
          replaceUrl: true,
        });
      },
      { injector },
    );
  });
}
