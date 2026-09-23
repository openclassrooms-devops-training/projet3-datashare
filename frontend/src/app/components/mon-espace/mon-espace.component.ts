import { Component, OnInit, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FileResponse, FileStatusFilter } from '../../core/models/file.models';
import { FileService } from '../../core/services/file.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-mon-espace',
  imports: [RouterLink],
  templateUrl: './mon-espace.component.html',
  styleUrl: './mon-espace.component.css'
})
export class MonEspaceComponent implements OnInit {
  private fileService = inject(FileService);
  private authService = inject(AuthService);
  private router = inject(Router);

  protected files: FileResponse[] = [];
  protected activeTab: FileStatusFilter = 'all';
  protected errorMessage: string | null = null;


  ngOnInit(): void {
    this.loadFiles();
  }

  protected setTab(tab: FileStatusFilter): void {
    // On met a jour l'etat local activeTab (utilise par le template pour surligner
    // l'onglet actif via [class.tab-active]="activeTab === 'all'" etc.), puis on
    // relance loadFiles() : c'est elle qui lit this.activeTab pour construire la
    // requete HTTP (fileService.list(this.activeTab)) - changer activeTab seul ne
    // suffit pas, il faut redemander la liste au backend avec ce nouveau filtre.
    this.activeTab = tab;
    this.loadFiles();
  }

  protected loadFiles(): void {
    this.fileService.list(this.activeTab).subscribe({
      next: (files) => {
        this.files = files;
        this.errorMessage = null;
      },
      error: () => {
        this.errorMessage = 'Erreur lors du chargement des fichiers.';
      }
    });
  }

  protected onDelete(file: FileResponse): void {
    // window.confirm(...) est bloquant : le code s'arrete ici tant que l'utilisateur
    // n'a pas repondu. S'il clique "Annuler", confirm() renvoie false et on sort
    // immediatement sans rien appeler au backend.
    const confirmed = window.confirm(`Supprimer definitivement "${file.filename}" ?`);
    if (!confirmed) {
      return;
    }

    this.fileService.delete(file.id).subscribe({
      next: () => {
        // Le serveur a confirme la suppression (204 No Content) : on met a jour
        // la liste localement en filtrant le fichier supprime, plutot que de
        // refaire un appel loadFiles() complet - evite un aller-retour HTTP inutile
        // pour juste enlever une ligne qu'on sait deja supprimee.
        this.files = this.files.filter(f => f.id !== file.id);
        this.errorMessage = null;
      },
      error: () => {
        // Ex. 403 si ce fichier n'appartient pas a l'utilisateur connecte (ne devrait
        // pas arriver via l'UI normale, mais le backend le verifie quand meme cote
        // serveur - voir SECURITY.md), ou 404 s'il a deja ete supprime entre-temps.
        this.errorMessage = 'Erreur lors de la suppression du fichier.';
      }
    });
  }

  // A quoi ca sert : le backend renvoie une date brute (file.expiresAt), pas un
  // texte pret a afficher. Cette methode est une "fonction de presentation" pure
  // (elle ne modifie aucun etat, ne fait aucun appel HTTP - elle prend un fichier
  // et renvoie juste une chaine) appelee directement depuis le template HTML :
  // {{ formatExpiry(file) }} dans mon-espace.component.html, sous le nom de
  // chaque fichier. C'est elle qui transforme la date technique en phrase lisible
  // ("Expire dans 2 jours" / "Expire demain" / "Expiré"), comme sur la maquette.
  protected formatExpiry(file: FileResponse): string {
    // file.expiresAt est une chaine ISO (ex. "2026-09-23T14:32:00Z") envoyee par le
    // backend ; new Date(...) la parse en objet Date exploitable en JS.
    const now = new Date();
    const expiryDate = new Date(file.expiresAt);
    // .getTime() renvoie des millisecondes depuis 1970 pour les deux dates : la
    // soustraction donne donc le temps restant en ms (negatif si deja expire).
    const diffTime = expiryDate.getTime() - now.getTime();
    // Math.ceil arrondit TOUJOURS vers le haut : 0.2 jour restant (quelques heures)
    // donne 1, pas 0 - c'est voulu, ca evite d'afficher "Expire dans 0 jours" pour
    // un fichier qui a encore quelques heures devant lui. Consequence : un temps
    // restant strictement positif ne peut jamais donner diffDays <= 0 - le seul
    // moyen de tomber dans le "else" (Expiré) plus bas, c'est diffTime <= 0, donc un
    // fichier reellement expire. Aucun cas ou un fichier encore valide afficherait
    // "Expiré" par erreur.
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays > 1) {
      return `Expire dans ${diffDays} jours`;
    } else if (diffDays === 1) {
      return 'Expire demain';
    } else {
      return 'Expiré';
    }
  }

  // Choisit quelle icone afficher devant le nom du fichier, d'apres son type MIME
  // (contentType, detecte par le backend - voir FileTypeValidationService). Le
  // template appelle cette methode pour savoir quelle branche du @switch afficher.
  protected fileIconType(file: FileResponse): 'image' | 'audio' | 'video' | 'other' {
    const type = file.contentType.toLowerCase();
    if (type.startsWith('image/')) return 'image';
    if (type.startsWith('audio/')) return 'audio';
    if (type.startsWith('video/')) return 'video';
    return 'other';
  }

  protected logout(): void {
    this.authService.logout().subscribe({
      complete: () => this.afterLogout(),
      error: () => this.afterLogout()
    });
  }

  private afterLogout(): void {
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
    this.router.navigate(['/login']);
  }
}
