import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AMIRA, HEDI } from '../../test/personnes';
import { Session } from './session';

describe('Session', () => {
  let session: Session;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    session = TestBed.inject(Session);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it("ne demande qu'une fois qui est connecté", async () => {
    const premier = session.charger();
    const second = session.charger();
    http.expectOne('/api/moi').flush(AMIRA);

    expect(await premier).toEqual(AMIRA);
    expect(await second).toEqual(AMIRA);
    expect(await session.charger()).toEqual(AMIRA);
    expect(session.personne()).toEqual(AMIRA);
  });

  it("un 401 veut dire que personne n'est connecté", async () => {
    const reponse = session.charger();
    http.expectOne('/api/moi').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await reponse).toBeNull();
    expect(session.personne()).toBeNull();
    // C'est retenu : la garde suivante de la même navigation ne redemande pas.
    expect(await session.charger()).toBeNull();
  });

  it('une personne déjà connue évite la requête', async () => {
    session.connaitre(HEDI);

    expect(await session.charger()).toEqual(HEDI);
    expect(session.personne()).toEqual(HEDI);
  });

  it('après oublier, redemande au serveur', async () => {
    session.connaitre(AMIRA);
    session.oublier();
    expect(session.personne()).toBeNull();

    const reponse = session.charger();
    http.expectOne('/api/moi').flush(HEDI);

    expect(await reponse).toEqual(HEDI);
  });

  it.each([
    [204, 'No Content'],
    [401, 'Unauthorized'],
  ])('se déconnecter avec %s oublie la personne', async (status, statusText) => {
    session.connaitre(AMIRA);

    const deconnexion = session.seDeconnecter();
    const requete = http.expectOne('/api/deconnexion');
    expect(requete.request.method).toBe('POST');
    requete.flush(null, { status, statusText });

    await deconnexion;
    expect(session.personne()).toBeNull();
  });

  it('sans Internet, la déconnexion échoue et la personne reste connectée', async () => {
    session.connaitre(AMIRA);

    const deconnexion = session.seDeconnecter();
    http.expectOne('/api/deconnexion').error(new ProgressEvent('error'), { status: 0 });

    await expect(deconnexion).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(session.personne()).toEqual(AMIRA);
  });
});
