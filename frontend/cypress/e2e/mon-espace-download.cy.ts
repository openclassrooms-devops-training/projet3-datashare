// Parcours "Mon espace" et "Telechargement public" (US02/US05/US06), en complement de
// register-login-upload.cy.ts (US01) et error-scenarios.cy.ts.
// Necessite le backend (dotnet run) et postgres (docker compose up -d) demarres en
// parallele du frontend (ng serve) - voir TESTING.md.

const API_URL = 'http://localhost:5074';

function registerLoginAndVisitUpload(email: string, password: string) {
  cy.request('POST', `${API_URL}/api/auth/register`, { email, password });
  return cy.request('POST', `${API_URL}/api/auth/login`, { email, password }).then((response) => {
    // cy.visit AVANT d'ecrire dans localStorage : localStorage est isole par origine,
    // il faut etre sur l'origine du frontend pour que l'ecriture atterrisse au bon endroit.
    cy.visit('/login');
    cy.window().then((win) => {
      win.localStorage.setItem('access_token', response.body.accessToken);
      win.localStorage.setItem('refresh_token', response.body.refreshToken);
    });
    cy.visit('/upload');
  });
}

describe('Mon espace : liste et suppression', () => {
  const password = 'Password1!';

  beforeEach(() => {
    // Email regenere a chaque test (pas une seule fois au niveau du describe) :
    // chaque beforeEach ré-appelle /api/auth/register, un email reutilise entre
    // les deux "it" de ce describe echouerait avec EMAIL_ALREADY_USED.
    const email = `cypress-espace-${Date.now()}-${Math.floor(Math.random() * 10000)}@example.com`;
    cy.intercept('POST', '**/api/files').as('upload');
    registerLoginAndVisitUpload(email, password);
    cy.get('input[type=file]').selectFile('cypress/fixtures/sample.pdf', { force: true });
    cy.contains('button', 'Téléverser').click();
    cy.wait('@upload');
    cy.contains('Copier le lien').should('be.visible');
  });

  it('affiche le fichier uploadé dans la liste', () => {
    cy.intercept('GET', '**/api/files*').as('listFiles');
    cy.contains('a', 'Mon espace').click();
    cy.wait('@listFiles');

    cy.url().should('include', '/mon-espace');
    cy.contains('sample.pdf').should('be.visible');
  });

  it('permet de supprimer un fichier', () => {
    cy.intercept('GET', '**/api/files*').as('listFiles');
    cy.intercept('DELETE', '**/api/files/*').as('deleteFile');
    cy.contains('a', 'Mon espace').click();
    cy.wait('@listFiles');

    // Cypress accepte automatiquement window.confirm() par defaut (pas de stub necessaire).
    cy.contains('button', 'Supprimer').click();
    cy.wait('@deleteFile').its('response.statusCode').should('eq', 204);

    cy.contains('sample.pdf').should('not.exist');
  });
});

describe('Téléchargement public', () => {
  const password = 'Password1!';
  let downloadPath: string;

  beforeEach(() => {
    // Meme raison qu'au-dessus : un email frais par test, pas partage entre les 3 "it".
    const email = `cypress-dl-${Date.now()}-${Math.floor(Math.random() * 10000)}@example.com`;
    cy.intercept('POST', '**/api/files').as('upload');
    registerLoginAndVisitUpload(email, password);
    cy.get('input[type=file]').selectFile('cypress/fixtures/sample.pdf', { force: true });
    cy.get('#password').type('secret123');
    cy.contains('button', 'Téléverser').click();
    cy.wait('@upload');
    // Attendre le vrai rendu de l'ecran de succes (pas juste la reponse reseau) :
    // sans ca, .field-input matche encore les 3 champs du formulaire (mot de passe,
    // expiration, tags) au lieu du lien de telechargement seul - meme piege que
    // documente dans TESTING.md sur cy.wait() vs affichage reel.
    cy.contains('Copier le lien').should('be.visible');

    cy.get('.field-input').invoke('text').then((text) => {
      downloadPath = new URL(text.trim()).pathname;
    });
  });

  it('demande le mot de passe et refuse un mauvais mot de passe', () => {
    cy.intercept('POST', '**/api/files/download/*').as('download');
    cy.visit(downloadPath);

    cy.contains('sample.pdf').should('be.visible');
    cy.contains('Mot de passe').should('be.visible');
    cy.get('#password').type('mauvais-mot-de-passe');
    cy.contains('button', 'Télécharger').click();

    cy.wait('@download').its('response.statusCode').should('eq', 401);
    cy.contains('Mot de passe incorrect.').should('be.visible');
  });

  it('télécharge le fichier avec le bon mot de passe', () => {
    cy.intercept('POST', '**/api/files/download/*').as('download');
    cy.visit(downloadPath);

    cy.get('#password').type('secret123');
    cy.contains('button', 'Télécharger').click();

    cy.wait('@download').its('response.statusCode').should('eq', 200);
  });

  it('affiche un message pour un lien invalide', () => {
    cy.visit('/download/token-qui-n-existe-pas');

    cy.contains('Fichier introuvable').should('be.visible');
  });
});
