import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** La racine n'affiche que les pages : le cadre de l'application est lui-même une page (voir app.routes.ts). */
@Component({
  selector: 'sh-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {}
