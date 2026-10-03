# 33 — Gouvernance, coûts, supervision et dérive *(lot 2, vague E)*

## 1. Objectif

Donner à l'équipe plateforme les garde-fous d'une organisation (politiques), la visibilité financière
(coûts, budgets), la supervision par défaut (alertes) et la détection des écarts entre Azure et le modèle
(dérive). Le tout sans qu'IFS n'accède aux abonnements Azure du client.

## 2. Politiques d'organisation ([DEC-71](03-decisions.md))

Une politique est une règle de validation définie par l'organisation, qui s'ajoute à celles d'IFS.

| Champ | Contenu |
|---|---|
| Règle | Une règle du catalogue de politiques (section 2.1), avec ses paramètres. |
| Portée | Tous les projets, ou une liste de projets. Appliquée à toutes les cibles, ou aux seules cibles protégées. |
| Effet | `Erreur` (bloque la génération) ou `Avertissement`. |
| Description | Raison affichée dans les constats. |

### 2.1 Catalogue de politiques

| Règle | Paramètres |
|---|---|
| Régions autorisées | Liste de régions. |
| Types interdits | Liste de types. |
| Valeurs interdites | Type, propriété, valeurs (exemple : SKU `Basic` en cible protégée). |
| Exposition publique interdite | Types concernés. |
| Authentification locale interdite | Types concernés. |
| Tags obligatoires | Clés, valeurs autorisées (liste ou motif). |
| Redondance minimale | Par type : niveau minimal (exemple : stockage `ZRS`, base redondante en zone). |
| Préfixe de nom imposé | Texte fixe imposé en tête des noms Azure. |
| Déploiement de modèle d'IA | Types de déploiement autorisés (exemple : `DataZone*` seulement). |
| Journalisation obligatoire | Espace Log Analytics par défaut obligatoire pour tous les composants. |

**RG-GOV-01 — Constats.** Une politique produit des constats `VAL-POL-REGLE`, avec le nom de la règle, de la politique
et sa description. Un constat de politique en `Erreur` ne s'acquitte pas.

**RG-GOV-02 — Dérogations.** Un propriétaire de projet demande une dérogation à une politique : objets
concernés, justification, date d'expiration (12 mois maximum). Un administrateur d'organisation l'approuve
ou la refuse. Une dérogation approuvée transforme les constats concernés en `Info` mentionnant la
dérogation ; à son expiration, ils redeviennent bloquants. Demandes et décisions sont journalisées.

**UC-GOV-01 — Gérer les politiques** (administrateur d'organisation). **UC-GOV-02 — Demander une
dérogation.** **UC-GOV-03 — Approuver ou refuser une dérogation.**

## 3. Coûts ([DEC-72](03-decisions.md))

**RG-GOV-03 — Source.** L'estimation utilise l'API publique des prix de détail Azure (devise et région de
la ressource), sans accès aux abonnements ni aux remises négociées. Le tarif est rafraîchi chaque jour.

**RG-GOV-04 — Calcul.** Pour chaque ressource présente dans chaque cible, IFS estime un coût mensuel à
partir de son SKU, de sa capacité, de son nombre d'instances et de sa région.
- Les coûts liés à l'usage reposent sur des **hypothèses** affichées et modifiables par ressource, avec des
  valeurs par défaut prudentes : Go ingérés par jour (Log Analytics), requêtes, stockage, jetons pour les
  modèles d'IA standard.
- Une ressource sans tarif connu est marquée « non estimée ».

**RG-GOV-05 — Affichage.**
- Coût par ressource, composant, cible et projet, avec le détail des postes.
- Dans le résumé de chaque révision : variation estimée par cible (« +118 € / mois en prd »).
- Dans la description de la pull request.

L'écran précise toujours qu'il s'agit d'une **estimation hors remises**, pas d'une facture.

**UC-GOV-04 — Modéliser un budget.** Ressource **budget** (`Microsoft.Consumption/budgets`) à la portée d'un
groupe de ressources ou de l'abonnement de la cible : montant mensuel (surchargeable par environnement),
seuils (réel ou prévu, en %), destinataires (e-mails, groupe d'actions).

## 4. Supervision ([DEC-73](03-decisions.md))

**UC-GOV-05 — Groupe d'actions.** Ressource **groupe d'actions** : e-mails, SMS, webhooks, rôles Azure à
notifier. Destinataires surchargeables par environnement.

**UC-GOV-06 — Alertes.** Ressources **alerte de métrique** (ressource cible, métrique, agrégation, seuil
surchargeable par environnement, fréquence, gravité, groupe d'actions) et **alerte de journaux** (requête
KQL sur un Log Analytics ou un Application Insights, seuil, fréquence).

**UC-GOV-07 — Test de disponibilité.** Pour une application exposée : test standard Application Insights
(URL, fréquence, emplacements, code attendu), avec alerte associée.

**RG-GOV-06 — Alertes recommandées.** Le descripteur de chaque type propose des alertes recommandées, avec
des seuils par défaut. Exemples :
- erreurs 5xx et temps de réponse d'une application ;
- redémarrages d'une Container App ;
- CPU et stockage d'une base ;
- messages en lettres mortes d'une file ;
- disponibilité d'un Key Vault ;
- jetons refusés pour quota d'un déploiement de modèle.

Un composant active les alertes recommandées en un réglage, avec un groupe d'actions par environnement.
Elles deviennent des ressources **implicites**, visibles et dont on peut ajuster les seuils.

## 5. Contrôle de dérive ([DEC-74](03-decisions.md))

**RG-GOV-07 — Pipeline de dérive.** Si le projet l'active, chaque destination d'infrastructure reçoit un
pipeline planifié (chaque jour, heure réglable) qui exécute, pour chaque composant et chaque cible,
l'aperçu (what-if, plan) de la **dernière révision déployée**. Il publie un résumé structuré : aucune
différence, ou liste des ressources et propriétés qui diffèrent.

**RG-GOV-08 — Lecture par IFS.** Le suivi des déploiements ([28](28-suivi-des-deploiements.md)) lit ce
résumé.
- L'écran affiche « dérive détectée » par composant et par cible, avec le détail.
- Une notification part vers les membres qui ont `publier`.

IFS ne corrige rien : l'utilisateur redéploie (la dérive disparaît) ou reporte le changement dans le modèle.

**RG-GOV-09 — Bruit.** Les propriétés qu'Azure modifie de lui-même, ou que l'aperçu signale à tort, sont
listées dans le descripteur et ignorées du résumé, avec une mention dans le détail.
