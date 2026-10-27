---
name: executer-etape
description: "Use at the start of every Codex session in InfraFlowSculptor, and whenever asked to continue, implement, or 'faire l'étape suivante'. Executes the current step and its validation end to end: gate check, test first, implement, run the recipes, self-review, update memory, commit, gate transition and push."
---

# Exécuter l'étape courante du plan

La conception de référence est dans les specs, décisions, plan et maquettes. Tu exécutes **l'étape courante** de `NEXT.md`,
puis la suivante, jusqu'à un vrai manque d'information, un accès indisponible ou un risque de coût non chiffré. Les verrous
🔒 sont des points d'auto-revue, sans attente de Claude, d'un autre modèle ou d'une validation humaine.

## 1. Ouvrir

1. `git pull` sur la branche du segment (`NEXT.md`, ligne « Branche »). En début de segment (étape qui suit un verrou
   approuvé, ou S-01) : `git fetch origin main` puis `git switch -c impl/<segment> origin/main` avec le nom donné par le
   verrou précédent — sauf si ce verrou dit « même branche » (R-02) : rester sur la branche du segment ; mets à jour la ligne « Branche » de `NEXT.md`.
2. `python tools/plan/gate.py check` → code 3 : vérifie le motif et poursuis les contrôles indépendants. Un statut
   `BLOQUE` ne se lève qu'après vérification de la condition et consignation de la preuve avec `gate.py unblock <ID> <preuve>`.
   Un statut `EN_ATTENTE_DE_RECETTE` signifie que Codex doit exécuter la recette ; `EN_ATTENTE_DE_REVUE` signifie qu'il
   doit faire l'auto-revue.
3. Si l'étape courante est un verrou `R-nn`, exécute la checklist d'auto-revue au § 5. Ne demande pas de revue externe.
4. Si l'étape dépend d'une décision non confirmée de `NEXT.md`, demande ce choix à Florian ; ne le remplace pas par une
   validation de modèle externe.
5. Passe le statut à `EN_COURS` avec les commandes du `gate.py`, sans modifier à la main ses lignes gérées.

## 2. Lire, dans cet ordre

1. L'étape entière dans `docs/plan/NN-*.md` (lien de `NEXT.md`).
2. Chaque référence citée : specs (règles exactes), technique (`DT-nn`, sections), maquette (`docs/design/maquette-v1/preview/<Écran>.html`, ouvert dans un navigateur ou lu en HTML) et Strata si l'étape touche l'interface.
3. La mémoire utile (`MEMORY.md`, `.github/memory/`), et graphify si `graphify-out/` existe.
4. Les skills partagées concernées : `cqrs-feature`, `xunit-testing`, `angular-strata`, `tdd-workflow`.

## 3. Faire

- Suis la liste 🔧 **dans l'ordre**, avec les noms, chemins et commandes exacts.
- **Test d'abord** pour tout code exécutable : écris le test, vérifie qu'il échoue pour la bonne raison, implémente
  jusqu'au vert, refactorise.
- Interface : libellés mot pour mot de la maquette ; uniquement les zones retenues ; composants `app-ds-*` et variables
  `--ifs-*` ; captures 1440 et 390 dans `docs/plan/revues/captures/<prochain R-nn>/<écran>-<largeur>.png`.
- Nouvelle route : scénario d'isolation déclaré, `.WithName()`, politique de débit, `dotnet build` (OpenAPI) puis
  `npm run api:generate`.
- Écart avec le code réel (nom, signature) : suis le code réel en restant fidèle à l'intention ; note l'écart dans la
  section « Écarts » de l'auto-revue.
- Contradiction avec une spec, une règle de sécurité ou de données personnelles, ou choix non écrit : marque `BLOQUE`,
  note la question précise dans `NEXT.md` et demande à Florian le seul arbitrage manquant. Reprends après vérification et
  preuve de résolution.

## 4. Vérifier

- Toutes les commandes ✅ de l'étape, puis la suite complète de la surface touchée (`dotnet build` sans avertissement,
  `dotnet test`, `npm run lint`, `npm test`, `npm run build`, `npm run e2e` si UI et AppHost disponible).
- Exécute toi-même chaque 🧪 à l'aide du navigateur, de la CLI ou de l'application accessible. Consigne les résultats dans
  `docs/plan/recettes/suivi.md` et actualise `NEXT.md`. Ne laisse une ligne en attente que si un accès ou une information
  réellement indisponible empêche l'exécution.
- 🧠 : mets à jour les fichiers de mémoire cités (skill `memory-management`) et une ligne de `changelog.md`.
- 📌 : si l'étape produit un état hors dépôt, ajoute-le au tableau « État hors dépôt » de `NEXT.md`.

## 5. Clore

1. `git add` des seuls fichiers de l'étape ; `git diff --cached --check` propre.
2. Commit avec le message du plan, puis une ligne vide et `Étape: <ID>`.
3. `python tools/plan/gate.py done <ID>` ; commit `chore(plan): <ID> terminée` (NEXT.md, JOURNAL.md) ; `git push`.
4. Si l'étape suivante est un verrou, effectue l'auto-revue : crée `docs/plan/revues/R-nn-autovalidation.md` avec les
   rubriques du modèle, puis `python tools/plan/gate.py auto-review R-nn`. Ensuite reprends au § 1 pour l'étape suivante,
   tant que la session le permet. En fin de session, `NEXT.md` est à jour et poussé.
