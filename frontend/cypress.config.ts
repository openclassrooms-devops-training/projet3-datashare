import { defineConfig } from 'cypress';
import addMochawesomeReporterPlugin from 'cypress-mochawesome-reporter/plugin';

export default defineConfig({
  reporter: 'cypress-mochawesome-reporter',
  reporterOptions: {
    reportDir: 'cypress/test-results',
    overwrite: true,
    html: true,
    json: true,
    charts: true,
  },
  e2e: {
    baseUrl: 'http://localhost:4200',
    env: {
      apiUrl: 'http://localhost:5074',
    },
    supportFile: 'cypress/support/e2e.ts',
    specPattern: 'cypress/e2e/**/*.cy.ts',
    setupNodeEvents(on) {
      addMochawesomeReporterPlugin(on);
    },
  },
  video: false,
});
