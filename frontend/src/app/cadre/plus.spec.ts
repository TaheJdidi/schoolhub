import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Session } from '../socle/session/session';
import { PageDeTest } from '../test/page-de-test';
import { AMIRA } from '../test/personnes';
import { Plus } from './plus';

describe('la page « Plus »', () => {
  it('liste le menu de la secrétaire, puis « Se déconnecter »', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'plus', component: Plus },
          { path: '**', component: PageDeTest },
        ]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    TestBed.inject(Session).connaitre(AMIRA);

    const harness = await RouterTestingHarness.create('/plus');
    await harness.fixture.whenStable();
    const page = harness.routeNativeElement as HTMLElement;

    expect(page.querySelectorAll('a')).toHaveLength(7);
    expect(page.querySelector('a[href="/journal"]')).toBeNull();
    expect(page.querySelector('button')?.textContent).toContain('Se déconnecter');
  });
});
