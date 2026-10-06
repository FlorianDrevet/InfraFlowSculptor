/* Généré depuis docs/design/strata/tokens.json — ne pas modifier. */
export const COLOR_TOKENS = [
  {
    "name": "bg",
    "value": "#0b0e13",
    "usage": "Fond de l'application, derrière tout le reste."
  },
  {
    "name": "surface-sidebar",
    "value": "#0e1218",
    "usage": "Barre latérale, champs de saisie, blocs d'explication dans un panneau, zone du graphe."
  },
  {
    "name": "surface-1",
    "value": "#11151c",
    "usage": "Panneaux, cartes, tableaux. La surface par défaut d'un bloc de contenu."
  },
  {
    "name": "surface-2",
    "value": "#171c25",
    "usage": "Surface surélevée : ligne sélectionnée, survol."
  },
  {
    "name": "surface-3",
    "value": "#1e2430",
    "usage": "Élément actif dans un contrôle segmenté, tuile d'initiales, puce neutre forte."
  },
  {
    "name": "surface-active",
    "value": "#1a2029",
    "usage": "Élément de navigation actif, bouton secondaire."
  },
  {
    "name": "line",
    "value": "#252c38",
    "usage": "Bordure standard des panneaux et séparateurs de premier niveau."
  },
  {
    "name": "line-subtle",
    "value": "#1e2430",
    "usage": "Séparateurs entre lignes d'un tableau ou d'une liste."
  },
  {
    "name": "line-strong",
    "value": "#343d4c",
    "usage": "Bordure des champs de saisie et des contrôles (contraste 3:1 sur surface-1)."
  },
  {
    "name": "text",
    "value": "#e8ecf2",
    "usage": "Texte principal sur bg et toutes les surfaces."
  },
  {
    "name": "text-2",
    "value": "#a7b0bf",
    "usage": "Texte secondaire, libellés, navigation inactive."
  },
  {
    "name": "text-3",
    "value": "#7d8797",
    "usage": "Légendes, métadonnées, surtitres. 4.9:1 sur surface-1 : ne pas descendre plus bas."
  },
  {
    "name": "signal",
    "value": "#5bc8df",
    "usage": "LA couleur d'action : bouton principal, onglet actif, focus, élément sélectionné. Une action principale par écran."
  },
  {
    "name": "signal-ink",
    "value": "#062228",
    "usage": "Texte et icônes posés sur un fond signal."
  },
  {
    "name": "signal-text",
    "value": "#7fd6e8",
    "usage": "Liens et texte d'accent sur surfaces sombres."
  },
  {
    "name": "signal-soft",
    "value": "rgba(91,200,223,0.12)",
    "usage": "Fond de puce « en cours », « en attente d'approbation » ou d'information."
  },
  {
    "name": "ember",
    "value": "#e8a65a",
    "usage": "Ce qui a changé ou demande de l'attention : modifié depuis la dernière révision, surcharge d'environnement, avertissement, état partiellement appliqué, demande d'accès."
  },
  {
    "name": "ember-text",
    "value": "#f0b676",
    "usage": "Texte ambre sur surfaces sombres."
  },
  {
    "name": "ember-soft",
    "value": "rgba(232,166,90,0.12)",
    "usage": "Fond de puce « modifiée », de cellule surchargée et de bannière d'avertissement."
  },
  {
    "name": "success",
    "value": "#5cc08a",
    "usage": "État réussi : déployée, publiée, fait, vérifié."
  },
  {
    "name": "success-text",
    "value": "#7ed3a3",
    "usage": "Texte de succès sur surfaces sombres."
  },
  {
    "name": "success-soft",
    "value": "rgba(92,192,138,0.12)",
    "usage": "Fond de puce de succès."
  },
  {
    "name": "danger",
    "value": "#e5695f",
    "usage": "Échec ou action destructrice. Jamais décoratif."
  },
  {
    "name": "danger-text",
    "value": "#f08a80",
    "usage": "Texte d'erreur sur surfaces sombres."
  },
  {
    "name": "danger-soft",
    "value": "rgba(229,105,95,0.12)",
    "usage": "Fond de puce ou de ligne en échec."
  },
  {
    "name": "cat-compute",
    "value": "#9dbbf5",
    "usage": "Catégorie Calcul (App Service, Functions, Container Apps) : repli quand l'icône Azure n'est pas affichée, légende du graphe."
  },
  {
    "name": "cat-data",
    "value": "#c6b2f5",
    "usage": "Catégorie Données (SQL, PostgreSQL, Cosmos DB, stockage, Azure Managed Redis)."
  },
  {
    "name": "cat-messaging",
    "value": "#f2a7c6",
    "usage": "Catégorie Messagerie (Service Bus, Event Hubs)."
  },
  {
    "name": "cat-security",
    "value": "#8adbbb",
    "usage": "Catégorie Sécurité (Key Vault, identités managées)."
  },
  {
    "name": "cat-observability",
    "value": "#e2d18a",
    "usage": "Catégorie Observabilité (Log Analytics, Application Insights)."
  },
  {
    "name": "cat-network",
    "value": "#a9cad7",
    "usage": "Catégorie Réseau (VNet, subnet, Private Endpoint)."
  },
  {
    "name": "cat-platform",
    "value": "#d7b898",
    "usage": "Catégorie Plateforme (registre de conteneurs, App Configuration, groupe de ressources)."
  },
  {
    "name": "cat-ai",
    "value": "#b8e294",
    "usage": "Catégorie IA (Microsoft Foundry, AI Search, Document Intelligence)."
  },
  {
    "name": "edge-dependency",
    "value": "#5b6576",
    "usage": "Graphe : liaison d'hébergement ou de valeur (trait plein si explicite, pointillé si implicite)."
  },
  {
    "name": "edge-identity",
    "value": "#b59af2",
    "usage": "Graphe : identité managée attachée. Bordure des puces d'identité et de dérive."
  },
  {
    "name": "identity-text",
    "value": "#c9b6f6",
    "usage": "Texte des puces d'identité, de rôle, de référence Key Vault et de dérive, sur surfaces sombres."
  },
  {
    "name": "identity-soft",
    "value": "rgba(181,154,242,0.14)",
    "usage": "Fond des puces d'identité, de rôle, de référence Key Vault et de l'état « dérive »."
  },
  {
    "name": "overlay-scrim",
    "value": "rgba(5,7,10,0.92)",
    "usage": "Fond derrière un dialogue."
  }
] as const;

export const TYPE_GROUPS = [
  {
    "name": "Interface",
    "family": "sans",
    "styles": [
      {
        "name": "display",
        "fontSize": "56px",
        "lineHeight": "60px",
        "fontWeight": 600,
        "letterSpacing": "-0.02em",
        "sample": "Strata",
        "usage": "Couverture et écrans vides marquants. Jamais dans l'application courante."
      },
      {
        "name": "page-title",
        "fontSize": "28px",
        "lineHeight": "34px",
        "fontWeight": 600,
        "letterSpacing": "-0.01em",
        "sample": "Boutique en ligne",
        "usage": "Titre de page (h1)."
      },
      {
        "name": "dialog-title",
        "fontSize": "18px",
        "lineHeight": "24px",
        "fontWeight": 600,
        "sample": "Publier la révision 16",
        "usage": "Titre de dialogue."
      },
      {
        "name": "section-title",
        "fontSize": "16px",
        "lineHeight": "22px",
        "fontWeight": 600,
        "sample": "Liaisons sortantes",
        "usage": "Titre de section ou de panneau (h2)."
      },
      {
        "name": "body",
        "fontSize": "14px",
        "lineHeight": "20px",
        "fontWeight": 400,
        "sample": "Le déploiement d'infrastructure écrit tous les paramètres ; le pipeline applicatif ne livre que l'image.",
        "usage": "Texte courant."
      },
      {
        "name": "body-dense",
        "fontSize": "13px",
        "lineHeight": "18px",
        "fontWeight": 400,
        "sample": "api · 3 surcharges en prd",
        "usage": "Tableaux, listes, panneaux latéraux, boutons."
      },
      {
        "name": "caption",
        "fontSize": "12px",
        "lineHeight": "16px",
        "fontWeight": 500,
        "sample": "Lu dans Azure DevOps il y a 1 min",
        "usage": "Légendes, métadonnées, libellés de champ."
      }
    ]
  },
  {
    "name": "Code",
    "family": "mono",
    "styles": [
      {
        "name": "mono",
        "fontSize": "12px",
        "lineHeight": "18px",
        "fontWeight": 400,
        "sample": "ca-shop-api-prd",
        "usage": "Noms Azure générés, codes, dépôts, chemins, identifiants, extraits de code."
      },
      {
        "name": "mono-title",
        "fontSize": "24px",
        "lineHeight": "32px",
        "fontWeight": 500,
        "sample": "orders",
        "usage": "Titre d'une ressource ou d'un composant (son nom logique est un identifiant)."
      },
      {
        "name": "overline",
        "fontSize": "11px",
        "lineHeight": "16px",
        "fontWeight": 500,
        "letterSpacing": "0.08em",
        "sample": "ÉTAPE 2 SUR 5",
        "usage": "Surtitres et en-têtes de colonne, toujours en capitales."
      }
    ]
  }
] as const;

export const FONT_FAMILIES = {
  "sans": "\"Instrument Sans\", system-ui, -apple-system, \"Segoe UI\", sans-serif",
  "mono": "\"JetBrains Mono\", ui-monospace, \"Cascadia Mono\", Consolas, monospace"
} as const;

export const SPACING_TOKENS = [
  {
    "name": "space-1",
    "value": "4px",
    "usage": "Écart icône-texte serré, padding de puce vertical."
  },
  {
    "name": "space-2",
    "value": "8px",
    "usage": "Écart entre boutons, entre puces."
  },
  {
    "name": "space-3",
    "value": "12px",
    "usage": "Écart dans une ligne de ressource, padding de champ."
  },
  {
    "name": "space-4",
    "value": "16px",
    "usage": "Padding de panneau, écart entre panneaux d'une colonne."
  },
  {
    "name": "space-6",
    "value": "24px",
    "usage": "Écart entre colonnes et grandes sections."
  },
  {
    "name": "space-8",
    "value": "32px",
    "usage": "Marges latérales du contenu de page."
  },
  {
    "name": "space-12",
    "value": "48px",
    "usage": "Respiration de bas de page, écrans d'assistant."
  }
] as const;

export const RADIUS_TOKENS = [
  {
    "name": "radius-sm",
    "value": "4px",
    "usage": "Raccourcis clavier, petites étiquettes."
  },
  {
    "name": "radius-md",
    "value": "6px",
    "usage": "Boutons, champs, tuiles d'icône, éléments de navigation."
  },
  {
    "name": "radius-lg",
    "value": "10px",
    "usage": "Panneaux et cartes."
  },
  {
    "name": "radius-xl",
    "value": "12px",
    "usage": "Dialogues, bandes de groupe de ressources dans le graphe."
  },
  {
    "name": "radius-pill",
    "value": "999px",
    "usage": "Puces de statut, filtres, pastilles."
  }
] as const;

export const SHADOW_TOKENS = [
  {
    "name": "shadow-overlay",
    "value": "0 24px 48px rgba(0,0,0,0.45)",
    "usage": "Dialogues, menus et popovers."
  }
] as const;

export const SIZE_TOKENS = [
  {
    "name": "control-height",
    "value": "36px",
    "usage": "Boutons et champs standard."
  },
  {
    "name": "control-height-sm",
    "value": "28px",
    "usage": "Boutons compacts, segments, filtres."
  },
  {
    "name": "sidebar-width",
    "value": "236px",
    "usage": "Barre latérale de l'application."
  },
  {
    "name": "topbar-height",
    "value": "56px",
    "usage": "Barre supérieure (fil d'Ariane + actions de page)."
  },
  {
    "name": "content-max",
    "value": "1280px",
    "usage": "Largeur maximale du contenu d'une page."
  }
] as const;
