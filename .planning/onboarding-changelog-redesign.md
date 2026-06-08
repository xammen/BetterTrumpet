# Refonte Onboarding + Changelog — Design Natif Windows 11

**Objectif :** Supprimer le "AI slop", créer un design propre, minimaliste, natif Windows 11, avec zéro animation décorative inutile.

---

## 🔴 Problèmes actuels identifiés

### Onboarding (950 lignes)
- ❌ **Trop d'animations** : splash screen avec particles, logo breathing, floating preview, bars animées, shimmer, confetti
- ❌ **Pas Windows 11** : gradient mesh custom, palette hardcodée, cards avec styles custom
- ❌ **Complexité excessive** : 6 pages avec des effets partout, code spaghetti
- ❌ **Confetti tape-à-l'œil** : pas premium, trop « gamifié »
- ⚠️ **Points positifs** : navigation claire, accessibilité correcte, structure ViewModel propre

### Changelog (438 lignes)
- ❌ **Parsing markdown manuel** : CreateSectionCard, AddBulletRow, ParseInlineMarkdown — fragile
- ❌ **Pas natif** : ne ressemble pas à une dialog Windows moderne
- ❌ **Shimmer artificiel** : gradient animé sur le titre, inutile
- ⚠️ **Points positifs** : fetch GitHub API, structure simple

---

## ✅ Approche de refonte

### Principes de design

1. **Windows 11 natif** : utiliser le système de thème existant (`Theme:Brush`, `Theme:Options`)
2. **Minimalisme** : supprimer toutes les animations décoratives, garder uniquement les transitions de pages
3. **Clarté** : hiérarchie visuelle simple, typographie clean, espaces généreux
4. **Performance** : moins de code, moins d'animations, moins de complexité
5. **Cohérence** : utiliser les mêmes patterns que le reste de l'app (FlyoutWindow, SettingsWindow)

### Design System

**Couleurs** : utiliser le système de thème existant au lieu de hardcoder
```xaml
<!-- Avant (hardcodé) -->
<Color x:Key="AccentColor">#3B9EFF</Color>
<Color x:Key="BgColor">#FF101014</Color>

<!-- Après (thème système) -->
<SolidColorBrush x:Key="AccentBrush" Theme:Brush="Accent" Theme:Options.Source="App"/>
<SolidColorBrush x:Key="BackgroundBrush" Theme:Brush="Background" Theme:Options.Source="App"/>
```

**Typographie** : suivre les guidelines Windows 11
- Titre principal : 28px, SemiBold
- Section : 20px, SemiBold
- Body : 14px, Regular
- Caption : 12px, Regular

**Espacement** : multiples de 4
- Marges extérieures : 32px
- Espacement entre sections : 24px
- Espacement entre éléments : 12px

**Composants réutilisables** :
- Card simple avec arrondi 8px, bordure subtile
- Radio/Checkbox Windows 11 style
- Bouton accent propre (sans hover tape-à-l'œil)

---

## 📐 Structure de la refonte

### 1. Onboarding simplifié (4 pages au lieu de 6)

**Page 0 : Welcome** (fusionner avec l'intro)
- Logo centré, simple
- Titre "Welcome to BetterTrumpet"
- Sous-titre court (1-2 lignes)
- Version badge discret
- ❌ Supprimer : splash, particles, floating preview, bars animées, counter, typing effect

**Page 1 : Setup** (fusionner Audio + Appearance)
- Section "Default Audio Device" : liste simple avec radio buttons
- Section "Appearance" : 2 cards côte à côte (System / Custom)
- ❌ Supprimer : animations des bars dans les previews

**Page 2 : Privacy** (garder tel quel, mais simplifier)
- Trust badges (No Ads, No Tracking, No Selling)
- Toggle Telemetry
- Toggle Run at Startup
- Radio buttons Update Channel
- ❌ Supprimer : reassurance dialog (juste un texte inline)

**Page 3 : Ready** (remplacer Tray Pin)
- "You're all set!" avec checklist simple
- Bouton "Get Started"
- ❌ Supprimer : confetti, GIF, animations de checklist

**Bénéfices :**
- 4 pages au lieu de 6 → plus rapide
- ~400 lignes de code au lieu de 950
- Zéro animation décorative
- Design épuré, lisible, natif

### 2. Changelog natif

**Structure simplifiée :**
- Header avec version badge
- ScrollViewer avec contenu markdown → XAML
- Bouton "Continue" simple

**Amélioration du parsing :**
- Garder le parsing markdown mais le simplifier
- Utiliser des TextBlock avec Inlines au lieu de grids custom
- Supprimer le shimmer du titre
- Cascade d'apparition plus subtile (ou aucune)

**Bénéfices :**
- ~250 lignes au lieu de 438
- Rendu plus propre, moins de code custom
- Performance améliorée

---

## 🛠️ Plan d'implémentation

### Phase 1 : Créer les nouveaux composants réutilisables

**Fichier :** `EarTrumpet/UI/Controls/OnboardingComponents.xaml`

Créer des styles réutilisables :
- `OnboardingCard` : Border avec CornerRadius 8, bordure subtile
- `OnboardingRadioButton` : style Windows 11
- `OnboardingToggle` : CheckBox comme toggle (déjà existe, peut être réutilisé)
- `OnboardingSectionTitle` : TextBlock 20px SemiBold

### Phase 2 : Refactoriser OnboardingWindow.xaml

**Changements structurels :**
1. Supprimer `SplashContainer` et toute la logique splash
2. Supprimer `ConfettiCanvas` et toute la logique confetti
3. Simplifier les 6 pages en 4 pages
4. Fusionner Page0 (Welcome) + Page1 (Audio) → nouvelle Page0 (Welcome simple)
5. Fusionner Audio + Appearance → nouvelle Page1 (Setup)
6. Garder Privacy → Page2
7. Simplifier Ready + TrayPin → Page3 (Ready simple)

**Changements visuels :**
1. Remplacer les couleurs hardcodées par `Theme:Brush`
2. Supprimer le `GradientMesh`
3. Simplifier la barre de progression (juste une barre, pas d'animation fancy)
4. Supprimer tous les effets de glow, shimmer, breathing, floating

### Phase 3 : Nettoyer OnboardingWindow.xaml.cs

**Méthodes à supprimer :**
- `ShowSplashScreen()`
- `AnimateLogoBurst()`
- `LaunchSplashParticles()`
- `StartLogoBreathing()`
- `TransitionToMainContent()`
- `AnimateWelcomePage()`
- `StartTypingEffect()`
- `AnimatePreviewBars()`
- `AnimateUserCounter()`
- `WelcomeLogo_MouseEnter/Leave()` (effet 3D inutile)
- `StartGradientShimmer()`
- `AnimateAppearancePage()`
- `AnimateTrayPinPage()`
- `LaunchConfetti()`
- `SpawnConfettiPiece()`

**Méthodes à simplifier :**
- `AnimatePageEntrance()` : garder un simple slide+fade, 200ms
- `AnimateReadyPage()` : juste un fade-in, pas de cascade complexe
- `UpdateProgressBar()` : animation simple sans easing fancy

**Résultat :**
- ~300 lignes au lieu de 950
- Code lisible, maintenable
- Aucune animation décorative

### Phase 4 : Simplifier OnboardingViewModel.cs

**Changements :**
- `PageCount = 4` au lieu de 6
- Supprimer `IsPage5`
- Ajuster la logique de navigation
- Simplifier `Progress` (4 pages au lieu de 6)

**Pas de changement structurel majeur** : le ViewModel est déjà propre.

### Phase 5 : Refactoriser ChangelogWindow

**Changements XAML :**
1. Supprimer le gradient mesh
2. Simplifier le header
3. Utiliser `Theme:Brush` au lieu de couleurs hardcodées
4. Supprimer les bordures de fade top/bottom (ou les rendre plus subtiles)

**Changements code-behind :**
1. Supprimer `ApplyTitleShimmer()` complètement
2. Simplifier `AnimateContentIn()` : fade simple sans cascade complexe
3. Garder le parsing markdown mais le nettoyer

**Résultat :**
- ~250 lignes au lieu de 438
- Rendu plus propre, plus rapide

---

## 📊 Métriques de succès

| Métrique | Avant | Après | Amélioration |
|----------|-------|-------|--------------|
| Lignes OnboardingWindow.xaml.cs | 950 | ~300 | -68% |
| Lignes ChangelogWindow.xaml.cs | 438 | ~250 | -43% |
| Nombre de pages onboarding | 6 | 4 | -33% |
| Animations décoratives | 15+ | 1-2 | -90% |
| Couleurs hardcodées | 12+ | 0 | -100% |
| Temps de complétion onboarding | ~2min | ~1min | -50% |

---

## 🎨 Mockup (texte)

### Onboarding Page 0 : Welcome
```
┌─────────────────────────────────────────────────┐
│                                           [×]   │
│                                                 │
│                   [Logo 64x64]                  │
│                                                 │
│            Welcome to BetterTrumpet             │
│      Volume control that actually works         │
│                                                 │
│                  [v2.3.9.0]                     │
│                                                 │
│                                                 │
│                                    [Continue →] │
│                      ○ ○ ○ ○                    │
└─────────────────────────────────────────────────┘
```

### Onboarding Page 1 : Setup
```
┌─────────────────────────────────────────────────┐
│                                           [×]   │
│                                                 │
│  🔊 Default Audio Device                        │
│  Choose your preferred playback device          │
│                                                 │
│  ○ Speakers (Realtek High Definition Audio)    │
│  ● Headphones (USB Audio Device)               │
│  ○ Monitor Audio (HDMI)                         │
│                                                 │
│  🎨 Appearance                                  │
│  Choose your theme preference                   │
│                                                 │
│  ┌──────────────────┐  ┌──────────────────┐    │
│  │ System Theme     │  │ Custom Theme     │    │
│  │   [preview]      │  │   [preview]      │    │
│  │ ✓ Selected       │  │                  │    │
│  └──────────────────┘  └──────────────────┘    │
│                                                 │
│  [← Back]                         [Continue →] │
│                      ○ ● ○ ○                    │
└─────────────────────────────────────────────────┘
```

### Changelog
```
┌─────────────────────────────────────────────────┐
│                                           [×]   │
│                                                 │
│  [v2.3.9.0]                                     │
│                                                 │
│  What's New in BetterTrumpet                    │
│  See what's improved in this update             │
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │ ✨ New Features                         │   │
│  │ ────────────────────────────────────    │   │
│  │ • Volume tick sound effect              │   │
│  │ • Smooth expand/collapse animations     │   │
│  │                                         │   │
│  │ 🐛 Bug Fixes                            │   │
│  │ ────────────────────────────────────    │   │
│  │ • Fixed media popup initialization      │   │
│  │ • Improved peak meter performance       │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  Thanks for using BetterTrumpet!                │
│                                    [Continue]   │
└─────────────────────────────────────────────────┘
```

---

## 🚀 Ordre d'exécution

1. ✅ Créer ce document de plan
2. Créer `OnboardingComponents.xaml` avec les styles réutilisables
3. Refactoriser `OnboardingWindow.xaml` (4 pages, design simple)
4. Nettoyer `OnboardingWindow.xaml.cs` (supprimer animations)
5. Ajuster `OnboardingViewModel.cs` (4 pages)
6. Simplifier `ChangelogWindow.xaml` (supprimer gradient/shimmer)
7. Nettoyer `ChangelogWindow.xaml.cs` (supprimer shimmer/cascade)
8. Tester l'onboarding complet
9. Tester le changelog
10. Commit avec message détaillé

---

## 📝 Notes

- **Ne pas toucher** : `WelcomeViewModel.cs` (différent de l'onboarding, semble legacy)
- **Réutiliser** : le toggle style existant dans `OnboardingWindow.xaml` est déjà propre
- **Thème système** : utiliser `Theme:Brush` pour s'intégrer avec le reste de l'app
- **Accessibilité** : garder tous les `AutomationProperties` existants
- **Keyboard nav** : garder toute la navigation au clavier existante
