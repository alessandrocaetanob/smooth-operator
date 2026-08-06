import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { Mascot, MascotState } from '../mascot/mascot';

@Component({
  selector: 'app-empty-state-card',
  standalone: true,
  imports: [Mascot],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './empty-state-card.html',
})
export class EmptyStateCard {
  @Input() mascotState: MascotState = 'idle';
  @Input() heading = '';
  @Input() body?: string;
}
