# 20 — Validation

## 1. Objectif

Signaler tout ce qui empêcherait un déploiement de réussir ou le rendrait dangereux, **avant** la
génération, avec le même résultat pour l'écran, l'API et le MCP ([DEC-19](03-decisions.md)).

## 2. Deux niveaux de contrôle

| Niveau | Quand | Effet |
|---|---|---|
| **Contrôle à la saisie** | À chaque commande (création, modification). | La commande est refusée avec un message par champ. Couvre tout ce qui est vérifiable sur l'objet seul : formats, valeurs du descripteur, unicités locales, cohérence immédiate. |
| **Validation du modèle** | Après chaque modification (résultat recalculé et mis en cache par version du modèle), et obligatoirement avant toute génération. | Produit des **constats**. Couvre les règles qui dépendent de plusieurs objets ou de l'état global. |

Une règle ne vit qu'à un endroit : ce qui peut être refusé à la saisie l'est, et n'apparaît pas comme
constat.

## 3. Constat

| Champ | Contenu |
|---|---|
| Code | `VAL-<DOMAINE>-<NOM>`, stable. |
| Gravité | `Erreur` (bloque la génération), `Erreur à la publication` (n'empêche pas de générer ni de télécharger, refuse la publication de la destination concernée), `Avertissement`, `Info`. |
| Objet | Projet, environnement, composant, groupe de ressources, ressource, liaison ou paramètre, avec lien direct. |
| Environnement | Environnement concerné, s'il y en a un. |
| Message | Phrase complète, sans jargon interne, qui dit ce qui ne va pas et pourquoi. |
| Correction | Action proposée ; si elle est sûre, applicable en un clic (« corriger »). |

**RG-VAL-01 — Bloquant.** Une génération est refusée tant qu'il reste au moins une `Erreur`. Le refus
liste les erreurs. Une `Erreur à la publication` laisse générer et télécharger, ce qui permet le mode
découverte sans connexion git ([DEC-78](03-decisions.md)).

**RG-VAL-02 — Pas de masquage d'erreur.** Une `Erreur` ne peut pas être ignorée. Un `Avertissement` peut
être **acquitté** par une personne qui a `modele.modifier` sur l'objet, avec un commentaire obligatoire ; l'acquittement est journalisé
et tombe si l'objet change.

**RG-VAL-03 — Performance.** La validation complète d'un projet de 300 ressources répond en moins de
2 secondes ([EXG-07](27-exigences-non-fonctionnelles.md)).

**RG-VAL-04 — Niveaux de préparation.** L'écran du projet distingue quatre états, du plus faible au plus
fort : **modèle valide** (aucune `Erreur`) ; **révision générable** (idem, sortie contrôlée) ;
**publiable** (aucune `Erreur à la publication` pour la destination) ; **déployable** (liste de contrôle
du kit complète pour la cible). Aucun état n'est présenté comme une vérification d'Azure : quotas,
politiques Azure du client et disponibilité réelle ne sont connus qu'au déploiement, sauf mention
« vérifié » d'un constat qui a lu Azure.

## 4. Catalogue des règles

### Projet, environnements, composants

| Code | Gravité | Condition |
|---|---|---|
| `VAL-ENV-AUCUN` | Erreur | Le projet n'a aucun environnement. |
| `VAL-ENV-APPROBATEURS` | Erreur | Cible protégée sans approbateur. |
| `VAL-CMP-SANS-ENVIRONNEMENT` | Erreur | Composant `PerEnvironment` qui ne cible aucun environnement. |
| `VAL-CMP-VIDE` | Info | Composant sans ressource. |
| `VAL-CMP-CYCLE` | Erreur | Cycle de dépendances de création entre composants ; le constat cite les liaisons et l'alternative ([RG-CMP-06](12-composants-et-groupes-de-ressources.md)). |
| `VAL-CMP-SENS` | Erreur | Liaison d'une ressource `Single` vers une ressource `PerEnvironment`. |
| `VAL-CMP-ENVIRONNEMENT` | Erreur | Liaison vers un composant qui ne cible pas un environnement où la source est présente. |
| `VAL-PRJ-TAGS` | Erreur | Plus de 50 tags effectifs sur une ressource. |
| `VAL-PRJ-TAGS-SYSTEME` | Avertissement | Tags système désactivés. |

### Nommage

| Code | Gravité | Condition |
|---|---|---|
| `VAL-NOM-LONGUEUR` | Erreur | Nom assaini hors des longueurs du type. |
| `VAL-NOM-VIDE` | Erreur | Nom vide après assainissement. |
| `VAL-NOM-COLLISION` | Erreur | Deux ressources produisent le même nom dans la même portée d'unicité. |
| `VAL-NOM-DISPONIBILITE` | Info (lot 1), Avertissement (lot 2, API Azure) | Nom de portée globale qui semble déjà utilisé dans Azure. |

### Ressources et catalogue

| Code | Gravité | Condition |
|---|---|---|
| `VAL-RES-REQUIS` | Erreur | Propriété obligatoire sans valeur effective dans un environnement de présence. |
| `VAL-RES-CONTRAINTE` | Erreur | Contrainte croisée du descripteur violée pour une valeur effective (exemple : CPU/mémoire). |
| `VAL-RES-PRESENCE` | Erreur | Ressource présente liée à une cible absente du même environnement. |
| `VAL-RES-EXISTANTE-ID` | Erreur | Ressource existante présente sans identifiant Azure pour cet environnement. |
| `VAL-RES-ABSENTE-PARTOUT` | Avertissement | Ressource absente de tous les environnements. |
| `VAL-CAT-DEPRECIE` | Avertissement, puis Erreur après la date de refus | Valeur dépréciée par le catalogue. |
| `VAL-CAT-REGION` | Erreur | Type ou valeur indisponible dans la région effective. |
| `VAL-GEN-CONTRAT` | Erreur | Module du client dont le contrat ne couvre pas une propriété du descripteur *(lot 3)*. |
| `VAL-GEN-LANGAGE` | Erreur | Type, propriété ou valeur non pris en charge par l'émetteur du langage du projet (apparaît surtout après un changement de langage, [DEC-48](03-decisions.md)). |
| `VAL-CAT-MISE-A-JOUR` | Info | Une version plus récente du catalogue est disponible ; le constat liste les fichiers et valeurs qu'elle changerait ([DEC-92](03-decisions.md)). |
| `VAL-CAT-FIN-SUPPORT` | Avertissement 90 jours avant, puis Erreur | La version du catalogue du projet arrive en fin de support ; la montée est nécessaire pour générer ([DEC-92](03-decisions.md)). |

### Sécurité

| Code | Gravité | Condition |
|---|---|---|
| `VAL-SEC-AUTH-LOCALE` | Avertissement (acquittable) | Authentification locale activée : mot de passe, compte admin, clé ([DEC-51](03-decisions.md)). Le constat propose l'alternative Entra. |
| `VAL-SEC-SECRET-ETAT` | Info | Terraform : un mot de passe passe par un attribut classique, faute d'attribut en écriture seule ; il sera dans l'état protégé. |
| `VAL-SEC-KEYVAULT-ABSENT` | Erreur | Authentification locale activée sans Key Vault de stockage désigné ([RG-PAR-16](17-parametres-applicatifs-et-secrets.md)). |
| `VAL-SEC-GENERE-EXISTANT` | Erreur | Mot de passe `Généré` destiné à un Key Vault existant ([DEC-105](03-decisions.md)). |
| `VAL-SEC-ORDRE` | Erreur | Key Vault alimenté par la release (secret de pipeline, mot de passe généré) qui n'est pas déployé avant un consommateur de ce secret ([RG-PAR-22](17-parametres-applicatifs-et-secrets.md)). |

### Liaisons, accès, paramètres

| Code | Gravité | Condition |
|---|---|---|
| `VAL-LIA-OBLIGATOIRE` | Erreur | Liaison obligatoire absente (plan, environnement Container Apps, serveur, stockage hôte, registre en mode Container). |
| `VAL-LIA-PLACEMENT` | Erreur | Source et cible dans des abonnements ou régions incompatibles. |
| `VAL-LIA-IDENTITE-PARTAGEE` | Erreur | Identité affectée utilisée par une ressource d'un autre composant ([RG-LIA-12](16-liaisons-identites-et-acces.md)). |
| `VAL-LIA-PORTEE-EXTERNE` | Info | Attribution à la portée d'une ressource existante ou d'un autre abonnement : droits à prévoir pour l'identité de déploiement. |
| `VAL-LIA-GROUPE-ABSENT` | Info | Groupe Entra sans identifiant pour un environnement : attributions non générées là. |
| `VAL-LIA-SQL-ADMIN` | Avertissement | Accès aux données défini alors que l'adhésion de l'identité de déploiement au groupe administrateur n'est pas confirmée dans la liste de contrôle. |
| `VAL-PAR-SECRET-SUSPECT` | Avertissement | Valeur littérale dont le nom ressemble à un secret. |
| `VAL-PAR-SECRET-CONFLIT` | Erreur | Même secret alimenté de deux façons. |
| `VAL-PAR-SECRET-PROPRIETAIRE` | Erreur | Deux composants écrivent le même secret physique (même coffre résolu, même nom) dans une cible ([DEC-105](03-decisions.md)). |
| `VAL-PAR-HORS-IFS` | Info | Secret « géré hors IFS » : il doit exister avant le déploiement. |

### Applications

| Code | Gravité | Condition |
|---|---|---|
| `VAL-APP-CODE-SOURCE` | Erreur à la publication | Application sans destination de code dans le plan de publication. La génération utilise le chemin par défaut. |
| `VAL-APP-PILE-PLAN` | Erreur | Pile ou mode incompatible avec le plan (exemple : Python sur Windows, conteneur sur `FC1`). |
| `VAL-APP-ETAPE` | Erreur | Étape du catalogue activée sans valeur par défaut pour la pile ni commande saisie. |
| `VAL-APP-STRATEGIE` | Erreur | Stratégie de déploiement impossible sur la ressource (exemple : slots sur un plan Basic ou Flex). |
| `VAL-APP-EXTENSION` | Erreur à la publication | Modèle d'étapes du client introuvable dans le dépôt de code ([RG-APP-14](19-applications-build-et-deploiement.md)). |

### Réseau *(lot 2 sauf mention)*

| Code | Gravité | Condition |
|---|---|---|
| `VAL-NET-IP-VIDE` (lot 1) | Erreur | Exposition restreinte sans plage IP. |
| `VAL-NET-CIDR-HORS` | Erreur | Subnet hors des espaces d'adressage. |
| `VAL-NET-CIDR-CHEVAUCHE` | Erreur | Subnets qui se chevauchent. |
| `VAL-NET-VNET-CHEVAUCHE` | Erreur si les VNets sont appairés (directement ou par le même hub), Avertissement sinon | VNets d'un même environnement qui se chevauchent. |
| `VAL-NET-APPAIRAGE-ENV` | Erreur | Appairage entre VNets d'environnements différents, hors hub `Single`. |
| `VAL-NET-AGENT-CYCLE` | Erreur | Pool d'exécuteurs privé utilisé par le composant qui le déploie, ou par un composant déployé avant lui. |
| `VAL-NET-SUBNET-TAILLE` | Erreur | Subnet trop petit pour son usage. |
| `VAL-NET-DNS` | Erreur | Ressource privée sans stratégie DNS pour l'environnement. |
| `VAL-NET-CONSOMMATEUR` | Erreur | Application qui consomme une ressource privée sans intégration sortante adaptée. |
| `VAL-NET-AGENT` | Erreur | La release doit joindre une ressource privée avec des exécuteurs qui n'atteignent pas le réseau. |

### IA *(lot 2)*

| Code | Gravité | Condition |
|---|---|---|
| `VAL-IA-QUOTA` | Erreur (avec connexion Azure), Info sinon | Capacité des déploiements de modèles supérieure au quota de l'abonnement, ou quota non vérifiable. |
| `VAL-IA-COSMOS` | Erreur | Cosmos DB des agents en configuration standard sous 3 000 RU/s de débit total. |
| `VAL-IA-CONFIG-STANDARD` | Erreur | Projet en configuration standard sans ses trois connexions (stockage, AI Search, Cosmos DB). |

### Politiques d'organisation *(lot 2)*

| Code | Gravité | Condition |
|---|---|---|
| `VAL-POL-REGLE` | Celle de la politique | Une politique d'organisation n'est pas respectée ; le constat nomme la politique et la règle ([33 § 2](33-gouvernance-couts-et-supervision.md)). |

### Publication

| Code | Gravité | Condition |
|---|---|---|
| `VAL-PIP-COMPATIBILITE` | Erreur | Dépôt du plan de publication incompatible avec la plateforme CI ([RG-PUB-19](24-depots-et-publication.md)). |
| `VAL-PIP-APPROBATEURS` | Erreur | Approbateur qui n'est pas une identité valide de la plateforme CI du projet (exemple : après un changement de plateforme). |
| `VAL-PUB-DESTINATION` | Erreur à la publication | Composant ou application sans destination. |
| `VAL-PUB-CONNEXION` | Avertissement sous 15 jours, Erreur une fois expirée | Jeton git de repli proche de l'expiration ou expiré. |
| `VAL-PUB-CONNEXION-DEPLOIEMENT` | Erreur | Azure DevOps : cible sans nom de service connection. |

## 5. Corrections en un clic

| Constat | Correction proposée |
|---|---|
| `VAL-NOM-LONGUEUR` | Ouvrir l'éditeur de gabarit du type, avec l'aperçu filtré sur les noms trop longs. |
| `VAL-LIA-OBLIGATOIRE` | Créer la liaison vers la seule cible possible, s'il n'y en a qu'une. |
| `VAL-PAR-SECRET-SUSPECT` | Convertir en secret Key Vault alimenté par pipeline. |
| `VAL-RES-PRESENCE` | Rendre la cible présente dans l'environnement, ou la source absente. |
| `VAL-NET-IP-VIDE` | Revenir à l'exposition publique. |
