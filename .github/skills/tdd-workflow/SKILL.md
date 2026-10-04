---
name: tdd-workflow
description: "Use for any executable code change in InfraFlowSculptor. Mandatory Red → Green → Refactor → Verify cycle."
---

# TDD obligatoire

1. **Rouge** : un test précis (un comportement, nom `Methode_Scenario_Resultat` ou phrase claire), qui échoue pour la
   bonne raison (pas une erreur de compilation). Quand l'étape cite une règle (`RG-NOM-05`), le nom du test la cite.
2. **Vert** : le minimum de code pour passer.
3. **Refactoriser** : sans changer le comportement, tests verts.
4. **Vérifier** : suite complète de la surface touchée, build sans avertissement.

Exception temporaire : une ligne dans `.github/test-debt.md` (étape, code non couvert, raison, étape qui solde) ; Claude
l'accepte ou la refuse en revue. Pas de test « théâtral » qui ne vérifie aucun invariant.
