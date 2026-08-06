import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-section-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './section-card.html',
})
export class SectionCard {
  @Input() title?: string;
  @Input() noPadding = false;
}
