---
name: executer-etape
description: "Use at the start of every Codex session in InfraFlowSculptor, and whenever asked to continue, implement, or 'faire l'étape suivante'. Executes the current step and its validation end to end: gate check, test first, implement, run the recipes, self-review, update memory, commit, gate transition and push."
---

# Exécuter l'étape courante du plan

La conception de référence est dans les specs, décisions, plan et maquettes. Tu exécutes **l'étape courante** de `NEXT.md`,
puis la suivante, jusqu'à un vrai manque d'information, un accès indisponible ou un risque de coût non chiffré. À chaque
verrou 🔒, demande une revue de code à Claude CLI selon `.agents/skills/demander-revue/SKILL.md`, puis fais l'auto-revue
Codex : Claude fournit des constats, Codex les vérifie et reste responsable du verdict et du gate.

## 1. Ouvrir

1. `git pull` sur la branche du segment (`NEXT.md`, ligne « Branche »). En début de segment (étape qui suit un verrou
   approuvé, ou S-01) : `git fetch origin main` puis `git switch -c impl/<segment> origin/main` avec le nom donné par le
   verrou précédent — sauf si ce verrou dit « même branche » (R-02) : rester sur la branche du segment ; mets à jour la ligne « Branche » de `NEXT.md`.
2. `python tools/plan/gate.py check` → code 3 : vérifie le motif et poursuis les contrôles indépendants. Un statut
   `BLOQUE` ne se lève qu'après vérification de la condition et consignation de la preuve avec `gate.py unblock <ID> <preuve>`.
   Un statut `EN_ATTENTE_DE_RECETTE` signifie que Codex doit exécuter la recette ; `EN_ATTENTE_DE_REVUE` signifie qu'il
   doit faire l'auto-revue.
3. Si l'étape courante est un verrou `R-nn`, demande d'abord la revue Claude CLI du segment selon la skill `demander-revue`,
   puis exécute la checklist d'auto-revue au § 5.
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
- Interface : avant le code, associe chaque route et état livré à son fichier `preview/<Écran>.html` et délimite les
  zones du jalon. Pour chaque écran maquetté, lance le front et la référence à la même taille (1440×900), compare-les
  côte à côte, puis corrige et recapture jusqu'à obtenir une concordance trait pour trait dans les zones du jalon :
  structure, hiérarchie, libellés, typographie, couleurs, espacements, dimensions et états des composants. Aucune
  différence visible n'est acceptée dans le périmètre de la maquette, sauf donnée dynamique ou zone explicitement
  exclue par le jalon ; consigne alors cette différence et sa raison. Si l'ouverture `file://` est bloquée, sers la
  référence en local. Une capture du front, les tests E2E et Axe ne remplacent pas cette comparaison. Conserve les
  libellés mot pour mot, utilise les composants `app-ds-*` et variables `--ifs-*`, et ne copie pas les données
  d'exemple dans le produit. Utilise si besoin des fixtures de recette correspondant à la maquette pour la comparaison,
  sans figer ces valeurs en production.
- Interface mobile : capture aussi l'application en 390×844 et vérifie son adaptation selon le README Strata et
  RG-UI-16. Les maquettes étant en 1440 px, ne prétends pas à une comparaison pixel à pixel mobile sans maquette
  mobile ; vérifie la hiérarchie, les contenus, les contrôles, l'absence de débordement et les règles responsive prévues.
- Preuve visuelle : dans `docs/plan/revues/captures/<prochain R-nn>/`, conserve les captures du front
  `<écran>-1440.png` et `<écran>-390.png`, ainsi que la référence rendue `<écran>-reference-1440.png`. Note dans
  `docs/plan/recettes/suivi.md` ou l'auto-revue le fichier de maquette utilisé, les tailles comparées, les
  itérations/corrections et le résultat. Ne déclare pas une fidélité exacte sans avoir regardé la comparaison finale.
- Interface sans maquette dédiée : ajoute une entrée dans `docs/design/maquette-v1/coverage.md` avant ou au début de
  l'implémentation, avec la mention explicite « Aucune maquette dédiée », la route/état, l'étape, la référence la plus
  proche et les captures. Implémente au plus près de cette référence et de Strata ; consigne les écarts et ne revendique
  pas une correspondance exacte avec une maquette inexistante. Si une décision produit importante reste impossible à
  déduire, applique la règle `BLOQUE` ci-dessous.
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
4. Si l'étape suivante est un verrou, demande la revue Claude CLI du diff depuis la base du segment et consigne les
   constats et leur traitement dans `docs/plan/revues/R-nn-autovalidation.md`. Puis effectue l'auto-revue Codex avec les
   rubriques du modèle et `python tools/plan/gate.py auto-review R-nn`. Ensuite reprends au § 1 pour l'étape suivante,
   tant que la session le permet. En fin de session, `NEXT.md` est à jour et poussé.
