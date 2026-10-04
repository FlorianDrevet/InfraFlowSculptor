---
name: revue-verrou
description: "Use when the user asks Claude to review a lock (« fais la revue », « relis R-nn », « Luna a fini »), or NEXT.md status is EN_ATTENTE_DE_REVUE. Claude (Opus) reviews the whole segment against plan, specs, technique and mockups, writes docs/plan/revues/R-nn-revue.md with a verdict, and lifts or keeps the lock with tools/plan/gate.py."
---

# Revue d'un verrou 🔒

Tu es le relecteur exigeant d'un code écrit par un autre modèle. Tu juges ce qui doit atteindre `main`.

## 1. Cadrer
1. `git fetch`, se placer sur `impl/<segment>` ; `python tools/plan/gate.py status` (statut `EN_ATTENTE_DE_REVUE`).
2. Lire `docs/plan/revues/R-nn-demande.md`, le texte du verrou dans le plan (« Claude vérifie »), les étapes du segment.
3. Diff complet du segment : `git diff origin/main...HEAD --stat` puis fichier par fichier (code d'abord, tests ensuite).

## 2. Vérifier par toi-même
- Relancer : `dotnet build` (0 avertissement), `dotnet test`, frontend (`lint`, `test`, `build`, `e2e` si l'AppHost
  tourne), `gate.py lint`, Pester si la release est touchée.
- Pour chaque étape : chaque point 🔧 est-il fait, au bon endroit, avec les bons noms ? chaque règle citée
  (`RG-…`, `VAL-…`, `DEC-…`, `DT-…`) est-elle respectée **et testée** ?
- Extensibilité ([`docs/technique/10-extensibilite.md`](../../../docs/technique/10-extensibilite.md) § 5) : registres au lieu de `switch`,
  `IModelCommand` sérialisables passant par l'exécuteur, effets externes par événements ou files, routes avec clé de fonction.
- Angles obligatoires : isolation (EXG-01, chaque route a son scénario), autorisation (attribut sur chaque commande et
  requête), secrets (aucun en base, journal, fichier, mémoire), un seul lieu de calcul (rien dans l'API ni le front),
  pureté du moteur, déterminisme et parité avec `reference/`, P9 (aucune zone d'un jalon ultérieur), fidélité aux
  maquettes (captures contre `docs/design/maquette-v1/preview/`), accessibilité, dette de test, conventions (`AGENTS.md`).
- Recette : `docs/plan/recettes/suivi.md` porte le résultat de la section du segment (OK, ou report explicite de l'utilisateur).
  Sans recette, pas d'approbation : verdict `CORRECTIONS` avec « recette à exécuter ».

## 3. Écrire la revue
`docs/plan/revues/R-nn-revue.md` selon le modèle de `docs/plan/revues/README.md`. Gravités : BLOQUANT, MAJEUR, MINEUR.
- Tout BLOQUANT ou MAJEUR non arbitré → `Verdict : CORRECTIONS`, corrections numérotées `R-nn-Cn` **exécutables sans
  décision** (fichier, changement attendu, test attendu).
- Sinon `Verdict : APPROUVÉ` ; les MINEURS deviennent des points ajoutés à une étape ultérieure du plan (édite-la).

## 4. Conclure
- Décisions nées de la revue : nouvelles `DT-nn` dans `docs/technique/01-decisions.md`, plan modifié (étapes ajoutées ou
  précisées, `gate.py lint` vert), statuts de preuves dans `docs/specs/04-perimetre-et-lots.md` § 7.2 si concerné.
- Verrou de fin de jalon : skill `detailler-jalon` **avant** d'approuver.
- Skill `consolider-memoire`.
- `python tools/plan/gate.py approve R-nn --by Claude` (ou `reject R-nn --by Claude`) ; commit
  `docs(plan): revue R-nn — <verdict>` ; push sur la branche du segment.
- Dire à l'utilisateur : le verdict, les points majeurs, et s'il doit fusionner la pull request (approbation) ou relancer
  Luna (corrections).
