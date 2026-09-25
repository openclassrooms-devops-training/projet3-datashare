// Parcours critique bout-en-bout : creation de compte -> connexion -> upload d'un fichier.
// Necessite le backend (dotnet run) et postgres (docker compose up -d) demarres en parallele
// du frontend (ng serve) - voir TESTING.md pour les instructions d'execution.

describe('Parcours critique : inscription -> connexion -> upload', () => {
  const email = `cypress-${Date.now()}@example.com`;
  const password = 'Password1!';

  it('permet de creer un compte, se connecter, puis uploader un fichier', () => {
    cy.intercept('POST', '**/api/auth/register').as('register');
    cy.intercept('POST', '**/api/auth/login').as('login');

    // Inscription
    cy.visit('/register');
    cy.get('#email').type(email);
    cy.get('#password').type(password);
    cy.get('#passwordConfirmation').type(password);
    cy.contains('button', 'Créer mon compte').click();
    cy.wait('@register').its('response.statusCode').should('eq', 201);

    // Le register redirige vers /login en cas de succes (RegisterComponent.onSubmit)
    cy.url().should('include', '/login');

    // Connexion avec le compte qu'on vient de creer
    cy.get('#email').type(email);
    cy.get('#password').type(password);
    cy.contains('button', 'Connexion').click();
    cy.wait('@login').its('response.statusCode').should('eq', 200);

    // Le login redirige vers /upload en cas de succes (LoginComponent.onSubmit)
    cy.url().should('include', '/upload');
    cy.contains('Tu veux partager un fichier').should('be.visible');

    // Upload d'un fichier
    cy.get('input[type=file]').selectFile('cypress/fixtures/sample.pdf', { force: true });
    cy.contains('Ajouter un fichier').should('be.visible');
    cy.contains('sample.pdf').should('be.visible');

    cy.contains('button', 'Téléverser').should('not.be.disabled').click();

    // Ecran de succes : lien de telechargement affiche
    cy.contains('Copier le lien').should('be.visible');
    cy.get('.field-input').invoke('text').should('include', '/download/');
  });
});
