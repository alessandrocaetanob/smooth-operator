import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-section-card',
  standalone: true,
  templateUrl: './section-card.html',
})
export class SectionCard {
  @Input() title?: string;
  @Input() noPadding = false;
}
