# Traitement de la revue du plan d'implémentation — 4 octobre 2026

Réponse de Claude à [2026-10-04-revue-plan-implementation.md](2026-10-04-revue-plan-implementation.md) (Luna, 18 constats).
Les 18 constats sont acceptés et corrigés. La revue portait sur `92d07b5` : PLAN-04 et PLAN-13 étaient en partie traités
depuis (`9f2a80f`), ils sont complétés ici.

| Constat | Traitement | Où |
|---|---|---|
| PLAN-01 — SQL du témoin | L'application ne crée plus de table : `witness-schema.sql` exécuté une fois par cible par un administrateur SQL ; contrôle `sql-least-privilege` (création de table refusée) | P-01, référence pilote § 4 crit. 5 |
| PLAN-02 — Secrets | `secretWrites` (écrivain seul : `core`) et `secretReferences` (consommateurs : existence dans le coffre, jamais la valeur) ; groupe de variables lié aux stages Aperçu **et** Déploiement de l'écrivain seulement, mapping par étape | P-02, P-03, P-04 |
| PLAN-03 — Journal en Aperçu | `Read-IfsOperationJournal` en lecture seule (journal absent = vide) ; `Open-IfsOperationJournal` (création, bail renouvelé) au Déploiement seulement ; test « aucune écriture en Aperçu » | P-03 |
| PLAN-04 — Projet avant J0-07 | Agrégat `Project` minimal avec table et migration en J0-05, projet `shop` dans `seed demo` pour la recette A14 | J0-05 |
| PLAN-05 — Entrée de la génération | `revision_inputs` : instantané canonique, catalogue, langage, plateforme figés sous verrou dans la transaction de la demande ; le worker ne lit que cette entrée ; test « worker suspendu » | J0-24 |
| PLAN-06 — Équité sous saturation | Rotation forcée par `ReleaseSession()` après chaque message ; test de saturation borné ; variante B (planificateur en base) entièrement spécifiée si le test échoue | S-08, DT-32 |
| PLAN-07 — Audit et pseudonymisation | `actor_ref` opaque, nom résolu à la lecture, valeurs personnelles dans `audit_personal_data` supprimable ; `audit_events` jamais modifiée | J0-04, DT-25 |
| PLAN-08 — Télémétrie | Réglage d'organisation et contrôle serveur livrés **avec** la collecte (J0-32), rétention 13 mois | J0-01, J0-32 |
| PLAN-09 — Sortie de J1 | Critères 1–14 et 16–18 à R-12 ; critère 15 à R-15 | J1, J2 |
| PLAN-10 — Attente de recette | Nouveau statut `EN_ATTENTE_DE_RECETTE`, commandes `gate.py wait-recette` / `resume` (seulement sur résultats transmis) ; conduite unique décrite | gate.py, P-08, AGENTS.md, executer-etape |
| PLAN-11 — Job e2e | Créé à S-15 (AppHost en CI, Playwright, artefacts, arrêt garanti, régression volontaire) | S-15 |
| PLAN-12 — Propositions | Propositions avant restauration et brouillons : J2-04 Propositions, J2-05 Versions et restauration, J2-06 Brouillons ; références renumérotées | J2, design, technique |
| PLAN-13 — Projet du moteur | `InfraFlowSculptor.Engine` et `ModelSnapshot` partiel créés en J0-08, complétés en J0-11 et J0-12 | J0-08, J0-11, J0-12 |
| PLAN-14 — Place de R-11 | R-11 placé avant J1-12, branche `impl/j1-sortie` | J1 |
| PLAN-15 — Preuve P3 | Deux cas définis : révision plus récente fusionnée (contrôle du manifeste à la tête de la branche) et changement Azure (empreinte recalculée sur l'artefact figé) | P-03, référence pilote crit. 9 |
| PLAN-16 — Preuve P4 | Révision 3 déployée en dev **et** prd avant le saut de la révision 4 | Référence pilote crit. 10 |
| PLAN-17 — Manifeste d'exemple | Déplacé hors du dossier comparé (`reference/pilot/manifest.example.json`) ; comparaison intégrale sans exception | P-06, référence pilote § 3 |
| PLAN-18 — Niveau de détail | Marque `> **Niveau : découpé.**` dans J1, J2, J3 ; `gate.py check` refuse leurs étapes ; `detailler-jalon` retire la marque ; README honnête sur ce que `lint` mesure | gate.py, README du plan |
| Points secondaires | Exception « même branche » (R-02) écrite dans le README et `executer-etape` ; enregistrements Azure DevOps anonymisés collectés pendant les preuves (`Save-AdoRecordings.ps1`) ; build Code tranché au jalon 2 (DT-41) | P-07, DT-41 |

**Écarts de spec à amender au verrou R-03** : RG-CMP-05 (DT-39) et 04 § 2.1 (DT-41), par une nouvelle `DEC-113`.
