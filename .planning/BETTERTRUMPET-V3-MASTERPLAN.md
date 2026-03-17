# BetterTrumpet v3.0 — Plan d'Upgrade

> **IMPORTANT : On part de la v2.4 qui fonctionne. PAS de réécriture from scratch. PAS de nouveau dossier. On upgrade le code existant dans `C:\Users\xammen\Documents\CLAUDE\ear\` progressivement, feature par feature, en s'assurant que chaque étape compile et marche avant de passer à la suivante.**
>
> **Objectif :** Upgrader BetterTrumpet v2.4 vers v3.0 en corrigeant les bugs des versions publiques (Winget, Choco, portable), en ajoutant deux variantes (Normal + Portable), un onboarding premium type Arc, de la télémétrie/crash reporting, et un système de profils/automation.

---

## Table des Matières

1. [Diagnostic des bugs actuels](#1-diagnostic-des-bugs-actuels)
2. [Approche : Upgrade progressif (PAS de rewrite)](#2-approche--upgrade-progressif-pas-de-rewrite)
3. [Phase 1 — Fixes & Cleanup](#3-phase-1--fixes--cleanup-sur-la-v24-existante)
4. [Phase 2 — Onboarding Premium (style Arc)](#4-phase-2--onboarding-premium-style-arc)
5. [Phase 3 — Télémétrie & Crash Reporting](#5-phase-3--télémétrie--crash-reporting)
6. [Phase 4 — Packaging (Normal + Portable)](#6-phase-4--packaging-normal--portable)
7. [Phase 5 — Qualité & Fiabilité](#7-phase-5--qualité--fiabilité)
8. [Phase 6 — UX/UI Polish](#8-phase-6--uxui-polish)
9. [Phase 7 — Mode CLI (Community Request)](#9-phase-7--mode-cli-community-request)
10. [**Features v3 — Sélection finale**](#10-features-v3--sélection-finale) ⭐ VALIDÉ
11. [Trucs techniques auxquels tu n'as pas pensé](#11-trucs-techniques-auxquels-tu-nas-pas-pensé)
12. [Stack technique recommandée](#12-stack-technique-recommandée)
13. [Timeline estimée (ORDRE OPTIMISÉ)](#13-timeline-estimée-ordre-optimisé)

---

## ORDRE D'EXÉCUTION OPTIMISÉ

> **L'ordre des sections de référence (3-10) ci-dessous reste inchangé pour ne pas casser les ancres.**
> **C'est l'ordre d'EXÉCUTION qui change :** on suit la timeline en section 13.

| Étape | Quoi | Pourquoi cet ordre |
|-------|------|--------------------|
| **1** | Fixes & Cleanup (§3) | Base saine obligatoire |
| **2** | Qualité & Fiabilité (§7) | Error handling + logging AVANT d'ajouter du code |
| **3** | Télémétrie Sentry (§5) | Brancher sur le error handling, crash reporting dès le début |
| **4** | Profils & Moteur de Règles (§10.2) | Cœur de v3, tout le reste en dépend |
| **5** | CLI + Named Pipe IPC (§9) | L'IPC sert au CLI, Command Palette, Raycast |
| **6** | Undo/Redo + Command Palette + Keyboard Nav (§10.3-10.5) | Utilisent profils + IPC |
| **7** | Mini Mode PiP + Notifications (§10.5-10.6) | Polish UI, affichent profil actif |
| **8** | Onboarding Premium (§4) | À la FIN : présente TOUTES les features d'un coup |
| **9** | Packaging (§6) | Dernier : on package quand tout est stable |

---

## 1. Diagnostic des bugs actuels

### Pourquoi la version publique est "full bugée"

| Problème | Cause racine | Impact |
|----------|-------------|--------|
| **Winget installer marqué x64 mais l'app est x86** | `Architecture: x64` dans le manifest alors que le build est `PlatformTarget = x86` | L'exe ne se lance pas ou crash sur certaines configs |
| **Choco installe mais ne crée aucun raccourci** | Le script crée un `.ignore` pour empêcher le shim, mais ne crée ni raccourci Start Menu, ni startup entry, ni PATH | Après install, l'utilisateur ne sait pas comment lancer l'app |
| **Version mismatch** | appxmanifest dit 2.3.1.5, Choco/Winget disent 2.4.0 | Confusion, updates cassées |
| **Software Rendering forcé** | `RenderMode.SoftwareOnly` en ligne 55 de App.xaml.cs | Performance catastrophique, CPU élevé, animations saccadées |
| **Bugsnag désactivé mais DLLs embarquées** | Code commenté mais dépendances toujours packagées | Poids inutile, zéro crash reporting |
| **Pas d'auto-update (portable)** | Aucun mécanisme de vérification de nouvelle version | Les users portable restent sur des vieilles versions bugées |
| **Strings hardcodées en anglais** | ~33 nouvelles features pas dans Resources.resx | UI cassée pour les 33 locales supportées |
| **Empty catches** | 3+ catch blocs vides ou bare catch | Bugs silencieux impossibles à diagnostiquer |
| **WelcomeViewModel pointe vers EarTrumpet** | LearnMore → github.com/File-New-Project/EarTrumpet | Pas BetterTrumpet |
| **Privacy link pointe vers l'original** | OpenPrivacy → EarTrumpet/PRIVACY.md | Pas la politique de BetterTrumpet |
| **Pas de gestion d'erreur au démarrage** | Si un composant échoue (MediaPopup, etc.), un seul try/catch silencieux | Crash mystérieux pour l'utilisateur |
| **Registry pollution** | Settings écrites dans `HKCU\Software\EarTrumpet` même pour BetterTrumpet | Conflit si EarTrumpet est aussi installé |

---

## 2. Approche : Upgrade progressif (PAS de rewrite)

### Règles d'or

```
⚠️  NE PAS créer un nouveau dossier / nouveau projet
⚠️  NE PAS réécrire des fichiers entiers
⚠️  NE PAS changer les namespaces (rester sur EarTrumpet.*)
⚠️  NE PAS migrer vers .NET 8 (rester sur .NET Framework 4.6.2)
⚠️  NE PAS toucher au code qui marche déjà

✅  Travailler dans C:\Users\xammen\Documents\CLAUDE\ear\
✅  Ajouter des fichiers/classes à côté du code existant
✅  Modifier le code existant chirurgicalement (petits diffs)
✅  Builder après chaque changement (MSBuild Debug|x86)
✅  Tester que l'app se lance après chaque étape
✅  Utiliser la solution existante : EarTrumpet.vs15.sln
```

### Dossier de travail existant
```
C:\Users\xammen\Documents\CLAUDE\ear\           ← ON RESTE ICI
├── EarTrumpet/                                  ← App principale (on modifie ici)
│   ├── UI/Views/                                ← Ajouter OnboardingWindow.xaml ici
│   ├── UI/ViewModels/                           ← Ajouter les VMs ici
│   ├── Diagnosis/                               ← Modifier ErrorReporter.cs pour Sentry
│   ├── DataModel/Storage/                       ← Améliorer le settings system ici
│   └── ...                                      ← Le reste ne bouge pas
├── EarTrumpet.Package/                          ← MSIX (existe déjà)
├── chocolatey/                                  ← Fixer le script (existe déjà)
├── manifests/                                   ← Fixer le winget manifest (existe déjà)
├── .planning/                                   ← Ce plan
└── EarTrumpet.vs15.sln                          ← Solution existante
```

### Ce qu'on AJOUTE (nouveaux fichiers uniquement)
```
EarTrumpet/
  UI/Views/OnboardingWindow.xaml(.cs)            ← NEW : onboarding
  UI/ViewModels/OnboardingViewModel.cs           ← NEW : onboarding VM
  UI/ViewModels/AudioProfile.cs                  ← NEW : profils audio
  UI/ViewModels/AudioProfileManager.cs           ← NEW : gestion profils
  UI/ViewModels/RuleEngine.cs                    ← NEW : moteur de règles
  UI/ViewModels/CommandPaletteViewModel.cs        ← NEW : command palette
  UI/Views/CommandPaletteWindow.xaml(.cs)         ← NEW : command palette
  UI/Views/MiniModeWindow.xaml(.cs)              ← NEW : PiP widget
  Interop/Helpers/CliHandler.cs                  ← NEW : argument parsing CLI
  Interop/Helpers/PipeServer.cs                  ← NEW : Named Pipe IPC
  Interop/Helpers/PipeClient.cs                  ← NEW : Named Pipe client
  Interop/Helpers/UpdateChecker.cs               ← NEW : auto-update
```

### Ce qu'on MODIFIE (diffs chirurgicaux)
```
EarTrumpet/App.xaml.cs                           ← Ajouter CLI routing, pipe server, onboarding
EarTrumpet/AppSettings.cs                        ← Ajouter settings profils/règles
EarTrumpet/Diagnosis/ErrorReporter.cs            ← Remplacer Bugsnag par Sentry
EarTrumpet/UI/ViewModels/WelcomeViewModel.cs     ← Fixer les liens (GitHub BetterTrumpet)
EarTrumpet/Interop/Helpers/SingleInstanceAppMutex.cs ← Ajouter IPC au lieu de juste exit
chocolatey/tools/chocolateyInstall.ps1           ← Fixer raccourcis
manifests/.../xmn.BetterTrumpet.installer.yaml   ← Fixer architecture x86
```

### Deux variantes de distribution

| | **Normal (Installer)** | **Portable** |
|---|---|---|
| **Installation** | Wizard WiX/Inno avec onboarding | Extraire et lancer |
| **Settings storage** | `%APPDATA%\BetterTrumpet\` | `.\config\` à côté de l'exe |
| **Auto-update** | Intégré (check GitHub Releases API) | Notification + lien download |
| **Start Menu** | Raccourci créé automatiquement | Non |
| **Startup** | Option dans onboarding | Géré manuellement |
| **Uninstall** | Propre via Programs & Features | Supprimer le dossier |
| **Registry** | `HKCU\Software\BetterTrumpet` (minimal) | Aucune écriture registry |
| **Télémétrie** | Opt-in pendant onboarding | Opt-in dans Settings |

---

## 3. Phase 1 — Fixes & Cleanup (sur la v2.4 existante)

> **Rester sur .NET Framework 4.6.2, x86, même solution, même namespaces.**
> Chaque fix = un petit diff, on build, on teste, on passe au suivant.

### 1.1 Supprimer le Software Rendering
- Fichier : `EarTrumpet/App.xaml.cs` ligne 55
- Supprimer `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;`
- Tester que l'app se lance normalement avec le GPU rendering

### 1.2 Cleanup code mort
- `ErrorReporter.cs` : virer le commentaire Bugsnag, préparer le slot pour Sentry
- `Features.cs` : supprimer si vide
- Déduplicer les 7 `ColorConverter.ConvertFromString` dans `AppSettings.cs` → helper method
- Remplacer les 2-3 empty catches restants par `Trace.WriteLine`
- **NE PAS renommer les namespaces** (ça casse tout pour rien)
- **NE PAS toucher aux registry paths** pour l'instant (compat v2)

### 1.3 Fixer les liens BetterTrumpet
- `WelcomeViewModel.cs` : changer les URLs vers le repo BetterTrumpet
- Fixer la Privacy Policy URL

### 1.4 Fixer Winget manifest
- `manifests/.../xmn.BetterTrumpet.installer.yaml` : changer `Architecture: x64` → `x86`

### 1.5 Fixer Chocolatey
- `chocolatey/tools/chocolateyInstall.ps1` : ajouter création raccourci Start Menu + option startup

### 1.6 Mode Portable
- Ajouter la détection de `portable.marker` dans `StorageFactory.cs`
- Si le fichier existe à côté de l'exe → utiliser un `JsonFileSettingsBag` au lieu du registre
- Nouveau fichier : `EarTrumpet/DataModel/Storage/JsonFileSettingsBag.cs`
- Settings stockées dans `.\config\settings.json`
- **Le mode normal (registry) reste inchangé**

---

## 4. Phase 2 — Onboarding Premium (style Arc)

### Inspiration : Arc Browser Onboarding
L'onboarding d'Arc est considéré comme le meilleur du marché. Voici comment l'adapter :

### 2.1 Flow d'onboarding (5 écrans, navigation fluide)

```
[Écran 1: Bienvenue]
  ├── Animation logo BetterTrumpet (Lottie ou frame-by-frame)
  ├── "Bienvenue dans BetterTrumpet"
  ├── "Le contrôle de volume réinventé"
  └── Bouton "Commencer" (avec animation de hover)

[Écran 2: Choix du thème]
  ├── Prévisualisation LIVE du flyout avec le thème sélectionné
  ├── 3 choix principaux :
  │   ├── Sombre (preview)
  │   ├── Clair (preview)
  │   └── Système (suit Windows)
  ├── Grille de thèmes couleur rapide (6-8 accents)
  └── "Tu pourras changer à tout moment dans les Settings"

[Écran 3: Comportement]
  ├── "Comment veux-tu utiliser BetterTrumpet ?"
  ├── Options avec animations :
  │   ├── ☑ Lancer au démarrage de Windows
  │   ├── ☑ Scroll sur l'icône pour changer le volume
  │   ├── ☐ Remplacer le contrôle de volume Windows
  │   └── ☐ Afficher le popup média au survol
  └── Chaque option a une micro-animation explicative

[Écran 4: Raccourcis]
  ├── Raccourcis recommandés (avec capture interactive)
  │   ├── Ouvrir le flyout : (cliquer pour enregistrer)
  │   ├── Ouvrir le mixer : (cliquer pour enregistrer)
  │   └── Muter/Unmuter : (cliquer pour enregistrer)
  ├── "Passer" pour ne rien configurer
  └── Animation de clavier stylisée

[Écran 5: Prêt !]
  ├── Résumé des choix en mode compact
  ├── Toggle télémétrie :
  │   "Aide-nous à améliorer BetterTrumpet en envoyant
  │    des rapports de crash anonymes"
  │   ☑ Activé (recommandé) / ☐ Désactivé
  ├── Texte GDPR clair avec lien Privacy Policy
  ├── Bouton "Lancer BetterTrumpet" (gros, animé)
  └── Confetti/particle effect au clic
```

### 2.2 Détails techniques de l'onboarding
- **Transitions entre écrans :** Slide horizontal avec easing (`CubicEase`), 400ms
- **Indicateur de progression :** Dots ou barre en bas (comme Arc)
- **Navigation :** Boutons Suivant/Précédent + swipe gesture support
- **Animations :** Utiliser `Storyboard` WPF natif, pas de librairie externe
- **Preview thème live :** Un mini-composant flyout qui réagit en temps réel
- **Fond :** Gradient animé subtil ou particules (selon perf)
- **Skip possible :** Lien discret "Passer la configuration" qui applique les défauts

### 2.3 First-Run Detection améliorée
```
SI première installation → Onboarding complet (5 écrans)
SI mise à jour majeure (v2→v3) → Mini-onboarding "What's New" (2 écrans)
SI mise à jour mineure → Notification discrète "Mis à jour en v3.x"
```

---

## 5. Phase 3 — Télémétrie & Crash Reporting

### 3.1 Architecture de télémétrie

**Solution recommandée : Sentry.io (gratuit pour OSS, 5K events/mois)**

Alternative : self-hosted avec Plausible/PostHog/GlitchTip

| Donnée | Quand | Pourquoi |
|--------|-------|----------|
| **Crash reports** | Exception non gérée | Identifier et fixer les bugs |
| **Session start** | Au lancement | Comptage d'installations actives |
| **Version + OS** | Au lancement | Savoir qui est sur quelle version |
| **Feature usage** | À l'action | Savoir quelles features sont utilisées |
| **Error breadcrumbs** | Avant un crash | Comprendre le chemin vers le crash |
| **Performance metrics** | Périodique | Détecter les lenteurs |

### 3.2 Ce qu'on collecte (et ce qu'on ne collecte PAS)

**ON COLLECTE :**
- Exception stack traces (sans données personnelles)
- Version de l'app, version Windows, architecture CPU
- Langue/région du système
- Nombre de périphériques audio
- Thème actif (light/dark/custom)
- Durée de session
- Features activées (liste booléenne, pas de détails)
- GPU capabilities (pour diagnostiquer rendering issues)

**ON NE COLLECTE PAS :**
- Noms d'applications audio (Spotify, Discord, etc.)
- Volume levels
- Noms de périphériques
- Adresse IP (pas de géolocalisation fine)
- Aucune donnée identifiable

### 3.3 Dashboard télémétrie (pour toi)

Mettre en place un dashboard qui montre :
- **Installations actives** (DAU/WAU/MAU)
- **Versions déployées** (pie chart)
- **Taux de crash** (% de sessions avec crash)
- **Top 5 crashes** (groupés par stack trace)
- **OS breakdown** (Win10 vs Win11, builds)
- **Architecture** (x86/x64/ARM64)
- **Rétention** (combien reviennent après 1j, 7j, 30j)
- **Feature adoption** (quel % utilise les thèmes, media popup, etc.)

### 3.4 Conformité GDPR
- **Opt-in explicite** pendant l'onboarding (pas opt-out)
- Toggle clair dans Settings avec description exacte des données
- Lien vers Privacy Policy complète
- Possibilité de demander la suppression (email ou bouton in-app)
- Pas de tracking tant que l'utilisateur n'a pas consenti
- Détection EU/EEA pour double opt-in si nécessaire

---

## 6. Phase 4 — Packaging (Normal + Portable)

### 4.1 Version Normal (Installer)

**Technologie : Inno Setup** (gratuit, mature, customisable)
- Ou WiX v4 si on veut du MSI corporate-friendly

**L'installer fait :**
1. Vérifie les prérequis (.NET Runtime si .NET 8)
2. Installe dans `%ProgramFiles%\BetterTrumpet\`
3. Crée raccourci Start Menu + Bureau (optionnel)
4. Enregistre dans Programs & Features (ajout/suppression)
5. Configure l'auto-start si choisi dans l'onboarding
6. Lance l'app avec le flag `--first-run`
7. L'app détecte `--first-run` et lance l'onboarding

**Uninstaller fait :**
1. Ferme l'app si en cours d'exécution
2. Supprime les fichiers
3. Supprime les raccourcis
4. Nettoie le registre
5. Optionnel : "Garder vos paramètres ?" → ne supprime pas `%APPDATA%`

### 4.2 Version Portable

**Comment ça marche :**
- Un seul exe + un dossier `config/` à côté
- Présence d'un fichier `portable.marker` déclenche le mode portable
- Zéro écriture registre
- Settings dans `./config/settings.json`
- Logs dans `./config/logs/`
- Peut tourner depuis une clé USB

**Distribution :**
- ZIP avec : `BetterTrumpet.exe` + `portable.marker` + `config/` (vide)
- Ou self-extracting archive

### 4.3 Winget (corrigé)
```yaml
PackageIdentifier: xmn.BetterTrumpet
PackageVersion: 3.0.0
Installers:
  # Version installer
  - Architecture: x64
    InstallerType: inno  # ou exe/nullsoft
    InstallerUrl: https://github.com/.../BetterTrumpet-3.0.0-Setup-x64.exe
    InstallerSha256: ...
    InstallerSwitches:
      Silent: /VERYSILENT /SUPPRESSMSGBOXES
      SilentWithProgress: /SILENT
  # Version portable (séparée ou même package avec switch)
  - Architecture: x64
    InstallerType: portable
    InstallerUrl: https://github.com/.../BetterTrumpet-3.0.0-Portable-x64.zip
    InstallerSha256: ...
```

### 4.4 Chocolatey (corrigé)
```powershell
# chocolateyInstall.ps1 - Version COMPLÈTE
$installDir = Join-Path $env:ProgramFiles 'BetterTrumpet'
# Ou utiliser Inno Setup en mode silent
Install-ChocolateyPackage @packageArgs
# Créer raccourci Start Menu
Install-ChocolateyShortcut -ShortcutFilePath "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\BetterTrumpet.lnk" -TargetPath "$installDir\BetterTrumpet.exe"
```

### 4.5 Auto-Update (pour la version portable)
```
Au démarrage (toutes les 24h) :
  1. GET https://api.github.com/repos/xammen/BetterTrumpet/releases/latest
  2. Comparer version locale vs version remote
  3. Si nouvelle version :
     → Notification discrète dans le system tray
     → "BetterTrumpet 3.1.0 est disponible. Mettre à jour ?"
     → Clic → Ouvre la page de téléchargement (ou download in-app)
  4. Pour la version installer : télécharger l'installer et lancer silently
```

---

## 7. Phase 5 — Qualité & Fiabilité

### 5.1 Gestion d'erreurs globale
```csharp
// Capturer TOUTES les exceptions non gérées
AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

// Pour chaque : logger vers Sentry + afficher dialog user-friendly
// "Oups ! BetterTrumpet a rencontré un problème.
//  Le rapport a été envoyé automatiquement.
//  [Relancer] [Quitter]"
```

### 5.2 Startup robuste
```
Phase 1: Core (si échoue → fatal, crash report, exit)
  - Settings load
  - Telemetry init
  - Audio API init

Phase 2: UI (si échoue → démarrage dégradé, notifier l'user)
  - Theme manager
  - Tray icon
  - Flyout window

Phase 3: Features (si échoue → feature désactivée, log)
  - Media popup
  - Addons
  - Auto-update check
  - Hotkeys
```

### 5.3 Tests
- **Unit tests** : Settings serialization, theme parsing, color conversion
- **Integration tests** : Audio API mocks, Settings persistence
- **UI tests** : Onboarding flow, theme switching
- **Smoke tests** : L'app démarre sans crash sur Win10/Win11
- **Package tests** : Installer/uninstaller/portable fonctionnent

### 5.4 Logging structuré
```
[2026-03-15 14:23:45.123] [INFO ] [App] Starting BetterTrumpet v3.0.0 (x64, .NET 8, Win11 22621)
[2026-03-15 14:23:45.234] [INFO ] [Audio] Found 3 playback devices
[2026-03-15 14:23:45.345] [WARN ] [Theme] Custom theme "MyTheme" has invalid color, using default
[2026-03-15 14:23:45.456] [ERROR] [Media] Failed to initialize media session: {exception}
```
- Rotation des logs : max 5 fichiers de 5MB
- Accessible via Settings → About → "Exporter les logs"

### 5.5 Health monitoring
- Détection de memory leaks (surveiller la mémoire toutes les 5min)
- Détection de high CPU (si >10% pendant 30s, réduire les animations)
- Handle leak detection (surveiller le compteur GDI/User objects)
- Watchdog : si le thread UI est bloqué >5s, forcer un dump et notifier

---

## 8. Phase 6 — UX/UI Polish

### 6.1 Animations fluides
- Supprimer `RenderMode.SoftwareOnly` → GPU rendering
- 60fps pour les sliders et peak meters
- Transitions douces entre les thèmes (lerp 300ms)
- Flyout : entrée/sortie avec spring animation (comme Win11)
- Settings : navigation avec slide transitions

### 6.2 Accessibilité
- **Screen reader support** complet (Narrator, NVDA, JAWS)
- **Keyboard navigation** complète dans tous les écrans
- **High contrast** mode support
- **Respect du `prefers-reduced-motion`** de Windows
- **Touch friendly** : cibles de tap minimum 44x44px

### 6.3 Localisation complète
- Migrer toutes les strings hardcodées vers Resources.resx
- Ajouter les nouvelles clés pour toutes les features BetterTrumpet
- Tester les layouts avec des langues longues (allemand) et RTL (arabe)
- Mettre à jour Crowdin avec les nouvelles clés

### 6.4 Notifications intelligentes
```
- "BetterTrumpet tourne en arrière-plan" (première fois seulement)
- "Nouvelle version disponible : 3.1.0"
- "Un périphérique audio a été ajouté : Casque Bluetooth"
- "Crash précédent détecté. Rapport envoyé." (si telemetry on)
```

---

## 9. Phase 7 — Mode CLI (Community Request)

> Feature demandée par la communauté. Transforme BetterTrumpet en outil scriptable.

### 7.0 Pourquoi c'est important
- **Power users** : scripters, streamers, DevOps
- **Stream Deck** : assignable en une action custom
- **AutoHotkey / PowerShell** : automatisation audio
- **CI/CD audio** : changer de device dans un pipeline (test automation)
- **Accessibilité** : contrôle audio sans interface graphique

### 7.1 Commandes CLI complètes

```
USAGE: BetterTrumpet.exe [OPTIONS] [COMMAND]

Sans commande → lance l'interface graphique (comportement normal)
Avec commande → exécute la commande et retourne un exit code

COMMANDES:
───────────────────────────────────────────────────────────────────

  INFORMATIONS
  ─────────────
  --list-devices                     Liste tous les périphériques audio
  --list-apps                        Liste les apps avec une session audio active
  --status                           État global (device par défaut, apps, volumes)

  CONTRÔLE PAR APP
  ─────────────────
  --set <app> <device>               Assigner une app à un périphérique
  --unset <app>                      Remettre une app sur le périphérique par défaut
  --set-volume <app> <0-100>         Changer le volume d'une app
  --mute <app>                       Muter une app
  --unmute <app>                     Unmuter une app
  --toggle-mute <app>                Toggle mute d'une app

  CONTRÔLE GLOBAL
  ────────────────
  --set-default <device>             Changer le périphérique par défaut
  --set-master-volume <0-100>        Changer le volume master
  --mute-all                         Muter tout
  --unmute-all                       Unmuter tout

  PROFILS (Phase 7+)
  ───────────────────
  --save-profile <name>              Sauvegarder l'état audio actuel en profil
  --load-profile <name>              Charger un profil audio
  --list-profiles                    Lister les profils disponibles
  --delete-profile <name>            Supprimer un profil

  AVANCÉ
  ───────
  --watch                            Mode streaming : affiche les événements en temps réel (JSON)
  --json                             Forcer la sortie en JSON (combinable avec toute commande)
  --quiet                            Pas de sortie (juste l'exit code)
  --version                          Afficher la version
  --help                             Afficher cette aide

OPTIONS:
  --json                             Sortie JSON au lieu de texte formaté
  --quiet / -q                       Pas de sortie stdout (juste exit code)
  --no-color                         Désactiver les couleurs ANSI dans la sortie

EXIT CODES:
  0 = Succès
  1 = Erreur générale
  2 = App non trouvée
  3 = Device non trouvé
  4 = Instance BetterTrumpet non en cours d'exécution (pour les commandes qui en ont besoin)
  5 = Argument invalide
```

### 7.2 Exemples d'utilisation concrets

```powershell
# ═══════════════════════════════════════════════════
#  LISTER LES DEVICES
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --list-devices

  # │ Device                                    │ Default │ Volume
  ──┼───────────────────────────────────────────┼─────────┼────────
  1 │ Speakers (Realtek High Definition Audio)   │   ✓     │  72%
  2 │ CABLE Input (VB-Audio Virtual Cable)       │         │ 100%
  3 │ Headphones (USB Audio Device)              │         │  85%
  4 │ CABLE-C Input (VB-Audio Cable C)           │         │ 100%

# Version JSON :
> BetterTrumpet.exe --list-devices --json

  [
    {"id": "{0.0.0...}", "name": "Speakers (Realtek High Definition Audio)", "default": true, "volume": 72, "muted": false},
    {"id": "{0.0.1...}", "name": "CABLE Input (VB-Audio Virtual Cable)", "default": false, "volume": 100, "muted": false},
    ...
  ]

# ═══════════════════════════════════════════════════
#  LISTER LES APPS
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --list-apps

  # │ App                │ Device                           │ Volume │ Muted
  ──┼────────────────────┼──────────────────────────────────┼────────┼──────
  1 │ spotify.exe         │ Speakers (Realtek HD Audio)      │   65%  │  No
  2 │ chrome.exe          │ Speakers (Realtek HD Audio)      │  100%  │  No
  3 │ discord.exe         │ Headphones (USB Audio)           │   80%  │  No
  4 │ vlc.exe             │ CABLE Input (VB-Audio)           │   90%  │  No
  5 │ System Sounds       │ Speakers (Realtek HD Audio)      │   30%  │  No

# ═══════════════════════════════════════════════════
#  ASSIGNER UNE APP À UN DEVICE (la feature demandée)
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --set vlc.exe "CABLE-C Input (VB-Audio Cable C)"
  ✓ vlc.exe → CABLE-C Input (VB-Audio Cable C)

# Matching flou (pas besoin du nom exact) :
> BetterTrumpet.exe --set vlc "CABLE-C"
  ✓ vlc.exe → CABLE-C Input (VB-Audio Cable C)

# Remettre sur le défaut :
> BetterTrumpet.exe --unset vlc.exe
  ✓ vlc.exe → Default (Speakers)

# ═══════════════════════════════════════════════════
#  CONTRÔLE VOLUME
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --set-volume spotify 50
  ✓ spotify.exe volume: 65% → 50%

> BetterTrumpet.exe --mute discord
  ✓ discord.exe muted

> BetterTrumpet.exe --set-master-volume 30
  ✓ Master volume: 72% → 30%

# ═══════════════════════════════════════════════════
#  STATUS GLOBAL
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --status

  BetterTrumpet v3.0.0 — Audio Status
  ═════════════════════════════════════
  Default Device: Speakers (Realtek HD Audio) [72%]

  Active Sessions:
    spotify.exe      │ Speakers      │  50% │ ♪ Playing
    chrome.exe       │ Speakers      │ 100% │ ♪ Playing
    discord.exe      │ Headphones    │  80% │ Muted

# ═══════════════════════════════════════════════════
#  MODE WATCH (streaming d'événements)
# ═══════════════════════════════════════════════════

> BetterTrumpet.exe --watch --json

  {"event":"session_created","app":"spotify.exe","device":"Speakers","volume":65,"time":"14:23:45"}
  {"event":"volume_changed","app":"spotify.exe","volume":50,"time":"14:23:47"}
  {"event":"device_added","device":"Bluetooth Headphones","time":"14:24:01"}
  {"event":"session_moved","app":"discord.exe","from":"Speakers","to":"Headphones","time":"14:24:05"}
  ^C

# ═══════════════════════════════════════════════════
#  STREAM DECK / SCRIPTS
# ═══════════════════════════════════════════════════

# Script PowerShell : "Gaming Mode"
BetterTrumpet.exe --set discord "Headphones"
BetterTrumpet.exe --set-volume spotify 20
BetterTrumpet.exe --set-volume discord 100
BetterTrumpet.exe --unmute-all

# Script Batch : mute Spotify quand en meeting
BetterTrumpet.exe --mute spotify --quiet
if %ERRORLEVEL% EQU 2 echo Spotify pas lancé
```

### 7.3 Architecture technique du CLI

```
┌─────────────────────────────────────────────────────┐
│                  BetterTrumpet.exe                   │
│                                                     │
│  Main() entry point                                 │
│    │                                                │
│    ├── args.Length == 0 ?                            │
│    │     └── YES → StartGui() (comportement normal)  │
│    │                                                │
│    └── NO → ParseCliArgs(args)                      │
│              │                                      │
│              ├── Commande "read-only" ?              │
│              │   (--list-devices, --list-apps, etc.) │
│              │     └── Initialiser AudioManager      │
│              │         directement, pas besoin de     │
│              │         l'instance GUI                 │
│              │         → Exécuter, print, exit        │
│              │                                      │
│              └── Commande "write" ?                  │
│                  (--set, --mute, --set-volume, etc.)  │
│                    │                                 │
│                    ├── Instance GUI running ?         │
│                    │     └── YES → Envoyer via        │
│                    │              Named Pipe IPC      │
│                    │              → Attendre réponse   │
│                    │              → Print + exit       │
│                    │                                 │
│                    └── NO → Initialiser AudioManager  │
│                            directement (headless)     │
│                            → Exécuter, print, exit    │
│                                                     │
└─────────────────────────────────────────────────────┘
```

### 7.4 IPC via Named Pipes

```csharp
// Côté serveur (instance GUI, toujours à l'écoute)
public class CliPipeServer : IDisposable
{
    private const string PipeName = "BetterTrumpet.CLI";
    
    // Boucle qui écoute les commandes entrantes
    // Format : JSON { "command": "set", "args": ["vlc.exe", "CABLE-C"] }
    // Réponse : JSON { "success": true, "message": "...", "data": {...} }
}

// Côté client (instance CLI)
public class CliPipeClient
{
    // Se connecte au pipe, envoie la commande, attend la réponse
    // Timeout 5 secondes → exit code 4 si l'instance GUI ne répond pas
}
```

**Pourquoi Named Pipes ?**
- Natif Windows, zero dépendance
- `System.IO.Pipes` disponible même en .NET Framework 4.6.2
- Rapide (< 1ms local), fiable
- Sécurisé (même session Windows uniquement par défaut)
- Pas besoin d'un port réseau (pas de firewall issues)

### 7.5 Matching flou des noms

```csharp
// L'utilisateur tape "vlc" ou "VLC" ou "vlc.exe" → on trouve "vlc.exe"
// L'utilisateur tape "CABLE-C" → on trouve "CABLE-C Input (VB-Audio Cable C)"

static string FuzzyMatch(string input, IEnumerable<string> candidates)
{
    // 1. Exact match (case-insensitive)
    // 2. StartsWith match
    // 3. Contains match
    // 4. Levenshtein distance ≤ 2
    // 5. Si plusieurs candidats → erreur avec suggestions
}
```

**Exemples de matching :**
| Input | Match | Méthode |
|-------|-------|---------|
| `vlc.exe` | `vlc.exe` | Exact |
| `vlc` | `vlc.exe` | StartsWith |
| `VLC` | `vlc.exe` | StartsWith (case-insensitive) |
| `spotify` | `spotify.exe` | StartsWith |
| `CABLE-C` | `CABLE-C Input (VB-Audio Cable C)` | StartsWith |
| `Headphones` | `Headphones (USB Audio Device)` | StartsWith |
| `Realtek` | `Speakers (Realtek High Definition Audio)` | Contains |
| `spotigy` | `spotify.exe` | Levenshtein (distance 1) |
| `cable` | ❌ Ambiguë : CABLE Input, CABLE-C Input | Erreur + suggestions |

### 7.6 Mode Watch (événements temps réel)

```
BetterTrumpet.exe --watch [--json] [--filter <type>]

Types d'événements :
  session_created    - Nouvelle app commence à jouer du son
  session_removed    - App arrête de jouer
  session_moved      - App déplacée vers un autre device
  volume_changed     - Volume d'une app change
  mute_changed       - Mute/unmute d'une app
  device_added       - Nouveau périphérique connecté
  device_removed     - Périphérique déconnecté
  default_changed    - Device par défaut changé
  peak_update        - Niveau audio en temps réel (opt-in, verbose)

Filtres :
  --watch --filter session    → seulement les événements de session
  --watch --filter device     → seulement les événements de device
  --watch --filter volume     → seulement les changements de volume
```

C'est parfait pour :
- **OBS scripts** : détecter quand Spotify joue pour afficher un widget
- **Stream automation** : changer de scène quand un device est connecté
- **Monitoring** : logger l'activité audio d'un PC

### 7.7 Intégration avec outils tiers

| Outil | Comment l'utiliser |
|-------|--------------------|
| **Stream Deck** | "System" → "Open" → `BetterTrumpet.exe --set discord "Headphones"` |
| **AutoHotkey** | `Run, BetterTrumpet.exe --toggle-mute spotify,, Hide` |
| **PowerShell** | `$devices = BetterTrumpet.exe --list-devices --json \| ConvertFrom-Json` |
| **Task Scheduler** | Planifier `--load-profile "Night"` à 22h |
| **Batch file** | `@BetterTrumpet.exe --mute-all --quiet` |
| **NirCmd combo** | Utiliser avec NirCmd pour des actions système + audio |
| **Home Assistant** | Appeler via SSH/WinRM pour contrôle domotique |

---

## 10. Features v3 — Sélection finale

> Sélection validée par l'utilisateur. Le reste est archivé en section 10bis pour plus tard.

---

### 10.1 Command Palette Audio (`Win+Shift+V`) — PLUGIN RAYCAST + IN-APP

**Raycast Plugin** (prioritaire) : extension Raycast native pour chercher/contrôler l'audio.
Communique avec BetterTrumpet via le Named Pipe IPC (même infra que le CLI).

**In-App** (aussi) : hotkey `Win+Shift+V` ouvre un champ flottant :
```
┌──────────────────────────────────────────────┐
│ 🔊  chill_                                    │
│                                              │
│  ▸ Activer profil "Chill"            [Enter] │
│    Activer profil "Chill Night"      [↓ ↵]   │
│                                              │
│  Récents:                                    │
│    chill  ·  mute discord  ·  spotify 40%    │
└──────────────────────────────────────────────┘
```
- Tape un nom de profil → l'active
- Tape `spotify 40` → set volume à 40%
- Tape `mute discord` → mute
- Fuzzy search sur apps, devices, profils
- Historique des dernières commandes
- Apparaît/disparaît en <100ms

---

### 10.2 Profils & Moteur de Règles (LE coeur de la v3)

C'est LA feature. Un système de profils avec des règles automatiques.

#### Profils — ce qu'ils contiennent

Un profil = un snapshot de l'état audio qu'on peut restaurer :
```
┌── Profil "Chill" ─────────────────────────────────────────┐
│                                                           │
│  Device par défaut : Speakers                             │
│  Master volume : 60%                                      │
│                                                           │
│  Règles par app :                                         │
│    spotify.exe    → 80%  (monter la musique)              │
│    discord.exe    → 30%  (baisser le vocal)               │
│    chrome.exe     → 40%  (baisser les vidéos)             │
│    *              → 20%  (tout le reste bas)               │
│                                                           │
│  Options :                                                │
│    ☑ Muter les notifications système                      │
│    ☐ Changer de device                                    │
│    ☑ Volume cap : max 70% pour toute app                  │
│                                                           │
└───────────────────────────────────────────────────────────┘
```

#### Profils built-in (prêts à l'emploi, éditables)

| Profil | Ce qu'il fait |
|--------|---------------|
| **Chill** | Spotify monte, tout le reste baisse, notifications mutées |
| **Gaming** | Jeu à 100%, Discord à 80%, musique à 20% |
| **Meeting** | App de visio à 100%, tout le reste muté |
| **Night** | Volume cap à 40%, tout réduit proportionnellement |
| **Focus** | Musique à 50%, tout le reste muté |
| **Cinema** | Un seul app à 100% (le lecteur vidéo actif), reste muté |

L'utilisateur peut en créer autant qu'il veut.

#### Moteur de règles — déclencheurs automatiques

```
┌─────────────────────────────────────────────────────────────┐
│  RÈGLES D'AUTOMATION                               [+ New] │
│                                                             │
│  ┌─ Règle 1 ──────────────────────────────── [ON] [Edit] ─┐│
│  │  QUAND  Heure entre 22:00 et 07:00                      ││
│  │  FAIRE  Activer profil "Night"                          ││
│  │  SINON  Restaurer l'état précédent                      ││
│  └─────────────────────────────────────────────────────────┘│
│                                                             │
│  ┌─ Règle 2 ──────────────────────────────── [ON] [Edit] ─┐│
│  │  QUAND  spotify.exe est lancé ET aucun jeu actif        ││
│  │  FAIRE  Activer profil "Chill"                          ││
│  └─────────────────────────────────────────────────────────┘│
│                                                             │
│  ┌─ Règle 3 ──────────────────────────────── [ON] [Edit] ─┐│
│  │  QUAND  discord.exe OU teams.exe OU zoom.exe est lancé  ││
│  │  FAIRE  Baisser musique à 20%                           ││
│  │  SINON  Remonter musique au niveau précédent            ││
│  └─────────────────────────────────────────────────────────┘│
│                                                             │
│  ┌─ Règle 4 ──────────────────────────────── [ON] [Edit] ─┐│
│  │  QUAND  Un jeu (fullscreen) est détecté                 ││
│  │  FAIRE  Activer profil "Gaming"                         ││
│  └─────────────────────────────────────────────────────────┘│
│                                                             │
│  ┌─ Règle 5 ──────────────────────────────── [OFF][Edit] ─┐│
│  │  QUAND  Casque Bluetooth connecté                       ││
│  │  FAIRE  Changer device par défaut + profil "Headphones" ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

#### Types de déclencheurs (QUAND)

| Catégorie | Déclencheurs disponibles |
|-----------|-------------------------|
| **Temps** | Heure précise, plage horaire, jour de la semaine |
| **App** | App lancée, app fermée, app passe en fullscreen |
| **Device** | Périphérique connecté, déconnecté, device par défaut changé |
| **Système** | Windows Focus Mode activé, veille/réveil, session lock/unlock |
| **Combinaison** | ET / OU entre plusieurs déclencheurs |

#### Types d'actions (FAIRE)

| Action | Description |
|--------|-------------|
| **Charger profil** | Appliquer un profil complet |
| **Changer volume app** | `spotify.exe → 40%` |
| **Muter/Unmuter app** | `discord.exe → mute` |
| **Muter/Unmuter tout** | Mute ou unmute global |
| **Changer device** | Changer le périphérique par défaut |
| **Volume cap** | Limiter le volume max de toute app |
| **Restaurer** | Revenir à l'état d'avant la règle |

#### Priorité des règles & conflits
- Les règles s'exécutent par ordre de priorité (drag & drop pour réordonner)
- Si 2 règles se contredisent, la plus haute dans la liste gagne
- Une règle peut avoir un SINON (quand la condition n'est plus vraie → rollback)
- Notification discrète quand une règle s'active : "Profil Night activé (règle horaire)"

#### Détection de jeux
Comment l'app sait qu'un "jeu" tourne ?
1. **Fullscreen detection** : process qui occupe tout l'écran (pas une fenêtre maximisée)
2. **Liste connue** : base de données embarquée d'exe de jeux populaires (~500 jeux)
3. **Apprentissage** : l'user marque une app comme "jeu" → on la retient
4. **Steam/Epic/Xbox** : détecter les launchers et leurs enfants

#### Détection d'apps de communication
Auto-détecté : `discord.exe`, `teams.exe`, `zoom.exe`, `slack.exe`,
`skype.exe`, `teamspeak.exe`, `mumble.exe`, `webex.exe`, `gotomeeting.exe`
+ l'user peut ajouter les siennes.

#### Détection d'apps de musique
Auto-détecté : `spotify.exe`, `itunes.exe`, `musicbee.exe`, `foobar2000.exe`,
`vlc.exe`, `winamp.exe`, `aimp.exe`, `tidal.exe`, `amazonmusic.exe`
+ l'user peut ajouter les siennes.

---

### 10.3 Undo/Redo Volume (`Ctrl+Z` / `Ctrl+Y`)

Chaque changement de volume est empilé dans un historique :
```
  HISTORIQUE (50 dernières actions) :
  
  ← Ctrl+Z                                    Ctrl+Y →
  
  22:14:03  Spotify      70% → 40%     [undo]
  22:14:01  Discord      80% → mute    [undo]
  22:13:45  Master       72% → 50%     [undo]
  22:13:30  Profil "Night" activé      [undo]
  22:12:15  Chrome       100% → 60%    [undo]
```

- `Ctrl+Z` quand le flyout est ouvert → undo la dernière action
- `Ctrl+Y` → redo
- Les changements manuels ET les changements par règle sont dans l'historique
- Undo un profil = restaure l'état d'avant le profil
- Pas de Ctrl+Z quand le flyout est fermé (pour pas interférer avec d'autres apps)

---

### 10.4 Keyboard Navigation (optionnel, activable dans Settings)

Désactivé par défaut. L'user l'active dans Settings → "Mode Power User".

Quand le flyout ou le mixer est ouvert :
```
  J / ↓        → App suivante
  K / ↑        → App précédente
  H / ←        → Volume -2%
  L / →        → Volume +2%
  Shift+H/L    → Volume -10% / +10%
  M            → Toggle mute
  S            → Solo (mute tout sauf celle-ci)
  0            → Volume à 0%
  5            → Volume à 50%
  9            → Volume à 100%
  P            → Ouvrir sélecteur de profils
  Ctrl+Z       → Undo
  Ctrl+Y       → Redo
  /            → Ouvrir command palette
  Esc          → Fermer
  ?            → Afficher l'aide raccourcis
```

Feedback visuel : l'app sélectionnée est highlight, le volume change en temps réel.

---

### 10.5 Mini Mode / PiP (Picture-in-Picture)

Un widget flottant ultra-compact, toujours visible :
```
  ┌──────────────────────────────────┐
  │  🔊 Master          ████████ 72% │
  │  🎵 Spotify         ██████░░ 65% │
  │  🎮 Valorant        ████████ 100%│
  │  💬 Discord         ████████ 80% │
  └──────────────────────────────────┘
```

- **Hotkey** pour show/hide (ex: `Win+Alt+V`)
- **Snap** aux bords de l'écran (magnétique)
- **Transparence** : 30% quand pas survolé, 100% au hover
- **Click-through** optionnel (la souris passe à travers quand transparent)
- **Compact** : montre seulement les apps actives (celles qui jouent du son)
- **Interaction** : clic sur un slider pour changer le volume, clic droit pour mute
- **Double-clic** : ouvre le mixer complet
- **Profil actif** affiché en bas : `[Chill]` (clic pour changer)
- **Redimensionnable** : drag le coin pour + ou - d'apps visibles
- Se souvient de sa position entre les sessions

---

### 10.6 Notifications contextuelles

Notifications discrètes dans le style Windows 11 (toast natif ou custom flyout) :

| Événement | Notification |
|-----------|-------------|
| **Règle activée** | "Profil **Night** activé automatiquement (22:00)" |
| **Règle désactivée** | "Profil **Night** désactivé, état précédent restauré" |
| **Nouveau device** | "Périphérique connecté : **Headphones USB**" [Définir par défaut ?] |
| **Device déconnecté** | "**Casque Bluetooth** déconnecté — basculé sur Speakers" |
| **Profil chargé (CLI/hotkey)** | "Profil **Gaming** activé" (discret, 2 secondes) |
| **Update disponible** | "BetterTrumpet **3.2.0** disponible" [Mettre à jour] |
| **Crash précédent** | "BetterTrumpet a redémarré après un problème. Rapport envoyé." |

Options :
- Chaque type de notification peut être activé/désactivé individuellement
- Niveau de verbosité : Minimal (erreurs seulement) / Normal / Détaillé
- Son de notification : Aucun / Son système / Son custom
- Position : coin de l'écran (comme les toasts Win11)
- Durée : 2s / 5s / Jusqu'au clic

---

### 10.7 Archivé — Features pour plus tard

<details>
<summary>Cliquer pour voir les features reportées (Streamer Mode, ChatMix, Ducking, Spaces, Game Bar, Stream Deck, REST API, Config-as-Code, HUD, Plugins, IoT, Voice Control, etc.)</summary>

Les features suivantes ont été évaluées et mises de côté pour des phases futures :

- **S2. Streamer Mode** (séparation audio stream/perso) — complexe, virtual audio devices
- **S3. ChatMix Slider** (Game vs Chat) — cool mais niche
- **S4. Audio Ducking** (mic activé → musique baisse) — utile mais post-v3
- **A4. Spaces/Groupes** (catégoriser les apps) — UX complexe
- **A5. Xbox Game Bar Widget** — API spécifique, post-v3
- **A6. Stream Deck Plugin natif** — SDK Elgato, post-v3
- **B1. Smart Volume Normalization** — algorithmique complexe
- **B2. Hearing Health Tracker** — différenciant mais pas prioritaire
- **B3. REST API locale** — utile pour IoT, post-v3
- **B4. Config-as-Code** (YAML) — power users, post-v3
- **B7. Plugin System** (Obsidian-style) — écosystème, post-v3
- **C1-C4.** HUD Dashboard, Dynamic Theme, Wallpaper, Micro-interactions — polish
- **D1-D4.** Home Assistant, Phone Remote, Discord RPC, Stream Deck — intégrations
- **E1-E4.** Voice control, AI Detection, Spatial Audio, Cross-device sync — futur

</details>

---

## 11. Trucs techniques auxquels tu n'as pas pensé

### Critique (à faire absolument)

1. **Graceful degradation** — Si Windows Audio Service crash ou est désactivé, l'app ne doit pas mourir. Afficher "Service audio indisponible, reconnexion..." et réessayer.

2. **Migration des settings** — Les utilisateurs actuels de BetterTrumpet v2 ont des settings dans le registre. La v3 doit les détecter, les importer automatiquement, et informer l'utilisateur.

3. **Gestion des DPI multiples** — Tu supportes les setups multi-écran avec DPI différents ? Le flyout doit s'adapter à l'écran où est la taskbar, pas à l'écran principal.

4. **Gestion du mode veille/réveil** — Quand le PC sort de veille, les périphériques audio peuvent changer. L'app doit détecter et se remettre à jour proprement.

5. **Signature de code** — Sans signature, Windows SmartScreen bloque l'exe avec un avertissement flippant. Les utilisateurs n'installeront pas. Il FAUT un certificat de code signing.

6. **UAC awareness** — L'app ne doit JAMAIS demander l'élévation admin. Si une feature le nécessite, la séparer dans un processus helper.

7. **Crash recovery** — Si l'app crash, au redémarrage elle doit restaurer son état (settings intactes, pas de corruption).

8. **Multiple audio endpoint support** — Windows 11 a les "sound endpoints" séparés (même device, plusieurs outputs). Faut les supporter.

### Important (très recommandé)

9. **Terms of Service / EULA** — Même minimaliste, avoir une licence claire protège juridiquement.

10. **Système de feedback in-app** — Bouton "Signaler un bug" qui capture automatiquement les infos système, les logs récents, et ouvre un formulaire pré-rempli (GitHub Issue ou form custom).

11. **Rate limiting sur la télémétrie** — Si l'app crash en boucle, ne pas envoyer 1000 rapports identiques. Grouper et limiter.

12. **Feature flags** — Pouvoir activer/désactiver des features à distance (via un JSON sur GitHub) sans pousser une mise à jour. Utile pour kill switches sur des features bugées.

13. **Deadlock detection** — L'audio COM threading est piégeux. Détecter les deadlocks et forcer un recovery plutôt que de freezer.

14. **Session 0 isolation** — L'app ne doit pas crasher si elle tourne en tant que service ou dans une session déconnectée (RDP).

15. **Performance budget** — Définir des limites : <30MB RAM, <1% CPU idle, <5% CPU active, startup <2s. Monitorer automatiquement.

### Nice-to-have (si le temps le permet)

16. **A/B testing framework** — Tester deux versions d'une feature et mesurer laquelle performe mieux.

17. **Plugin/Addon marketplace** — Un mini-store in-app pour des extensions tierces.

18. **Theming API** — Permettre aux devs de créer des thèmes avancés en XAML/JSON.

19. **CLI** — ~~Déjà promu en Phase 7 ! Voir la section complète ci-dessus.~~

20. **Profiling mode** — Mode caché (Ctrl+Shift+P) qui affiche les stats de perf en overlay.

21. **Système de backup/sync** — Sauvegarder les settings dans le cloud (GitHub Gist, OneDrive).

22. **Système de changelog in-app** — "Quoi de neuf dans v3.1" avec slides sexy à chaque mise à jour.

23. **Defensive shutdown** — Si l'app détecte qu'elle consomme >200MB RAM ou >20% CPU, elle se restart automatiquement.

24. **Compatibility mode** — Détection de conflits avec d'autres apps audio (Voicemeeter, SoundSwitch, etc.) et suggestions de config.

25. **Raccourcis Windows 11** — Support des raccourcis de la barre des tâches Win11 (jump list avec actions rapides).

---

## 12. Stack technique

### Framework (ON NE CHANGE PAS)
- **.NET Framework 4.6.2** — c'est ce qu'on a, ça marche, on ne migre pas
- **WPF** — le UI framework existant
- **x86** — la target existante
- **VS2022, MSBuild, Debug|x86** — le build existant

### Librairies à AJOUTER (via NuGet, dans le packages.config existant)
| Besoin | Librairie | Raison |
|--------|-----------|--------|
| Crash reporting | **Sentry 4.x** (.NET FW compatible) | Remplace Bugsnag mort |
| JSON | **Newtonsoft.Json 13.0.2** (déjà là) | On garde |
| Auto-update | **Custom** (HttpClient + GitHub API) | Pas de dépendance |

### Librairies existantes qu'on GARDE
| Package | Version | Status |
|---------|---------|--------|
| Newtonsoft.Json | 13.0.2 | Garde |
| XamlAnimatedGif | 2.1.0 | Garde |
| GitVersionTask | 5.5.1 | Garde |

### Librairies à SUPPRIMER
| Package | Raison |
|---------|--------|
| Bugsnag | 2.2.0 | Remplacé par Sentry |
| Bugsnag.ConfigurationSection | 2.2.0 | Inutile |

### Télémétrie (Sentry)
- **Plan gratuit :** 5K events/mois, 1 user, 30 jours rétention
- **Plan Team :** $26/mois, 50K events, 90 jours rétention
- **Self-hosted :** GlitchTip (gratuit, Docker) ou Sentry on-premise

---

## 13. Timeline estimée (ORDRE OPTIMISÉ)

> Chaque phase part du code qui marche. On build et on teste entre chaque étape.
> Si ça casse → on revert, on comprend pourquoi, on refait proprement.
>
> **⚠️ L'ordre ci-dessous est l'ordre d'EXÉCUTION (pas l'ordre des sections).**

### Étape 1 : Fixes & Cleanup → §3 (1-2 jours)
- [ ] Supprimer le software rendering (1 ligne)
- [ ] Cleanup code mort (Bugsnag, Features.cs, empty catches)
- [ ] Déduplicer color parsing dans AppSettings
- [ ] Fixer les liens Welcome → BetterTrumpet
- [ ] Fixer Winget manifest (x64 → x86)
- [ ] Fixer Chocolatey (ajouter raccourcis)
- [ ] Ajouter mode portable (JsonFileSettingsBag + portable.marker)
- [ ] **BUILD + TEST : l'app se lance et marche comme avant**

### Étape 2 : Qualité & Fiabilité → §7 (1-2 jours)
> Error handling et logging AVANT d'ajouter des features = toutes les features sont couvertes automatiquement.
- [ ] Gestion d'erreurs globale (AppDomain, TaskScheduler, Dispatcher)
- [ ] Startup robuste en 3 phases (Core → UI → Features)
- [ ] Logging structuré avec rotation (5 fichiers x 5MB)
- [ ] Health monitoring (memory, CPU, handle leaks)
- [ ] **BUILD + TEST : un crash affiche un dialog user-friendly**

### Étape 3 : Télémétrie Sentry → §5 (1-2 jours)
> Se branche sur le error handling de l'étape 2. Crash reporting dès le début du dev.
- [ ] NuGet : ajouter Sentry SDK, supprimer Bugsnag
- [ ] Modifier `ErrorReporter.cs` : init Sentry au lieu de Bugsnag
- [ ] Consentement GDPR (toggle dans Settings, pas encore dans onboarding)
- [ ] Crash reporting automatique
- [ ] **BUILD + TEST : un crash envoie bien un rapport sur Sentry**

### Étape 4 : Profils & Moteur de Règles → §10.2 (3-5 jours)
> Le cœur de v3. Le CLI, la Command Palette, le Mini Mode en dépendent.
- [ ] Nouveaux fichiers `AudioProfile.cs`, `AudioProfileManager.cs`, `RuleEngine.cs`
- [ ] Ajouter settings dans `AppSettings.cs` : ProfilesJson, RulesJson
- [ ] Profils built-in (Chill, Gaming, Meeting, Night, Focus, Cinema)
- [ ] Moteur de règles : déclencheurs temps, app, device, système
- [ ] UI dans SettingsWindow.xaml : page de gestion profils + règles
- [ ] Notifications "Profil X activé automatiquement"
- [ ] **BUILD + TEST : créer un profil, l'activer, vérifier que les volumes changent**

### Étape 5 : CLI + Named Pipe IPC → §9 (2-3 jours)
> L'IPC sert au CLI, à la Command Palette ET au plugin Raycast. Expose les profils.
- [ ] Nouveau fichier `CliHandler.cs` : parser les args dans `OnAppStartup`
- [ ] Nouveaux fichiers `PipeServer.cs` + `PipeClient.cs` : Named Pipe IPC
- [ ] Modifier `App.xaml.cs` : si args → CLI mode, sinon → GUI normal
- [ ] Modifier `SingleInstanceAppMutex.cs` : si 2e instance avec args → pipe la commande
- [ ] Commandes : --list-devices, --list-apps, --set, --set-volume, --mute, --json
- [ ] Commandes profils : --save-profile, --load-profile, --list-profiles
- [ ] Fuzzy matching pour noms
- [ ] **BUILD + TEST : `BetterTrumpet.exe --list-devices` affiche les devices**

### Étape 6 : Undo/Redo + Command Palette + Keyboard Nav → §10.3-10.4 (2-3 jours)
> Utilisent les profils (étape 4) + le pipe IPC (étape 5).
- [ ] Undo/Redo volume : historique dans un `Stack<VolumeAction>` dans App.xaml.cs
- [ ] Nouveau fichier `CommandPaletteWindow.xaml` : Win+Shift+V, fuzzy search profils/apps
- [ ] Keyboard navigation (optionnel dans Settings, mode Power User)
- [ ] **BUILD + TEST : Ctrl+Z undo un changement de volume, Command Palette trouve un profil**

### Étape 7 : Mini Mode PiP + Notifications → §10.5-10.6 (1-2 jours)
> Polish UI, affichent le profil actif et les événements des règles.
- [ ] Nouveau fichier `MiniModeWindow.xaml` : PiP widget flottant, snap aux bords
- [ ] Notifications contextuelles (toasts natifs ou custom flyout)
- [ ] **BUILD + TEST : Mini Mode affiche les apps actives avec le profil en cours**

### Étape 8 : Onboarding Premium → §4 (2-3 jours)
> À la FIN : présente TOUTES les features (profils, thèmes, télémétrie, raccourcis).
- [ ] Nouveau fichier `OnboardingWindow.xaml` + VM (dans UI/Views et UI/ViewModels)
- [ ] 5 écrans avec transitions slide (Storyboard WPF natif)
- [ ] Preview thème live
- [ ] Capture de raccourcis interactive
- [ ] Toggle télémétrie Sentry (connecté au opt-in de l'étape 3)
- [ ] Modifier `App.xaml.cs` : remplacer `DisplayFirstRunExperience()` → ouvrir Onboarding
- [ ] **BUILD + TEST : l'onboarding s'affiche au premier lancement, toutes les options marchent**

### Étape 9 : Packaging → §6 (2-3 jours)
> On package quand TOUT est stable et testé.
- [ ] Nouveau fichier `UpdateChecker.cs` : check GitHub Releases API toutes les 24h
- [ ] Notification tray "Nouvelle version disponible"
- [ ] Inno Setup script pour version installeur (nouveau fichier, pas dans le .sln)
- [ ] Build portable = ZIP de l'exe + portable.marker + config/
- [ ] Corriger Winget + Choco pour la v3
- [ ] Corriger le CI GitHub Actions pour les 2 variantes
- [ ] **BUILD + TEST : l'auto-update détecte une version plus récente**

### Étape 10 : Plugin Raycast (bonus, 1-2 jours)
- [ ] Projet séparé (pas dans le .sln) : extension Raycast TypeScript
- [ ] Communique via Named Pipe IPC (même infra que CLI, étape 5)
- [ ] Publication sur Raycast Store

### Étape 11+ : Features archivées (quand le temps le permet)
- [ ] Streamer Mode, ChatMix, Ducking, Spaces, Game Bar widget, etc.

**Total estimé : 16-26 jours de développement**

---

## Prochaines étapes immédiates

1. ~~**Décider** : Sentry (hosted)~~ → **DÉCIDÉ : Sentry hosted (gratuit 5K events/mois)**
2. ~~**Décider** : Inno Setup ou WiX~~ → **À décider à l'étape 9**
3. **Commencer Étape 1** : fixes rapides sur le code existant, build, test
4. **Chaque étape se termine par BUILD + TEST** avant de passer à la suite

---

*Ce plan est vivant. On l'ajuste au fur et à mesure. Dernière mise à jour : ordre d'exécution optimisé (profils avant CLI, onboarding à la fin).*
