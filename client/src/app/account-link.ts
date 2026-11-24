import { afterNextRender, DestroyRef, inject, Injector } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';

export function watchAccountLink(onLink: (token: string | null) => void): void {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const injector = inject(Injector);
  const destroyRef = inject(DestroyRef);

  route.fragment.pipe(takeUntilDestroyed(destroyRef)).subscribe((fragment) => {
    if (fragment === null) return;

    onLink(fragment.match(/^token=([A-Za-z0-9_-]{43})$/)?.[1] ?? null);
    afterNextRender(
      () => {
        if (route.snapshot.fragment !== fragment) return;

        const cleanUrl = router.createUrlTree([], {
          relativeTo: route,
          queryParamsHandling: 'preserve',
        });
        void router.navigateByUrl(cleanUrl, { replaceUrl: true }).then((cleaned) => {
          if (
            !cleaned ||
            destroyRef.destroyed ||
            router.currentNavigation() ||
            router.url !== router.serializeUrl(cleanUrl)
          )
            return;

          // Angular keeps one previous navigation. Replace it with the already scrubbed URL.
          return router.navigateByUrl(cleanUrl, {
            replaceUrl: true,
            onSameUrlNavigation: 'reload',
          });
        });
      },
      { injector },
    );
  });
}
