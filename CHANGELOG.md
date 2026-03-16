# Changelog

```
  ╔══════════════════════════════════════════════════╗
  ║                                                  ║
  ║         ♬⋆.˚  BetterTrumpet  v3.0.0  ˚.⋆♬      ║
  ║                                                  ║
  ║         59 fichiers · +8000 lignes               ║
  ║         14 nouvelles features                    ║
  ║                                                  ║
  ╚══════════════════════════════════════════════════╝
```

### Fonctionnalités

```
  ┌─────────────────────────────────────────────────┐
  │  🎓  ONBOARDING PREMIUM                        │
  │                                                 │
  │  ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐      │
  │  │  1  │→│  2  │→│  3  │→│  4  │→│  5  │      │
  │  │Bien-│ │Audio│ │Appa-│ │Confi│ │Prêt!│      │
  │  │venue│ │     │ │rence│ │dent.│ │ 🎉  │      │
  │  └─────┘ └─────┘ └─────┘ └─────┘ └─────┘      │
  │  ████████████████████████░░░░░░░  80%           │
  └─────────────────────────────────────────────────┘
```
Assistant de configuration en 5 pages avec prévisualisation audio en temps réel, choix de périphérique, personnalisation du thème. Animations décalées, barres de progression, confettis sur la dernière page.

```
  ┌─────────────────────────────────────────────────┐
  │  ↩️  UNDO / REDO                                │
  │                                                 │
  │   Ctrl+Z  ◄────────────►  Ctrl+Y               │
  │                                                 │
  │   volume ✓  mute ✓  profils ✓                   │
  └─────────────────────────────────────────────────┘
```
Système undo/redo complet — Ctrl+Z / Ctrl+Y sur tous les changements de volume, mute et profils.

```
  ┌─────────────────────────────────────────────────┐
  │  📌  PIN FLYOUT           ⌨️  SWITCH RAPIDE     │
  │                                                 │
  │  Ctrl+P → le flyout       Raccourci pour        │
  │  reste ouvert en          cycler entre les      │
  │  permanence               périphériques         │
  └─────────────────────────────────────────────────┘
```

```
  ┌─────────────────────────────────────────────────┐
  │  🎚️  PROFILS VOLUME                             │
  │                                                 │
  │   ┌──────────┐  Capturer / Restaurer            │
  │   │ Gaming   │  Export .btprofile                │
  │   │ Music    │  Import .btprofile                │
  │   │ Meeting  │                                   │
  │   └──────────┘                                   │
  └─────────────────────────────────────────────────┘
```
Sauvegarder et restaurer l'état audio complet de toutes les apps. Export/import en fichier .btprofile.

```
  ┌─────────────────────────────────────────────────┐
  │  🎨  MOTEUR DE THÈMES                           │
  │                                                 │
  │  7 canaux :  slider │ track │ peak │ fond       │
  │              texte  │ accent│ glow              │
  │                                                 │
  │  ▓▓▓▓░░░░  ▓▓▓▓▓░░  ▓▓░░░░░  ▓▓▓▓▓▓░          │
  │  Neon       Pastel   Dark      Nature           │
  └─────────────────────────────────────────────────┘
```
Refonte complète — 7 canaux de couleur, thèmes sauvegardés, grille visuelle, mode album art dynamique.

```
  ┌─────────────────────────────────────────────────┐
  │  🎵  MEDIA POPUP                                │
  │                                                 │
  │   ┌───────────────────────┐                     │
  │   │ 🎵  Titre             │  Survol de          │
  │   │    Artiste            │  l'icône tray →     │
  │   │ ▓▓▓▓▓▓▓▓░░░ 2:34     │  lecteur flottant   │
  │   │ ◄◄  ▶  ►► 🔀 🔁      │                     │
  │   └───────────────────────┘                     │
  └─────────────────────────────────────────────────┘
```
Album art, seek bar, contrôles shuffle/repeat. Slider volume avec gradient qui reprend la couleur dominante de la pochette.

```
  ┌─────────────────────────────────────────────────┐
  │  🔄  NOTIFICATIONS DE MISE À JOUR               │
  │                                                 │
  │  GitHub API → badge tray (●) → bannière flyout  │
  │  Canaux : Stable · Beta · All                   │
  └─────────────────────────────────────────────────┘
```

```
  ┌─────────────────────────────────────────────────┐
  │  📋  QUOI DE NEUF         📦  EXPORT / IMPORT   │
  │                                                 │
  │  Fenêtre catégorisée      .btsettings (JSON)    │
  │  après mise à jour,       Tous les réglages     │
  │  shimmer sur le titre     en un fichier         │
  └─────────────────────────────────────────────────┘
```

```
  ┌─────────────────────────────────────────────────┐
  │  🎞️  ANIMATIONS           🌿  MODE ÉCO         │
  │                                                 │
  │  Vitesse et FPS           Réduit CPU/GPU        │
  │  configurables            sur batterie          │
  └─────────────────────────────────────────────────┘
```

### Refonte UX

Refonte de la page Apparence — Visual First avec onglets, preview hero, grille de thèmes.
Refonte UX des paramètres — rythme, aération, cohérence.
Refonte de la page À propos — carte télémétrie, résumé santé.

### Technique

```
  ┌─────────────────────────────────────────────────┐
  │  ⚡  CLI · 19 COMMANDES                         │
  │                                                 │
  │  $ bt volume set 75                             │
  │  $ bt device list                               │
  │  $ bt profile load Gaming                       │
  │  $ bt update check                              │
  │  $ bt settings export backup.btsettings         │
  │                                                 │
  │  Named pipe IPC · JSON responses                │
  └─────────────────────────────────────────────────┘
```

```
  ┌─────────────────────────────────────────────────┐
  │  🛡️  CRASH PROTECTION                           │
  │                                                 │
  │  Sentry GDPR ──► opt-in toggle en temps réel    │
  │  Health ──► mémoire, handles GDI/User, uptime   │
  │  Logging ──► 5 fichiers × 5 Mo, rotation auto   │
  └─────────────────────────────────────────────────┘
```

```
  ┌─────────────────────────────────────────────────┐
  │  🚀  DÉMARRAGE 3 PHASES                         │
  │                                                 │
  │  Phase 1: Core    ──► fatal si échec            │
  │  Phase 2: UI      ──► isolé, tray + flyout      │
  │  Phase 3: Features──► isolé, updates + CLI      │
  └─────────────────────────────────────────────────┘
```
Scaffolding palette de commandes (pour futur plugin Raycast). Mode portable (settings JSON, zéro registre).

---

## 2.3.0.0
- Ajout d'un réglage pour activer/désactiver le changement de volume avec la molette n'importe où (merci @Tester798 !)
- Ajout d'un réglage pour activer/désactiver le changement de volume avec la molette au survol de l'icône EarTrumpet (merci @Tester798 !)
- Ajout d'une nouvelle zone de réglages communautaires
- Ajout d'un réglage communautaire pour activer/désactiver l'échelle de volume logarithmique (merci @yonatan-mitmit !)
- Ajout de raccourcis legacy dans le menu contextuel vers [Préférences volume et périphériques] / [Mélangeur de volume]
- Ajout de la possibilité d'utiliser la touche Windows dans les raccourcis (merci @iamevn !)
- Ajout du tri linguistique des noms de périphériques audio (merci @Tester798 !)
- Ajout d'un contournement pour Windows Search (CortanaUI) affichant une icône par défaut (X)
- Correction d'un problème où l'installation d'EarTrumpet via AppInstaller échouait si les libs Visual C++ n'étaient pas installées
- Correction d'un problème où les infobulles EarTrumpet ne se mettaient pas à jour en temps réel lors du défilement à la molette sur Windows 10 (merci @krlvm !)
- Forçage du rendu logiciel uniquement pour éviter les GPU énergivores
- Amélioration de l'animation du flyout (merci @krlvm !)

## 2.2.2.0
- Correction d'un problème de changement de volume lors du défilement dans certains scénarios (ex : réalité virtuelle)
- Mise à jour des traductions japonaises
- Nettoyage des anciennes ressources de langue

## 2.2.1.0
- Correction du comportement du menu contextuel tactile sur Windows 11 avec le futur « ShyTaskbar » activé
- Correction de l'apparence du flyout sur Windows 10 avec le mode clair activé (merci @xmha97)
- Correction de l'animation du flyout ne respectant pas les bons réglages système sur Windows 10 et Windows 11
- Mise à jour de la bibliothèque de lecture GIF pour réduire l'utilisation mémoire (merci @rocksdanister)
- Réduction de l'utilisation d'un contournement pour les ralentissements Acrylic sur la plupart des builds de Windows (merci @krlvm)
- Mise à jour des traductions par les contributeurs Crowdin
- Mise à jour des métadonnées de la page produit Microsoft Store pour corriger les problèmes de localisation

## 2.2.0.0
- Ajout de raccourcis clavier pour contrôler le volume de plusieurs périphériques à la fois (merci @Taknok !)
- Ajustement du stockage des données de l'application pour améliorer la fiabilité
- Correction d'un crash qui pouvait survenir en activant/désactivant l'Acrylic du flyout dans certains scénarios
- Correction d'un problème avec le flyout étendu devenant trop grand sur Windows 11
- Mise à jour des traductions japonaises, grecques, croates et russes (merci aux contributeurs Crowdin !)

## 2.1.10.0
- Le flyout se souvient maintenant s'il était étendu/réduit entre les lancements (merci @Tester798 !)
- Correction d'un problème avec l'animation d'ouverture du flyout se comportant de manière erratique avec certaines souris
- Correction d'un problème où des périphériques sans certaines caractéristiques interféraient avec le mute et d'autres actions
- Correction d'un problème avec les noms de périphériques ne s'affichant pas correctement s'ils contenaient des underscores
- Correction d'un problème avec le flyout s'ouvrant en dehors de la zone de travail dans des cas supplémentaires
- Ajustement de la position du flyout par rapport au bord de l'écran pour correspondre au look Windows 11
- Suppression du fond de couleur unie derrière les icônes d'application
- Mise à jour des traductions finnois, allemand et autres

## 2.1.9.0
- Ajout du support basique de Windows 11
- Ajout/mise à jour des traductions italien, hongrois, espagnol, portugais, turc, chinois, norvégien, arabe, tchèque, polonais, suédois, roumain et russe
- Correction d'un problème avec le flyout s'ouvrant en dehors de la zone de travail
- Correction d'un problème avec le déplacement lent des fenêtres lors du glisser

## 2.1.8.0
- Ajout des traductions hongrois, suédois, coréen et tamoul
- Mise à jour des traductions japonaises
- Ajout d'une icône fluent
- Correction d'un problème avec les traductions tchèques et afrikaans manquantes
- Correction d'un problème avec les icônes n'apparaissant pas pour les applications de bureau packagées (ex : Microsoft Flight Simulator)
- Correction d'un problème avec l'élément de menu [Windows Legacy > Paramètres de son] ouvrant le mauvais panneau
- Correction d'un problème de gestion des icônes de la zone de notification
- Correction d'un problème empêchant l'utilisation des touches système dans les raccourcis clavier

## 2.1.7.0
- Correction d'un crash lors de la récupération des infos de région pour la conformité RGPD sur certaines machines

## 2.1.6.0
- Ajout de la possibilité d'activer/désactiver le rapport de crash
- Ajout de traductions manquantes
- Ajout d'un lien vers la politique de confidentialité
- Correction d'un problème d'affichage d'icône avec les apps basées sur libmpv (ex : Plex)
- Correction d'un problème rendant les sliders de volume difficiles à manipuler à la souris en haute résolution
- Restauration du comportement de l'icône tray pré-2.1.2.0 en attendant de résoudre les problèmes de duplication d'icônes

## 2.1.5.0
- Correction d'un problème avec l'icône « line in » n'apparaissant pas
- Correction d'un problème avec les sous-menus contextuels disparaissant de manière inattendue

## 2.1.4.0
- Correction de divers bugs avec la zone de recherche dans les Paramètres
- Correction de la disparition de l'icône tray quand le Shell Windows crash/redémarre
- Correction de l'atténuation du slider de volume principal au mute
- Correction de la couleur de la piste du flyout en thème clair
- Correction de l'icône mute à volume zéro
- Ajout de contraintes de largeur de la fenêtre mélangeur quand un grand nombre de périphériques audio sont présents
- Ajout du support de la langue slovène
- Ajout d'états de survol sur les boutons
- Corrections de crash supplémentaires

## 2.1.3.0
- Correction d'un crash causant la disparition d'EarTrumpet au démarrage
- Correction de diverses fuites potentielles
- Ajout d'un dialogue d'aide en cas de polices cassées empêchant le démarrage

## 2.1.2.0
- Correction d'une fuite de handle d'icône causant un crash
- Correction des raccourcis clavier non correctement désenregistrés
- Correction des touches fléchées changeant le volume du périphérique par défaut
- Correction des couleurs du thème Contraste élevé
- Correction de la fenêtre des paramètres couvrant la barre des tâches en maximisé
- L'icône tray devrait rester en place après les mises à jour désormais
- Les icônes tray et app se mettent correctement à l'échelle
- L'icône tray supporte le défilement sans ouvrir le flyout
- Suppression de métadonnées indésirables de la télémétrie

## 2.1.1.0
- Correction d'un crash lors du parsing de nombres sur les systèmes non-anglais

## 2.1.0.0
- Ajout d'une nouvelle expérience de paramètres
- Ajout du support du mode clair Windows
- Ajout de raccourcis clavier pour ouvrir les fenêtres mélangeur et paramètres
- Réduction de l'encombrement du menu contextuel
- Correction de divers problèmes d'affichage avec les systèmes d'écriture RTL
- Changement du comportement de nommage des apps pour s'aligner avec Windows
- Ajout du texte mute à l'icône de la zone de notification
- Ajout du lien « Ouvrir les paramètres de son » dans le menu contextuel
- Ajout de texte dans l'infobulle de l'icône pour indiquer l'état mute
- Ré-ajout de l'ombre et des bordures de la fenêtre flyout
- Ajout de points de télémétrie supplémentaires
- Suppression de l'arabe, hongrois, coréen, norvégien bokmål, portugais, roumain et turc en attendant la fin de la localisation
- Corrections de bugs supplémentaires

## 2.0.8.0
- Changement du comportement de regroupement pour se baser sur le chemin d'installation vs. le nom de l'exécutable
- Désactivation du flou de la fenêtre flyout quand elle n'est pas visible pour éviter qu'elle apparaisse dans l'alt-tab
- Correction d'un problème où l'onglet Améliorations manquait dans le dialogue des périphériques de lecture
- Correction d'un problème où le flyout était trop grand quand la barre des tâches est en masquage auto
- Correction d'un problème où les périphériques désactivés ou débranchés apparaissaient de manière inattendue
- Correction d'un crash quand aucun endpoint audio par défaut n'était présent
- Correction d'un crash lors du clic droit sur une session audio après l'avoir déplacée

## 2.0.7.0
- Ajout du support supplémentaire des thèmes contraste élevé
- Ajout du support DPI par moniteur
- Ajout d'une limite de tampon de logging de diagnostic interne
- Désactivation d'Alt+Espace sur la fenêtre flyout
- Correction d'un problème de rendu quand le DPI était supérieur à 100% et qu'il y avait plus de périphériques que ce que le flyout pouvait afficher sans scrollbar
- Correction d'un problème de rendu où l'icône de la zone de notification devenait floue au-dessus de 100% de DPI
- Correction de l'icône et du nom des périphériques d'enregistrement en mode « Écoute »
- Correction de la mise à l'échelle de l'icône de la zone de notification
- Correction du flyout de débordement au-dessus de 100% de DPI

## 2.0.6.0
- Correction d'un problème affectant la localisation sur les systèmes non-anglais

## 2.0.5.0
- Correction de l'icône Sons système sur ARM64
- Mise à jour des couleurs Contraste élevé
- Ajout de la collecte d'infos de debug quand l'énumération des périphériques échoue

## 2.0.4.0
- Limitation du défilement à la molette uniquement au-dessus de l'icône de la zone de notification
- Ajout du support de RS3
- Correction d'un bug qui affichait « Il n'y a aucun périphérique de lecture » lors de la suppression du périphérique par défaut
- Correction d'un crash lors de l'ajout/suppression de périphériques
- Suppression des erreurs non-fatales de la télémétrie

## 2.0.3.0
- Correction d'un problème avec certaines apps (ex : Sea of Thieves) ne s'affichant pas correctement
- Ajout de langues supplémentaires (arabe, espagnol, hongrois, coréen, turc et ukrainien)

## 2.0.2.0
- Changements de télémétrie

## 2.0.1.0
- Correction d'un crash lors de l'ajout/suppression rapide d'un périphérique
- Correction d'un crash avec les sessions audio multi-processus
- Correction d'un crash lors du lancement de liens web
- Correction d'un crash à la fermeture des fenêtres EarTrumpet
- Correction d'un crash lors de l'expansion/réduction de la fenêtre principale
- Correction d'un crash quand le masquage auto de la barre des tâches est activé
- Correction d'un crash quand le registre contient des données de personnalisation invalides
- Correction d'un crash lors de l'appel aux APIs de stockage de données de l'application

## 2.0.0.0
- Ajout du clic milieu sur l'icône de la zone de notification pour couper le son
- Ajout de la possibilité d'utiliser la molette pour changer le volume quand la fenêtre est ouverte
- Ajout du peak metering multi-canaux
- Ajout de la possibilité de déplacer les apps entre périphériques
- Ajout de la possibilité de voir plusieurs périphériques
- Ajout de la fenêtre mélangeur de volume
- Amélioration du regroupement de sessions d'apps
- Amélioration de la navigation clavier
- Ajout d'un raccourci clavier pour ouvrir le flyout
- Ajout du support des modes clair/sombre de Windows
- Ajout des liens Sons, Enregistrement, etc. dans le menu contextuel
- Amélioration des animations et des détails
- Corrections supplémentaires pour RTL, accessibilité et apps sans icônes

## 1.5.3.0
- Amélioration des performances des sliders
- Correction de la compatibilité Acrylic Blur pour Windows 10 1803

## 1.5.2.0

## 1.5.1.0

## 1.5.0.0

## 1.4.4.0

## 1.4.3.0

## 1.4.2.0

## 1.4.1.0

## 1.4.0.0

## 1.3.2.0
- Correction du changement de périphériques audio dans Windows 10 (RS1)

## 1.3.1.0
- Correction d'un problème de mise à l'échelle DWM où la fenêtre apparaissait à la mauvaise position
- Correction de problèmes d'interface quand aucun périphérique audio n'est trouvé
- Correction du changement de périphériques audio dans Windows 10 (TH1) et Windows 10 November Update (TH2)
- Correction de sessions multiples n'apparaissant pas pour certaines applications
- Ajout des métadonnées de l'éditeur pour le Gestionnaire des tâches

## 1.3.0.0
- Correction de l'affichage du Speech Runtime
- Correction du positionnement quand le masquage auto de la barre des tâches est activé
- L'installateur/désinstallateur vérifie maintenant si l'app est en cours d'exécution
- Ajout de la possibilité de changer le périphérique audio par défaut (clic droit sur l'icône tray)
- Ajout de la possibilité de couper le son des apps/périphérique audio
- Ajout du slider de volume principal du périphérique par défaut

## 1.2.0.0
- Correction d'un problème avec certaines apps n'apparaissant pas dans Ear Trumpet lors de l'utilisation de services audio en arrière-plan (ex : iHeartRadio)
- Correction d'un problème avec certaines apps n'apparaissant pas dans Ear Trumpet lors de la lecture de médias protégés (ex : Netflix)
- Correction d'un problème avec les apps ne s'affichant pas à cause de chemins de logo/icône inattendus (ex : Skype Translator)
- Ajout de la localisation de base dans Ear Trumpet (anglais par défaut pour l'instant — n'hésitez pas à proposer des traductions via pull requests)

## 1.1.1.0

## 1.1.0.0
- Correction d'un problème de mise à l'échelle DPI
- Correction d'Ear Trumpet ne s'affichant pas correctement quand la barre des tâches était à un autre emplacement ou pas sur le moniteur principal
- Correction initiale pour les logos d'apps modernes manquants
- Ear Trumpet n'autorisera désormais qu'une seule instance ouverte
- Readme GitHub mis à jour avec les détails et versions minimales
- L'installateur n'autorise plus l'installation sur les versions de Windows antérieures à Windows 10
- Correction d'un problème avec le mode tablette de Windows 10
- Correction de la fenêtre Ear Trumpet n'ayant pas la bordure et l'ombre correctes

## 1.0.0.0
- Version initiale
