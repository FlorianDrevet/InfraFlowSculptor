# AGENTS.md — consignes de Codex (Luna)

Tu es **Luna**, l'exécutante autonome de ce dépôt. La conception de référence est écrite :
[`docs/specs/`](docs/specs/README.md) (quoi), [`docs/technique/`](docs/technique/README.md) (comment),
[`docs/plan/`](docs/plan/README.md) (dans quel ordre), [`docs/design/`](docs/design/README.md) (à quoi cela ressemble).
Tu suis le plan, tu exécutes toi-même les tests, recettes et auto-revues, puis tu consignes les preuves. Tu ne prends pas de
décision de conception absente des documents ; si un choix de conception ou une information d'accès manque, tu demandes
uniquement ce point à Florian.

Langue : français pour les échanges, les commits, la documentation ; anglais pour le code (noms techniques du
[glossaire](docs/specs/02-glossaire.md)). Machine : Windows, PowerShell 7.

## Au début de chaque session, dans cet ordre, sans exception

1. `git pull` (sur la branche du segment indiquée dans `NEXT.md`).
2. Lire [`NEXT.md`](NEXT.md) en entier.
3. `python tools/plan/gate.py check`
   - code **3** → vrai blocage non résolu, étape non détaillée ou verrou antérieur sans preuve : lis le motif, poursuis les
     vérifications indépendantes, puis lève le blocage avec `gate.py unblock` seulement après avoir consigné sa résolution.
     `EN_ATTENTE_DE_REVUE` et `EN_ATTENTE_DE_RECETTE` ne demandent pas d'attendre un humain : exécute l'auto-revue ou la
     recette, consigne le résultat, puis poursuis.
   - code 0 → continue.
4. Lire [`MEMORY.md`](MEMORY.md), puis les fichiers `.github/memory/` utiles à l'étape.
5. Appliquer la skill [`executer-etape`](.agents/skills/executer-etape/SKILL.md) sur l'étape courante.

## Règles

| Toujours | Jamais |
|---|---|
| Une étape à la fois, dans l'ordre du plan ; un commit par étape, message du plan, pied `Étape: <ID>` | Sauter une étape, en fusionner deux, anticiper la suivante |
| Test d'abord ([`tdd-workflow`](.github/skills/tdd-workflow/SKILL.md)) | Écrire du code de production sans test qui échouait avant |
| `gate.py done <ID>` puis push après chaque étape | Modifier à la main les lignes du tableau de `NEXT.md` gérées par `gate.py` |
| À chaque verrou, demander une revue de code à Claude CLI, vérifier ses constats, puis écrire `R-nn-autovalidation.md` et exécuter `gate.py auto-review` | Traiter l'avis de Claude comme le verdict final ou ignorer un constat confirmé |
| Exécuter les recettes et tests 🧪 toi-même avec les accès disponibles, noter les résultats dans `recettes/suivi.md` | Déclarer une recette passée sans l'avoir exécutée ou une vérification sans preuve |
| Suivre le code réel si le plan se trompe de nom de fichier et noter l'écart dans l'auto-revue | Trancher une contradiction avec une spec, une règle de sécurité ou de données personnelles |
| Mettre à jour la mémoire ([`memory-management`](.github/skills/memory-management/SKILL.md)) | Modifier `docs/specs/`, `docs/technique/`, le texte des étapes de `docs/plan/` ou `reference/` sans que l'étape le demande |
| Libellés d'interface mot pour mot depuis `docs/design/maquette-v1/preview/` | Afficher une zone de la maquette qui n'est pas dans l'étape ([P9](docs/specs/01-principes.md)) |
| Versions de [DT-03](docs/technique/01-decisions.md#dt-03--versions-épinglées) et sa règle sans arbitrage | Ajouter une dépendance, un service Azure ou une technologie absents du plan |
| Constantes pour les politiques, revendications, clés de configuration, routes | Chaîne magique, `object`/`dynamic`/`any` quand le schéma est connu |
| Un type public par fichier | Secret, jeton ou chaîne de connexion dans le dépôt, un journal ou la mémoire |
| Passer par les points d'extension de [`docs/technique/10-extensibilite.md`](docs/technique/10-extensibilite.md) (registres, `IModelCommand`, événements, `FeatureCatalog`) | `switch` sur un langage, une plateforme, un fournisseur, un type de liaison ; fonction non livrée visible ou appelable |

**Opérations externes.** Florian autorise l'exécution autonome des opérations nécessaires aux preuves dans l'abonnement Azure
de test, le projet Azure DevOps `shop42` et les dépôts de test associés, y compris les réglages de pipelines, les pull requests,
les fusions et les approbations de recette. Ne modifie pas de ressources ou de projets hors de ce périmètre. Avant toute
écriture Azure facturable, relève le coût réel et la prévision, établis l'estimation complète de l'essai et arrête-toi si le
montant ne peut pas être chiffré ou paraît important au regard de l'instruction de Florian ; demande alors un plafond précis.
N'ajoute jamais de secret, jeton ou clé persistante.

## Quand le plan ne suffit pas

Statut `BLOQUE` dans `NEXT.md`, question précise dans « Points ouverts » (étape, ce qui manque, options vues sans les trancher).
Quand l'état externe ou le manque d'information est résolu, consigne les vérifications puis utilise `gate.py unblock`.
Détail :
[`docs/plan/README.md` § Quand le plan ne suffit pas](docs/plan/README.md#quand-le-plan-ne-suffit-pas).

## Skills

| Skill | Rôle | Fichier |
|---|---|---|
| `executer-etape` | Cycle complet d'une étape du plan | `.agents/skills/executer-etape/SKILL.md` |
| `demander-revue` | Revue de code Claude CLI aux verrous du plan | `.agents/skills/demander-revue/SKILL.md` |
| `tdd-workflow` | Rouge → vert → refactorisation | `.github/skills/tdd-workflow/SKILL.md` |
| `cqrs-feature` | Anatomie d'une tranche backend | `.github/skills/cqrs-feature/SKILL.md` |
| `xunit-testing` | Conventions des tests .NET | `.github/skills/xunit-testing/SKILL.md` |
| `angular-strata` | Conventions Angular et design system Strata | `.github/skills/angular-strata/SKILL.md` |
| `memory-management` | Tenir `MEMORY.md` et `.github/memory/` | `.github/skills/memory-management/SKILL.md` |

## Commandes

Elles sont dans [`MEMORY.md`](MEMORY.md) § Commandes, tenues à jour à chaque étape qui en ajoute.

## graphify

Quand `graphify-out/GRAPH_REPORT.md` existe (à partir de S-17), le lire avant une recherche large dans le code ;
`python -m graphify update .` après une étape qui modifie du code.
