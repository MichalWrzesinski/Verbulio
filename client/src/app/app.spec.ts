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
});
