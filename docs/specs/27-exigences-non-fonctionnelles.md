# 27 — Exigences non fonctionnelles

## Sécurité et isolation

**EXG-01 — Isolation des organisations.** Toute donnée appartient à une organisation. Chaque requête,
lecture comprise, filtre par organisation et vérifie le rôle sur le projet ([RG-ORG-11](10-organisations-et-acces.md)).
Une suite de tests automatisés vérifie, pour **chaque** route de l'API et chaque outil MCP, qu'un
utilisateur d'une autre organisation ou d'un autre projet obtient « introuvable ». Une route sans ce
test ne peut pas être livrée.

**EXG-02 — Secrets.** IFS ne stocke aucun secret applicatif ([DEC-04](03-decisions.md)). Les jetons git de
repli et les secrets propres à IFS sont dans un coffre géré, jamais en base ni dans les journaux. Les
jetons d'API ne sont stockés que sous forme d'empreinte.

**EXG-03 — Référentiel.** L'application vise OWASP ASVS 4 niveau 2. Test d'intrusion externe avant
l'ouverture commerciale, puis annuel.

**EXG-04 — Chiffrement.** TLS 1.2 minimum en transit, chiffrement au repos de toutes les données et
sauvegardes.

**EXG-05 — Chaîne d'approvisionnement.** Dépendances analysées à chaque build ; une vulnérabilité
critique connue bloque la livraison. Les modules, fournisseurs et paquets utilisés dans les sorties
sont des versions épinglées ([DEC-45](03-decisions.md)).

## Données et conformité

**EXG-06 — Hébergement et RGPD.** Données hébergées dans l'Union européenne. Données personnelles
limitées au profil (nom, e-mail, identifiants Entra) et au journal d'audit. Export et effacement des
données d'un utilisateur sur demande, sauf le journal d'audit, conservé pour sa durée légale avec
pseudonymisation de l'auteur après départ.

## Performance

**EXG-07 — Temps de réponse.**

| Opération | Cible (p95) |
|---|---|
| Lecture ou commande unitaire de l'API | < 300 ms |
| Validation complète d'un projet de 300 ressources | < 2 s |
| Génération d'une révision d'un projet de 300 ressources, contrôle de la sortie compris | < 30 s |
| Préparation d'une publication (lecture des dépôts, diff) | < 20 s par dépôt |

**EXG-08 — Volumes.** Un projet supporte au moins 20 environnements, 50 composants, 1 000 ressources et
5 000 liaisons, avec des temps au plus doublés par rapport à EXG-07.

## Disponibilité et exploitation

**EXG-09 — Disponibilité.** 99,5 % mensuel hors maintenance annoncée. Une indisponibilité d'IFS n'empêche
jamais un client de déployer : ses pipelines n'appellent pas IFS.

**EXG-10 — Sauvegarde.** RPO 1 heure, RTO 4 heures. Restauration testée chaque trimestre.

**EXG-11 — Observabilité.** Traces distribuées, journaux structurés, métriques par organisation
(générations, publications, échecs). Chaque échec de contrôle de sortie ([RG-GEN-04](21-generation-et-revisions.md))
déclenche une alerte à l'équipe IFS.

## Qualité du produit

**EXG-12 — Accessibilité.** WCAG 2.2 AA pour toute l'interface.

**EXG-13 — Langues.** Français et anglais, complets.

**EXG-14 — Navigateurs.** Les deux dernières versions majeures de Chrome, Edge, Firefox et Safari.

**EXG-15 — API.** API REST versionnée (`/v1`), décrite en OpenAPI, réponses d'erreur au format
`application/problem+json` avec code de règle et erreurs par champ. Toute fonction de l'écran est
disponible par l'API.

**EXG-16 — Tests d'acceptation de la sortie.** Le projet de référence ([30](30-projet-de-reference.md)) est
généré à chaque build d'IFS, **pour chaque combinaison langage × plateforme livrée** ([04 § 1](04-perimetre-et-lots.md)) ;
sa sortie est comparée à des fichiers de référence. Chaque mise à jour du catalogue ou d'un émetteur
déploie réellement le projet de référence, dans chaque langage livré, sur un abonnement de test, avec un
résultat attendu sans erreur.

**EXG-17 — Tests d'intégration fournisseurs.** Chaque version d'IFS publie le projet de référence sur de
vrais dépôts de chaque fournisseur livré, avec les trois préréglages du plan de publication, exécute le kit
d'installation sur chaque plateforme CI livrée, et fait tourner les pipelines produits de bout en bout.

**EXG-18 — Déterminisme.** Voir [RG-GEN-03](21-generation-et-revisions.md) ; vérifié par EXG-16.

**EXG-19 — Parité entre langages.** Pour le projet de référence, les ressources Azure obtenues après
déploiement sont identiques quel que soit le langage : mêmes types, noms, groupes, régions, propriétés
gérées par IFS, tags, identités et attributions de rôles. La comparaison porte sur un export normalisé des
ressources ; les écarts tolérés (propriétés calculées par Azure, valeurs par défaut propres à une version
d'API) sont listés et justifiés. Un écart non listé bloque la livraison d'un émetteur
([DEC-44](03-decisions.md)).

**EXG-20 — Protection des états.** Les stockages d'état créés par le kit ([23 § 2](23-kit-installation.md))
n'acceptent que l'authentification Entra, ont le versioning et la suppression réversible activés, et ne
donnent l'accès en écriture qu'aux identités de déploiement de leur cible. Le kit vérifie ces réglages à
chaque exécution et signale toute dérive.
