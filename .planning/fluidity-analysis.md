# BetterTrumpet — Analyse de fluidité & plan « premium »

> Branche de travail : `perf/gpu-dwm-backdrop`
> Objectif : rendre l'app aussi fluide que MagicPods (démarrage, ouverture du flyout, settings, clic droit tray).
> Date : 2026-06-07

---

## TL;DR du diagnostic

La cause racine est structurelle : **MagicPods est une app de composition native (WinUI/DWM, tout sur GPU). BetterTrumpet est une app WPF en fenêtres *layered* à transparence par-pixel (`AllowsTransparency="True"`), ce qui DÉSACTIVE l'accélération matérielle et force le rendu logiciel (CPU).** Tout le reste (animations saccadées, acrylique, blur, ombres) découle de ce choix.

---

## Diagnostic détaillé

### 1. `AllowsTransparency="True"` → rendu software (coupable n°1)
- `FlyoutWindow.xaml:18` et `DialogWindowStyle` dans `App.xaml:556` activent la transparence par-pixel.
- WPF passe alors la fenêtre en `WS_EX_LAYERED` alpha-par-pixel → **tout le rendu de la fenêtre bascule en software, plus de GPU**.
- Conséquence : animation d'entrée (déplacement + fade 165 ms, `WindowAnimationLibrary.cs:16-17`), peak-meters, acrylique, `DropShadowEffect`, `BlurEffect` → tout calculé CPU puis blitté frame par frame.
- Sur HiDPI (150 %), fenêtre 360 px + acrylique repeinte à 30 fps en software = micro-stutter ressenti.

### 2. Démarrage froid : .NET Framework 4.6.2 sans optimisation JIT
- `EarTrumpet.csproj` cible `v4.6.2` (.NET Framework).
- **Aucun `ProfileOptimization` / multicore JIT** (grep = 0 occurrence), pas de ReadyToRun/NGen.
- Le découpage en phases du démarrage est bon (`App.xaml.cs:246-451`) mais le JIT reste séquentiel sur le chemin critique.

### 3. Ouverture du flyout : coûts cumulés synchrones (thread UI)
`FlyoutWindow.xaml.cs:69-88`, à chaque open :
1. `Show()`
2. `EnableAcrylic(...)` → `SetWindowCompositionAttribute` ré-appliqué **à chaque** ouverture (`FlyoutWindow.xaml.cs:77,328`).
3. `PositionWindowRelativeToTaskbar` fait `UpdateLayout()` **PUIS** `LayoutRoot.Measure(infini)` → double passe layout (`FlyoutWindow.xaml.cs:144-145`).
4. Puis l'animation démarre.

### 4. Clic droit tray : tout reconstruit à froid à chaque fois
- `GetTrayContextMenuItems()` (`App.xaml.cs:611-752`) reconstruit tout le menu à chaque clic.
- `ShowContextMenu` (`ShellNotifyIcon.cs:324`) crée un `ContextMenu` WPF neuf avec `HasDropShadow` + `DropShadowEffect` (BlurRadius 16, `App.xaml:694`) + popups `AllowsTransparency`.
- 1er clic droit paie en plus le JIT + 1er popup HWND → lag « première fois ».

### 5. Settings / Full window construites à la demande
- `WindowHolder.cs:42-48` crée la fenêtre au 1er Open.
- `CreateSettingsExperience` (`App.xaml.cs:754`) instancie tout l'arbre VM + parse XAML + JIT synchrone au clic.

### 6. Détails anti-premium
- Tooltip tray fait main (`App.xaml.cs:79-150`) : `Popup` + `Border` + `DropShadowEffect` software.
- Blur live light-dismiss : `VisualBrush` du contenu + `BlurEffect` Gaussian recalculé chaque frame (`FlyoutWindow.xaml:402-413`).
- `Trace.WriteLine` sur chemins chauds (`FlyoutViewModel.cs:393`, `WndProc` tray) — à retirer en release.

### Points déjà bien faits (à préserver)
- Flyout créé une fois puis *cloaké* (`App.xaml.cs:282-283`).
- Peak-meter timer correctement gated à la visibilité (`DeviceCollectionViewModel.cs:357`).
- Démarrage en phases avec features en `Task.Run` (`App.xaml.cs:312-451`).

---

## Plan d'action (priorisé impact/effort)

### Quick wins (faible effort)
- [ ] `ProfileOptimization.SetProfileRoot` + `StartProfile` au tout début du démarrage → −30/−50 % cold start.
- [ ] Acrylique persistant : ne ré-appliquer que si flag/couleur a changé (`FlyoutWindow.xaml.cs:77,328`).
- [ ] Supprimer le double layout (`Measure` OU `UpdateLayout`, pas les deux) `FlyoutWindow.xaml.cs:144-145`.
- [ ] Pré-chauffer menu tray + fenêtre Settings en idle (cloakées) après démarrage.
- [ ] Retirer `Trace.WriteLine` des chemins chauds en release.

### Gains moyens
- [ ] Remplacer `BlurEffect` + `VisualBrush` live du light-dismiss par voile semi-opaque statique.
- [ ] Remplacer `DropShadowEffect` (menu, tooltip) par ombres natives DWM.
- [ ] Construire le `ContextMenu` tray une fois, MAJ uniquement des items dynamiques.

### Chantier de fond — **PLAN 1 (retenu)** : récupérer le GPU + backdrop natif
> C'est là que se trouve ~80 % de la sensation « liquide ».

1. [ ] Migrer `EarTrumpet.csproj` de .NET Framework 4.6.2 → **.NET 8/9** (WPF supporté, bien plus rapide).
2. [ ] Retirer `AllowsTransparency="True"` (flyout d'abord, puis dialogues).
3. [ ] Backdrop natif via DWM : `DWMWA_SYSTEMBACKDROP_TYPE` (Mica/Acrylic Win11) + `DWMWA_WINDOW_CORNER_PREFERENCE`.
4. [ ] Valider l'accélération matérielle retrouvée (animations entrée/sortie GPU).
5. [ ] Fenêtre par fenêtre : flyout → dialogues → settings/full window.

### Alternative (non retenue pour l'instant)
- Plan 2 : héberger le flyout dans une surface `Windows.UI.Composition` (DesktopWindowTarget) pour animations GPU natives. Plus de travail.

---

---

## AUDIT DE FAISABILITÉ .NET 8 (2026-06-07, lecture seule)

### Cible décidée
- **`net8.0-windows10.0.19041.0`** (TFM Windows → projections WinRT automatiques, pas de CsWinRT manuel).
- SDK installé sur la machine : **.NET 9.0.308 uniquement** (pas de SDK 8). Le SDK 9 sait builder une cible net8.0-windows via ref packs (téléchargés au 1er build). Pas bloquant. À garder en tête pour la CI.

### Inventaire du projet
- Format csproj **legacy** (non-SDK), avec **liste explicite de ~430 `<Compile Include>`** + addons inline (`Addons/EarTrumpet.Actions`).
- `packages.config` (16 paquets) → à convertir en `PackageReference`.
- `App.config` : binding redirect Newtonsoft + `AppContextSwitchOverrides` DPI.
- 2e projet : `EarTrumpet.ColorTool` (net462, outil séparé, non critique).
- Packaging : `EarTrumpet.Package.wapproj` (Desktop Bridge / MSIX), x86.
- CI : Azure Pipelines `MSBuild@1` sur le `.wapproj` (2 tasks : Store + Sideload).

### Dépendances WinRT réelles (point structurant n°1)
Le TFM Windows les couvre toutes — fichiers concernés :
| Fichier | API WinRT |
|---|---|
| `DataModel/MediaSessionService.cs` | `GlobalSystemMediaTransportControlsSessionManager` (Windows.Media.Control) |
| `DataModel/Storage/Internal/WindowsStorageSettingsBag.cs` | `ApplicationDataManager.CreateForPackageFamily` (Windows.Management.Core) |
| `UI/Themes/Manager.cs` | `UISettings` (Windows.UI.ViewManagement) |
| `Interop/Helpers/PackageHelper.cs` | `Windows.ApplicationModel.Package` |

→ La référence `Windows.winmd` (lignes 100-106 du csproj) disparaît, remplacée par le TFM.

### Risques identifiés (par criticité)
1. **GitVersionTask 5.5.1** (csproj l.3,615,629) — incompatible SDK-style/.NET moderne. → migrer vers `GitVersion.MsBuild` récent. `GitVersion.yml` déjà présent à la racine.
2. **Conversion csproj** — passer en SDK-style avec glob auto (`**/*.cs`). Vérifié : le glob ne capture que l'arbre sous `EarTrumpet/`, donc les dossiers racine parasites (`bettertrumpet-next/`, `undefined/`, `bettertrumpet-site/`) ne sont PAS concernés. ✅ Bonus : ajout de fichiers par l'autre session = zéro conflit csproj.
3. **XamlAnimatedGif 2.1.0** (net45) — vérifier compat net8 ; sinon bump version.
4. **App.config** — disparaît ; binding redirects automatiques. Surveiller le switch DPI `DoNotScaleForDpiChanges` (comportement DPI différent en .NET moderne — pertinent vu nos calculs `DpiX()/DpiY()` dans le flyout).
5. **Packaging wapproj** — devrait survivre en référençant le projet net8. Alternative moderne possible (`WindowsPackageType=MSIX`) mais on garde le wapproj pour limiter le périmètre.
6. **prebuild.ps1** + target `BeforeBuild` (versioning) — à re-câbler dans le nouveau csproj.
7. **`Resources.resx` + 32 langues** + `Resources.Designer.cs` (PublicResXFileCodeGenerator) — fonctionnent en SDK-style ; éviter double-génération.
8. **PlatformTarget x86** — conserver (`<Platforms>x86</Platforms>` + RID), cohérent avec MSIX x86.

### Dépendances OK sans souci
- Sentry 4.12.1 (netstandard2.0), Newtonsoft.Json 13, System.Text.Json 6 (intégré au runtime en net8 → peut être retiré), MEF (`System.ComponentModel.Composition` dispo en net8).

### Verdict
**Faisable, risque moyen.** Concentré sur : conversion csproj + GitVersion + packaging/CI. Aucun blocage technique de fond — le TFM Windows règle le plus gros point (WinRT). Le code C#/XAML lui-même devrait migrer avec peu ou pas de changements.

### Plan d'exécution séquencé (migration)
1. [ ] Convertir `EarTrumpet.csproj` en SDK-style (`net8.0-windows10.0.19041.0`, glob, PackageReference).
2. [ ] Remplacer GitVersionTask → GitVersion.MsBuild ; re-câbler prebuild/versioning.
3. [ ] Build `dotnet build` local jusqu'au vert (résoudre erreurs API/réf une à une).
4. [ ] Lancer l'app non packagée, valider démarrage + flyout + tray + settings.
5. [ ] Réparer le packaging wapproj (référence net8) + build MSIX local.
6. [ ] Ajuster la CI Azure (UseDotNet task pour SDK).
7. [ ] **Point de bascule** : merge possible ici. Ensuite seulement → étapes backdrop DWM (retrait AllowsTransparency, etc.).
8. [ ] (Optionnel/après) migrer `EarTrumpet.ColorTool`.

---

## Journal de bord
<!-- Consigner ici décisions, mesures avant/après, blocages -->
- 2026-06-07 — Création branche `perf/gpu-dwm-backdrop` + sauvegarde de l'analyse. Décision : attaquer le PLAN 1.
- 2026-06-07 — Audit de faisabilité .NET 8 terminé (lecture seule). Cible : `net8.0-windows10.0.19041.0`. Verdict : faisable, risque moyen. Risques principaux : conversion csproj, GitVersionTask, packaging/CI. SDK 9 seul installé (sait builder net8).
- 2026-06-07 — **Migration .NET 8 : BUILD VERT** sur branche `migration/net8` (partie de master à jour `3a9c6b0b`). Étapes #1, #2, #3 (build local) terminées :
  - `EarTrumpet.csproj` converti en SDK-style. TFM `net8.0-windows10.0.19041.0` + `SupportedOSPlatformVersion=10.0.17763.0` (média GSMTC). Glob auto des .cs (validé : 340=340, 0 orphelin .cs). PackageReference. x86 préservé. OutputPath `Build\Release` préservé (script portable). Backup : `EarTrumpet.csproj.legacy-bak`.
  - `GitVersionTask 5.5.1` → `GitVersion.MsBuild 5.12.0`. Versioning vérifié OK : résout `3.0.13-migration-net8...` + estampille le manifest via prebuild.ps1.
  - Dépendances retirées (in-box net8) : System.Memory/Buffers/Text.Json/ValueTuple/Immutable/Unsafe/AsyncInterfaces/etc. Conservées : Newtonsoft 13, Sentry 4.12.1, XamlAnimatedGif 2.1.0, +System.Management/ComponentModel.Composition 8.0.
  - Corrections de compilation : CA1416 (~400, NoWarn — app Windows-only, calls runtime-gated), WFAC010 (NoWarn — WPF possède le manifest DPI PerMonitorV2), SYSLIB0032 (retiré `[HandleProcessCorruptedStateExceptions]` obsolète dans MediaSessionService.cs).
  - **Fichier orphelin trouvé** : `UI/Controls/SearchBox.xaml` (réf. VM inexistante, sans code-behind, absent du build legacy) → exclu du build (`<Page Remove>`, non supprimé).
  - **`App.config` supprimé** : legacy 100 % (supportedRuntime v4.6.2, switch DPI ignoré en net8, binding redirect inutile). DPI désormais 100 % via App.manifest PerMonitorV2.
  - Résultat : `dotnet build -c Release -p:Platform=x86` → 0 erreur / 0 warning. exe + dll + 32 satellites de langue générés dans `Build\Release`.
  - **RESTE À FAIRE** : #4 valider l'app au runtime (lancement, flyout, tray, settings, DPI) — nécessite test visuel ; #5 packaging MSIX wapproj→net8 ; #6 CI Azure (UseDotNet). NB : la `.sln` référence encore les projets en format legacy — build via `dotnet build EarTrumpet.csproj` pour l'instant.
