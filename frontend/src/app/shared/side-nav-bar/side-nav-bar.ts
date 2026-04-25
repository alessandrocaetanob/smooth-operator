import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { ConnectionsService } from '../../services/connections.service';

@Component({
  selector: 'app-side-nav-bar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './side-nav-bar.html',
  styleUrl: './side-nav-bar.css',
})
export class SideNavBar {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly connectionsSvc = inject(ConnectionsService);

  newConnection(): void {
    this.router.navigate(['/connections']);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
