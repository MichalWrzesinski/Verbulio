import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  imports: [RouterLink],
  templateUrl: './landing.html',
})
export class Landing {
  protected readonly learningAvailable =
    inject(ActivatedRoute).snapshot.data['learningAvailable'] === true;
}
