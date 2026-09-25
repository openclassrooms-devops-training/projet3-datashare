module.exports = {
  preset: 'jest-preset-angular',
  roots: ['<rootDir>/src/'],
  testMatch: ['**/+(*.)+(spec).+(ts|js)'],
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  collectCoverage: true,
  coverageReporters: ['html', 'text-summary', 'lcov'],
  // Rapport de resultats detaille (pass/fail par test), distinct du rapport de
  // couverture (coverageReporters ci-dessus) - ecrit dans un dossier separe pour
  // ne pas melanger les deux types de rapports HTML.
  reporters: [
    'default',
    ['jest-html-reporters', { publicPath: './test-results', filename: 'report.html', pageTitle: 'DataShare - Resultats des tests frontend' }],
  ],
};
