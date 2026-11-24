import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { vi } from 'vitest';
import { routes } from './app.routes';

describe('Public navigation', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('opens the local placement test from the reference landing', async () => {
    const harness = await RouterTestingHarness.create('/');
    const page = harness.routeNativeElement;

    expect(page?.querySelector('h1')?.textContent).toBe('Zacznij naukę angielskiego');
    expect(page?.querySelector('a[href="/test"]')?.textContent).toBe('Sprawdź swój poziom');

    await harness.navigateByUrl('/test');
    const requests = TestBed.inject(HttpTestingController);
    requests.expectOne({ method: 'POST', url: '/api/v1/local-learning/identity' }).flush(null);
    await Promise.resolve();
    requests.expectOne({ method: 'POST', url: '/api/v1/local-placement/tests' }).flush({
      id: 'guest-placement',
      test: {
        answeredCount: 0,
        totalCount: 20,
        result: null,
        question: {
          number: 1,
          text: 'window',
          direction: 'EnglishToPolish',
          mode: 'Choices',
          choices: ['okno', 'drzwi', 'ściana', 'dach'],
        },
      },
    });
    await vi.waitFor(() => {
      harness.detectChanges();
      expect(harness.routeNativeElement?.querySelector('#q-word')?.textContent?.trim()).toBe(
        'window',
      );
    });

    const test = harness.routeNativeElement;
    expect(test?.querySelector('h1')?.textContent).toBe('Test poziomu');
    expect(test?.querySelectorAll('button.tag')).toHaveLength(4);
    expect(test?.querySelector('.result')).toBeNull();
  });

  it('links from account settings to data-copy information', async () => {
    const harness = await RouterTestingHarness.create('/ustawienia');
    const requests = TestBed.inject(HttpTestingController);
    requests.expectOne('/api/v1/local-account/settings/account').flush({
      email: 'fixture@example.invalid',
      deletionAvailable: false,
    });
    requests.expectOne('/api/v1/local-account/settings/learning').flush({ autoAdvance: true });
    harness.detectChanges();

    const link = harness.routeNativeElement?.querySelector<HTMLAnchorElement>(
      'a[href="/prywatnosc/eksport"]',
    );
    expect(link?.textContent?.trim()).toBe('Jak dostać kopię swoich danych');

    const page = await harness.navigateByUrl('/prywatnosc/eksport');
    expect(page).toBeTruthy();
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(
      'Kopia Twoich danych',
    );
    expect(harness.routeNativeElement?.textContent).toContain('wniosków o kopię danych');
  });

  it('hides the data-copy link when sign-in is required', async () => {
    const harness = await RouterTestingHarness.create('/ustawienia');
    const requests = TestBed.inject(HttpTestingController);
    for (const resource of ['account', 'learning']) {
      requests
        .expectOne('/api/v1/local-account/settings/' + resource)
        .flush(null, { status: 401, statusText: 'Unauthorized' });
    }
    harness.detectChanges();

    expect(harness.routeNativeElement?.querySelector('a[href="/logowanie"]')).toBeTruthy();
    expect(harness.routeNativeElement?.querySelector('a[href="/prywatnosc/eksport"]')).toBeNull();
  });

  it('keeps credential fields off the HTTP development login page', async () => {
    const harness = await RouterTestingHarness.create('/logowanie');
    const page = harness.routeNativeElement;

    expect(page?.querySelector('h1')?.textContent).toBe('Zaloguj się');
    expect(page?.textContent).toContain('przez HTTPS');
    expect(page?.querySelector('form')).toBeNull();
  });
});
