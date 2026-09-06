import { defineConfig } from 'cypress';

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    env: {
      apiUrl: 'http://localhost:5000',
    },
    supportFile: false,
    specPattern: 'cypress/e2e/**/*.cy.ts',
  },
  video: false,
});
