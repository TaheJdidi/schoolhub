import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Le cadre de l'application, autour des pages d'une personne connectée. */
@Component({
  selector: 'sh-cadre',
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class Cadre {}
