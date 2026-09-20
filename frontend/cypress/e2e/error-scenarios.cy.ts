// Scenarios d'erreur, en complement du parcours nominal (register-login-upload.cy.ts).
// Necessite le backend (dotnet run) et postgres (docker compose up -d) demarres en
// parallele du frontend (ng serve) - voir TESTING.md.

const API_URL = 'http://localhost:5074';

describe('Connexion - mauvais mot de passe', () => {
  it("affiche un message d'erreur et reste sur /login", () => {
    const email = `cypress-err-${Date.now()}@example.com`;

    cy.request('POST', `${API_URL}/api/auth/register`, { email, password: 'Password1!' });

    cy.visit('/login');
    cy.get('#email').type(email);
    cy.get('#password').type('MauvaisMotDePasse1!');
    cy.contains('button', 'Connexion').click();

    cy.contains('Identifiants incorrects.').should('be.visible');
    cy.url().should('include', '/login');
  });
});

describe('Upload - cas de rejet', () => {
  beforeEach(() => {
    cy.intercept('POST', '**/api/files').as('upload');
    // Creation + connexion directement via l'API (plus rapide/fiable que de repasser
    // par le formulaire a chaque test - deja verifie par le parcours nominal).
    const email = `cypress-err-${Date.now()}-${Math.floor(Math.random() * 10000)}@example.com`;
    const password = 'Password1!';

    cy.request('POST', `${API_URL}/api/auth/register`, { email, password });
    cy.request('POST', `${API_URL}/api/auth/login`, { email, password }).then((response) => {
      // cy.visit AVANT d'ecrire dans localStorage : localStorage est isole par origine,
      // il faut etre sur l'origine du frontend pour que l'ecriture atterrisse au bon endroit.
      cy.visit('/login');
      cy.window().then((win) => {
        win.localStorage.setItem('access_token', response.body.accessToken);
        win.localStorage.setItem('refresh_token', response.body.refreshToken);
      });
      cy.visit('/upload');
    });
  });

  it("rejette un fichier dont le type n'est pas supporte", () => {
    cy.get('input[type=file]').selectFile('cypress/fixtures/invalid.pdf', { force: true });
    cy.contains('button', 'Téléverser').click();
    cy.wait('@upload').its('response.statusCode').should('eq', 400);

    cy.contains("Ce type de fichier n'est pas autorisé.").should('be.visible');
  });

  it('rejette un mot de passe fichier trop court', () => {
    cy.get('input[type=file]').selectFile('cypress/fixtures/sample.pdf', { force: true });
    cy.get('#password').type('abc');
    cy.contains('button', 'Téléverser').click();
    cy.wait('@upload').its('response.statusCode').should('eq', 400);

    cy.contains('Le mot de passe doit contenir au moins 6 caractères.').should('be.visible');
  });
});
