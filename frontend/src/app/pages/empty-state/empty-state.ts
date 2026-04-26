import { Component } from '@angular/core';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-empty-state',
  imports: [Mascot],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.css',
})
export class EmptyState {}
