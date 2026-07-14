import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Mascot } from '../../shared/mascot/mascot';

@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink, Mascot, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './not-found.html',
})
export class NotFound {}
