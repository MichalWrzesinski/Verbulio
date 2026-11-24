import { Location } from '@angular/common';
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

  it('removes the link secret and posts it only after confirmation', async () => {
    const token = 'a'.repeat(43);
    const harness = await RouterTestingHarness.create('/potwierdz-adres#token=' + token);
    const requests = TestBed.inject(HttpTestingController);
    const endpoint = '/api/v1/local-account/email-confirmation/confirm';
    requests.expectNone(endpoint);
    expect(TestBed.inject(Location).path(true)).toBe('/potwierdz-adres');

    harness.routeNativeElement?.querySelector('button')?.click();
    harness.routeNativeElement?.querySelector('button')?.click();
    const confirmation = requests.expectOne({ method: 'POST', url: endpoint });
    expect(confirmation.request.body).toEqual({ token });
    expect(confirmation.request.headers.get('X-Verbulio-Client')).toBe('local-learning');
    confirmation.flush(null, { status: 204, statusText: 'No Content' });
    harness.detectChanges();

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      'Adres potwierdzony',
    );
    expect(harness.routeNativeElement?.querySelector('a[href="/logowanie"]')).toBeTruthy();
  });

  it('replaces a pending confirmation when a second link opens on the same route', async () => {
    const original = 'a'.repeat(43);
    const replacement = 'b'.repeat(43);
    const harness = await RouterTestingHarness.create('/potwierdz-adres#token=' + original);
    const requests = TestBed.inject(HttpTestingController);
    const endpoint = '/api/v1/local-account/email-confirmation/confirm';
    harness.routeNativeElement?.querySelector('button')?.click();
    const active = requests.expectOne(endpoint);

    await harness.navigateByUrl('/potwierdz-adres#token=' + replacement);
    expect(active.cancelled).toBe(true);
    expect(TestBed.inject(Location).path(true)).toBe('/potwierdz-adres');
    requests.expectNone(endpoint);
    harness.routeNativeElement?.querySelector('button')?.click();
    const confirmation = requests.expectOne(endpoint);
    expect(confirmation.request.body).toEqual({ token: replacement });
    confirmation.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('reports an invalid confirmation link without a success state', async () => {
    const harness = await RouterTestingHarness.create('/potwierdz-adres#token=' + 'a'.repeat(43));
    harness.routeNativeElement?.querySelector('button')?.click();
    TestBed.inject(HttpTestingController)
      .expectOne('/api/v1/local-account/email-confirmation/confirm')
      .flush({ code: 'invalid_confirmation_token' }, { status: 400, statusText: 'Bad Request' });
    harness.detectChanges();

    expect(harness.routeNativeElement?.querySelector('[role=alert]')?.textContent).toContain(
      'nieprawidłowy lub wygasł',
    );
    expect(harness.routeNativeElement?.querySelector('button')).toBeNull();
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent?.trim()).toBe(
      'Potwierdź adres e-mail',
    );
  });

  it('keeps credential fields off the HTTP development login page', async () => {
    const harness = await RouterTestingHarness.create('/logowanie');
    const page = harness.routeNativeElement;

    expect(page?.querySelector('h1')?.textContent).toBe('Zaloguj się');
    expect(page?.textContent).toContain('przez HTTPS');
    expect(page?.querySelector('form')).toBeNull();
  });
});
