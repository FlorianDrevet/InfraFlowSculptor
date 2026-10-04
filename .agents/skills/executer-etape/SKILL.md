---
name: executer-etape
description: "Use at the start of every Codex session in InfraFlowSculptor, and whenever asked to continue, implement, or 'faire l'étape suivante'. Executes exactly one step of docs/plan/ (the current step of NEXT.md) end to end: gate check, read, test first, implement, verify, prepare the manual test, update memory, commit, gate done, push — then loops or stops at a lock."
---

# Exécuter l'étape courante du plan

La conception est faite. Tu exécutes **l'étape courante** de `NEXT.md`, rien d'autre, puis la suivante, jusqu'à un
verrou 🔒 ou un blocage.

## 1. Ouvrir

1. `git pull` sur la branche du segment (`NEXT.md`, ligne « Branche »). En début de segment (étape qui suit un verrou
   approuvé, ou S-01) : `git fetch origin main` puis `git switch -c impl/<segment> origin/main` avec le nom donné par le
   verrou précédent — sauf si ce verrou dit « même branche » (R-02) : rester sur la branche du segment ; mets à jour la ligne « Branche » de `NEXT.md`.
2. `python tools/plan/gate.py check` → code 3 : arrête-toi et explique — **sauf** statut `EN_ATTENTE_DE_RECETTE` quand
   l'utilisateur te transmet dans cette session des résultats de recette : alors `gate.py resume <ID>` et suis l'étape
   (exemple : P-08). Code 0 : continue.
3. Si l'étape courante est un verrou `R-nn` → skill `demander-revue` (ou `appliquer-corrections` si le statut est
   `CORRECTIONS_DEMANDEES`). Fin.
4. Si l'étape dépend d'une décision de « Décisions à confirmer » de `NEXT.md` non confirmée → statut `BLOQUE`, question,
   commit, push, arrêt.
5. Passe le statut à `EN_COURS` (ligne « Statut » de `NEXT.md`).

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
- Écart avec le code réel (nom, signature) : suis le code réel en restant fidèle à l'intention ; note l'écart dans une
  section « Écarts » de ton résumé (repris dans la prochaine demande de revue).
- Contradiction avec une spec, une règle de sécurité ou de données personnelles, ou choix non écrit : **arrête-toi**
  (statut `BLOQUE`, question dans `NEXT.md`, commit `chore(plan): question sur <ID>`, push).

## 4. Vérifier

- Toutes les commandes ✅ de l'étape, puis la suite complète de la surface touchée (`dotnet build` sans avertissement,
  `dotnet test`, `npm run lint`, `npm test`, `npm run build`, `npm run e2e` si UI et AppHost disponible).
- Prépare le 🧪 : données (`seed demo`), commandes, URL. Tu ne l'exécutes pas à la place de l'utilisateur ; ajoute
  l'étape à « Tests manuels en attente de vous » de `NEXT.md` (une ligne : ID, lien vers la rubrique 🧪).
- 🧠 : mets à jour les fichiers de mémoire cités (skill `memory-management`) et une ligne de `changelog.md`.
- 📌 : si l'étape produit un état hors dépôt, ajoute-le au tableau « État hors dépôt » de `NEXT.md`.

## 5. Clore

1. `git add` des seuls fichiers de l'étape ; `git diff --cached --check` propre.
2. Commit avec le message du plan, puis une ligne vide et `Étape: <ID>`.
3. `python tools/plan/gate.py done <ID>` ; commit `chore(plan): <ID> terminée` (NEXT.md, JOURNAL.md) ; `git push`.
4. Si `gate.py` annonce un verrou : skill `demander-revue`. Sinon reprends au § 1 pour l'étape suivante, tant que la
   session le permet. En fin de session, `NEXT.md` est à jour et poussé.
