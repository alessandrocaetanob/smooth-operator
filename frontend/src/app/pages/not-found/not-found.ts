import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, Mascot],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './not-found.html',
})
export class NotFound {}
