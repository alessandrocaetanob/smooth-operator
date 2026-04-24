import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-top-nav-bar',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './top-nav-bar.html',
  styleUrl: './top-nav-bar.css',
})
export class TopNavBar {}
