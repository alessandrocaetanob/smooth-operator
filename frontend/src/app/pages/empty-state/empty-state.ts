import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-empty-state',
  imports: [Mascot, TranslatePipe],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.css',
})
export class EmptyState {}
