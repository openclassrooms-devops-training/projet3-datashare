// Support file minimal, ajoute uniquement pour cypress-mochawesome-reporter (le
// rapport HTML des resultats e2e a besoin de s'enregistrer cote navigateur). Le
// projet n'utilisait pas de support file avant (supportFile: false) - pas de
// custom commands ni d'autre setup global a ce jour.
import 'cypress-mochawesome-reporter/register';
