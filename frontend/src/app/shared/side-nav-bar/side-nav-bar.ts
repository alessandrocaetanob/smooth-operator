import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-side-nav-bar',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './side-nav-bar.html',
  styleUrl: './side-nav-bar.css',
})
export class SideNavBar {}
