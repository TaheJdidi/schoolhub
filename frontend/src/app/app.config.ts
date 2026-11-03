import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { FRANCAIS } from './socle/francais';
import { intercepteurDeSession } from './socle/session/intercepteur';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    { provide: LOCALE_ID, useValue: FRANCAIS },
    // Les paramètres de l'adresse (?retour=…) et les données des adresses arrivent dans les entrées des pages.
    provideRouter(routes, withComponentInputBinding()),
    // Une session tombée pendant le travail ramène à la connexion.
    provideHttpClient(withInterceptors([intercepteurDeSession])),
  ],
};
