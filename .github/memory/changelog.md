# Changelog de la mémoire

| Date | Auteur | Changement |
|---|---|---|
| 2026-10-04 | Claude | Création de la mémoire : vue d'ensemble, structure, agents, graphe, design system ; autres fichiers en attente du code |
| 2026-10-04 | Claude | DT-33 identité managée, DT-34 langues, DT-30 thème prêt, DT-22 alternatives, guide Keycloak, maquette corrigée (v28) |
| 2026-10-04 | Claude | DT-22 confirmée : Mediator remplace MediatR |
| 2026-10-04 | Claude | DT-08 confirmée : toutes les décisions techniques sont tranchées |
| 2026-10-04 | Claude | Revue du plan par Luna traitée (18 constats) : statut EN_ATTENTE_DE_RECETTE, jalons découpés refusés par gate.py |
| 2026-10-05 | Luna | S-01 — prérequis machine, configurations du dépôt, tests du garde-fou et hook pre-commit |
| 2026-10-05 | Luna | S-02 — squelette CQRS généré dans `src/backend/` et solution compilée |
| 2026-10-05 | Luna | S-03 — backend .NET 10, ServiceDefaults, Mediator, API `/v1/version`, ProblemDetails et retrait de l'auth locale |
| 2026-10-05 | Luna | S-04 — cinq projets de tests, tests Domain/API et règles d'architecture automatisées |
| 2026-10-05 | Luna | S-05 — AppHost Aspire, émulateurs, résolution de clients Azure, tests d'acceptation et recette locale |
| 2026-10-05 | Luna | S-06 — EF Core/PostgreSQL 17, isolation par organisation, concurrence, outbox, idempotence et migration initiale |
| 2026-10-05 | Luna | S-07 — authentification OIDC Keycloak/Entra, utilisateur courant, résolution d'adresse vérifiée, `/v1/me` et OAuth2 PKCE dans Scalar; recette locale partielle, volume Keycloak conservé |
| 2026-10-06 | Luna | S-08 — worker, outbox, sessions, fairness, leases et traces; conteneurs locaux persistants entre les lancements; corrections additives Keycloak conservant le volume; revue Sonnet traitée (renouvellement de bail, warmup worker, batch/retries d'outbox); recette Alice/Nina et import 26.6 validés; 57 tests et build 15 projets verts |
| 2026-10-06 | Luna | S-09 — application Angular 22, configuration runtime et OIDC; rôle offline_access corrigé sur les comptes locaux persistants, bearer limité à l'API; textes d'authentification traduits, sortie config ignorée par Git; recette Bob/profil/rechargement; revue Sonnet; 57 tests .NET, 4 Python, 3 Angular et build 15 projets verts |
| 2026-10-06 | Luna | S-10 — génération des tokens Strata et thèmes multiples depuis `tokens.json`, polices Fontsource locales, styles de base et galerie dev; 3 tests Angular + 3 générateur, lint, tokens:check et build verts. Comparaison visuelle partielle : Chrome bloque la référence `file://`, valeurs contrôlées depuis la source et la galerie. |
| 2026-10-06 | Luna | S-13 — coquille Angular, connexion et erreurs API, métadonnées HTTP limitées à l'API pour éviter le préflight Keycloak, langue/thème, Playwright desktop/mobile; 34 tests Angular, 6 tests tokens/icônes et 6 scénarios E2E verts, lint/build réussis. |
| 2026-10-06 | Luna | S-14 — OpenAPI généré au build, document runtime déterministe, client Angular ng-openapi-gen et contrôle de dérive; sorties client normalisées en LF/fin de ligne unique, exclues du formatage; revue Sonnet traitée; 58 tests .NET, 34 tests Angular, 6 tests outils et 6 scénarios E2E verts. |
| 2026-10-06 | Luna | S-15 — workflow CI pour backend, acceptation Docker, frontend, audit des dépendances et E2E Aspire; OpenAPI/client Angular vérifiés contre les artefacts commis; rapports TRX/JUnit, couverture, Playwright et journaux publiés; badge CI ajouté. Le build AppHost en `Development` a révélé une validation DI incompatible avec l'hôte temporaire `GetDocument`, limitée à ce générateur; build Release et 14 tests API passent. |
| 2026-10-06 | Luna | S-16 — observabilité et diagnostic du Service Bus local : télémétrie, traces et logs structurés, classification prudente de la fermeture `AcceptSession`, build et CI verts. |
| 2026-10-06 | Luna | S-17 — mémoire alignée sur le code, graphe Graphify AST généré sans LLM et section « Démarrer » ajoutée au README. Prérequis et contrôles automatiques verts ; la recette clone neuf reste partielle tant que l'AppHost actif occupe les ports fixes. |
| 2026-10-06 | Luna | P-01 — application témoin .NET 10, contrôles de dépendances à identité managée, schéma SQL, image non-root et tests. |
| 2026-10-06 | Luna | S-08 — le fixture d'acceptation tolère les notifications API DCP périmées lorsque `/health` répond; budget Service Bus élargi pour le sidecar SQL à froid. |
| 2026-10-06 | Luna | S-15 — le job Playwright injecte le secret d'Actions dans le royaume Keycloak jetable pour activer les quatre scénarios authentifiés. |
| 2026-10-06 | Luna | Documentation — ajout d'un REX débutant Keycloak/Aspire, relié depuis l'index technique et la mémoire auth/build. |
| 2026-10-15 | Luna | P-08 — région France Central décidée ; préflight de disponibilité 1–3 réussi en lecture seule, tarifs actualisés et budget consulté ; confirmation du plafond/estimation encore requise. |
| 2026-10-15 | Luna | P-08 — plafond et sous-total confirmés ; sortie pilote et overlays alignés sur France Central, manifeste recalculé, contrôles de déterminisme et Bicep réussis, 69 tests Pester verts. |
| 2026-10-15 | Luna | P-08 — CI GitHub corrigée (8/8 checks), projet privé Azure DevOps `shop42`, groupes Entra/Azure DevOps et dépôt de référence préparés. Préflight coûts : projections contradictoires et supérieures au plafond de 200 EUR/mois ; aucune ressource Azure créée, étape en attente de réconciliation des coûts. |
| 2026-10-17 | Luna | P-08 — mémoire actualisée après les runs #739/#738 et l’arbitrage Claude option B ; #738 reste la preuve négative, le parcours positif et le nettoyage Shop42 restent en attente. |
| 2026-10-17 | Luna | P-08 D1a — diagnostic #748, comparateur What-If hors ligne testé et protocole D1b ; attente des cinq aperçus DEV personnels, sans changement au fail-closed. |
| 2026-10-18 | Luna | P-08 D1b — cinq aperçus personnels identiques, rôles Reader limités consignés, #748 toujours non diagnostiqué ; D2/D3 en attente de définition Claude. |
| 2026-10-18 | Luna | P-08 D2a — capture de l’aperçu recalculé en cas de divergence sans toucher à l’aperçu approuvé ; tests Pester ciblés et validations Bicep, déterminisme et pipelines verts. |
| 2026-10-18 | Luna | P-08 D2b — répétition et clone local validés ; audit des triggers bloque le push avant Azure Repos ; C9 et choix utilisateur A/B/C consignés. |
| 2026-10-23 | Codex | Workflow — recettes exécutées par Codex, auto-revue des verrous, preuve de résolution des blocages et coûts vérifiés avant les écritures Azure. |
| 2026-10-24 | Codex | P-08 — reprise bornée des timeouts SQL post-login et constat du chemin de reprise PRD à préciser. |
| 2026-10-24 | Codex | P-08 — mécanisme de reprise PRD protégé, lié à l’opération #762 exacte, ajouté à la référence et auto-revu ; publication et exécution restent à faire. |
| 2026-10-24 | Codex | P-08 — empêche le réarmement du journal après reprise lorsque le commit du manifeste évolue ; 132 tests, Bicep, YAML, déterminisme et analyse statique validés. Cost/C5 actualisés et huit pipelines collatéraux suspendus pour la publication. |
| 2026-10-24 | Codex | P-08 — récupération PRD #778 réussie après correction du retry SQL ; préflight C4, inventaire C5, santé C6 et contrôle C3/C9 actualisés avant le D3 conditionnel. |
| 2026-10-24 | Codex | P-08 — blocage #762 levé par preuve et gate.py ; préflight final avant D3 rafraîchi, C3/C5/C9 et coût confirmés, compilation `previewRun` sans run créée. Le D3 unique reste conditionné à C8. |
| 2026-10-24 | Codex | P-08 — D3 #780 réussi en DEV/PRD avec C8 valide ; nettoyage suspendu après What-If exact `orders-prd` prévoyant cinq modifications. Coûts et C5 actualisés ; blocage consigné pour arbitrage. |
| 2026-10-25 | Codex | P-08 — nettoyage Shop42 confirmé (C5/C3/coût) ; Florian autorise explicitement shop43 et shop44, les projets et dépôts sont préparés, et le contrôle Cost Analysis J+1 reste dû le 2026-10-26 avant toute écriture Azure facturable. |
| 2026-10-25 | Codex | P-08 P2 shop43 : révocation Log Analytics Reader confirmée et tentative de suppression bloquée par denyDelete ; workspace sain, C5 inchangé et coût actualisé. |
| 2026-10-25 | Codex | P-08 P4 révision 3 shop43 : #837 a repris l’opération SQL DEV laissée Started par #836 puis réussi DEV/PRD ; C5 174/29/7/63/7, coût et captures actualisés. |

| 2026-10-25 | Codex | P-08 — P4 shop43 rev4 #840 exécutée en DEV, approbation PRD refusée selon recette ; C5 inchangé, coût post-run actualisé, deux identités détachées à nettoyer après rev5. |

| 2026-10-26 | Codex | P-08 P4 shop43 rev5 — release #843 réussie en DEV/PRD ; rapport détachement, C5/coût et captures consignés. |
| 2026-10-26 | Codex | P-08 T shop43 bloquée par les What-If (5/4/1 Modify) ; J+1 shop42 inconclusif, données Cost Management jusqu’au 24/10. Aucun deleteAll shop43 exécuté. |
| 2026-10-26 | Codex | P-08 — diagnostic What-If exact : règle de réconciliation retenue ; différence de destination Log Analytics sur l’environnement Container Apps confirmée, nettoyage shop43 toujours suspendu. |
| 2026-10-27 | Codex | P-08 clôturé pour shop44 : piles et ressources de preuve retirées, baseline préservée, journaux copiés et vérifiés, C3/C5/coût final consignés ; contrôle quotidien par groupe indisponible sur HTTP 429, déviation acceptée par Florian. |
| 2026-10-27 | Codex | R-03 — crochet d'interruption P7 retiré de la sortie publiée ; preuves P1–P5/P7–P9 prototypées et écart de journalisation `revoke` consigné. |
| 2026-10-27 | Codex | Workflow — rétablir une revue de code Claude CLI en lecture seule à chaque verrou, avec vérification des constats et verdict final Codex. |
| 2026-10-27 | Codex | J0-01 validée localement — backend, profils, organisations, isolation, préférences, disponibilité des fonctions, interface et recette authentifiée 6/6 desktop/mobile ; correctif du tiroir mobile |
| 2026-10-27 | Codex | J0-02 — rôles et cycle de vie des membres, protection RG-ORG-05, restrictions par domaines/tenants, routes API et interface ; 104 tests .NET, 43 tests frontend et 16/16 E2E passent |
| 2026-10-27 | Codex | J0-03 — invitations à jeton empreint, contrôle d’adresse vérifiée, e-mail via outbox, adaptateurs MailPit/ACS, interface d’invitation et retour après connexion |
| 2026-10-27 | Codex | J0-04 — journal append-only, purge bornée à 13 mois, consultation d’organisation et recette E2E desktop/mobile validée (18/18) |
| 2026-10-27 | Codex | J0-05 — permissions et rôles de projet, agrégat `shop`, autorisation systématique, endpoints OpenAPI, attribution plafonnée, dernier propriétaire et audit ; 155 tests backend et 59 tests frontend passent |
| 2026-10-27 | Codex | R-04 — revue de sécurité clôturée, idempotence transactionnelle isolée par utilisateur, protections d'audit renforcées et validation du jeton restaurée ; 205 tests .NET et 63 tests frontend verts, A14 Scalar validée ; conflit PostgreSQL et réponse 409 commitée après savepoint vérifiés ; écart E2E dû aux données persistantes consigné |
| 2026-10-27 | Codex | J0-06 — catalogue pilote JSON versionné, fournisseur immuable, validation des schémas, routes sans organisation, mappings AVM épinglés et type JSON récursif du client généré ; backend et frontend verts |
| 2026-10-27 | Codex | J0-07 — projets et environnements, tags, commandes du modèle transactionnelles, verrouillages après publication, restauration et purge ; 240 tests .NET, 64 tests frontend et builds verts |
| 2026-10-27 | Codex | J0-08 — assistant de création, brouillon serveur 30 jours, moteur de noms initial et création atomique ; preuves et limites E2E consignées |
| 2026-10-28 | Codex | J0-09 — Florian confirme le stockage relationnel des favoris/récents (JSONB exclu), reporte les compteurs à J0-10/J0-20 et autorise les personas Keycloak locaux ; les récents seront livrés avec l'accueil J0-32 |
| 2026-10-28 | Codex | J0-09 — écrans du projet terminés : favoris relationnels, formulaires Angular corrigés, 71 tests frontend et 261 tests backend verts ; revue Claude ciblée sans défaut bloquant ; CI complète 40660436427 verte, E2E et nettoyage des artefacts validés |
