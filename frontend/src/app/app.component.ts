import { Component, inject } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { HeaderComponent } from './shared/header/header.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, HeaderComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'datashare-frontend';

  private router = inject(Router);
  private route = inject(ActivatedRoute);

  // Certaines pages (ex. Mon espace) ont leur propre bandeau "DataShare" integre
  // a leur mise en page (sidebar) et ne doivent pas afficher aussi le header
  // global - sinon on se retrouve avec deux logos DataShare empiles. La route
  // active porte data: { hideHeader: true } pour signaler ce cas (voir app.routes.ts).
  protected hideHeader = false;

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      let child = this.route.firstChild;
      while (child?.firstChild) {
        child = child.firstChild;
      }
      this.hideHeader = !!child?.snapshot.data['hideHeader'];
    });
  }
}
