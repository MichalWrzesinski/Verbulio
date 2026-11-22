import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('Public navigation', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });
  });

  it('shows the reference landing and links to the placement test', async () => {
    const harness = await RouterTestingHarness.create('/');
    const page = harness.routeNativeElement;

    expect(page?.querySelector('h1')?.textContent).toBe('Zacznij naukę angielskiego');
    expect(page?.querySelector('a[href="/test"]')?.textContent).toBe('Sprawdź swój poziom');

    await harness.navigateByUrl('/test');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Test słownictwa');
  });
});
