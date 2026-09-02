# Publier une release BetterTrumpet

Ce guide est la procedure de release publique de BetterTrumpet. Il couvre
GitHub, Chocolatey, Winget et le Microsoft Store sans melanger leurs artefacts.

Une release est construite depuis le tag. Le tag doit donc etre pose **avant**
de produire les binaires. Ne jamais faire une release depuis un commit local
non identifie.

## Canaux et artefacts

| Canal | Artefact | Mise a jour |
| --- | --- | --- |
| GitHub Releases | setup Inno + ZIP portable + checksums | GitHub updater pour les installations non packagees |
| Chocolatey | package qui telecharge le setup GitHub | checksum SHA256 du setup GitHub |
| Winget | manifeste qui pointe vers le setup GitHub | checksum SHA256 du setup GitHub |
| Microsoft Store | `.msixupload`/MSIX bundle | Microsoft Store uniquement |

Le Store est une mise a jour de l'application Partner Center existante, pas une
nouvelle application. Son produit est `9PKBH40D32G8`, avec l'identite :

```text
Name:      xammen.Bettertrumpet
Publisher: CN=7EDFC72A-8780-4841-8F34-30B45D719EAF
```

Les installations Store ne doivent jamais utiliser le programme de mise a jour
GitHub/Inno. Inversement, le setup, Chocolatey et Winget ne sont pas des
packages Store.

## 1. Preparer la version

Remplacer `X.Y.Z` par la version cible et partir de `master`. Ne pas synchroniser
ni pousser `migration/net8` : cette branche est un ancetre historique.

```powershell
git fetch --prune origin
git status --short
git log --oneline origin/master..HEAD
```

Verifier que les seuls changements inclus sont bien ceux de la release. Le depot
peut contenir du travail utilisateur non lie : ne pas utiliser `git add -A`.

Mettre a jour les sources de version suivantes :

- `GitVersion.yml` : `next-version: X.Y.Z`
- `installer.iss` : `#define AppVersionStr "X.Y.Z"` uniquement; `AppVersion`,
  `AppVerName`, les trois `OutputName`, `VersionInfoVersion` (`X.Y.Z.0`) et
  `VersionInfoProductVersion` en decoulent
- `release.ps1` : `$Version`
- `build-portable.ps1` : `$Version` (dossier et nom du ZIP)
- `chocolatey/bettertrumpet.nuspec` : version et URL des notes
- `chocolatey/tools/chocolateyInstall.ps1` : `$version` (les URL en decoulent);
  les trois checksums viennent apres le build
- `winget-manifest/xmn.BetterTrumpet*.yaml` et
  `winget-manifest/manifests/x/xmn/BetterTrumpet/X.Y.Z/` : les trois manifestes
  de staging et les trois manifestes canoniques (version, installer, locale);
  le checksum vient apres le build
- `release-notes-X.Y.Z.md` et, apres le build, `release-checksums-X.Y.Z.txt`
- `EarTrumpet.Package/Package.appxmanifest` : `Version="X.Y.Z.0"`, seulement si
  la release passe aussi par le Store

Une recherche avant le commit permet de trouver les anciennes valeurs qui
resteraient dans les fichiers de release :

```powershell
rg -n "3\.3\.1|X\.Y\.Z" GitVersion.yml installer.iss release.ps1 build-portable.ps1 `
  chocolatey winget-manifest EarTrumpet.Package release-notes-*.md
```

Adapter l'ancienne version dans la commande ci-dessus. Verifier aussi que le
manifeste Store conserve exactement le `Name` et le `Publisher` ci-dessus.

## 2. Ecrire les notes

Les notes GitHub sont en anglais, factuelles, et ecrites comme par un humain.
Un ton legerement casual est bienvenu, mais le lecteur doit pouvoir identifier
les changements et correctifs en un coup d'oeil. Mentionner les issues et PR
qui ont motive un changement quand cela aide a comprendre le contexte.

Format de base :

```markdown
## BetterTrumpet vX.Y.Z

A short human intro, when it has something useful to say.

### What changed
- **Feature or fix** - Clear user-facing description. Thanks to @contributor.

### Fixes
- **Specific problem** - What users can now expect, with a link to #123 if useful.
```

Ne pas laisser GitHub generer des notes automatiques a partir des commits sans
relecture. Les commits sont la source, pas le texte final.

## 3. Committer et tagger la source de release

Ajouter explicitement les fichiers prepares, puis creer un tag annote. Le build
realise ensuite sur ce tag obtient la bonne version GitVersion.

```powershell
git add GitVersion.yml installer.iss release.ps1 build-portable.ps1 `
  chocolatey/bettertrumpet.nuspec chocolatey/tools/chocolateyInstall.ps1 `
  winget-manifest/xmn.BetterTrumpet.yaml `
  winget-manifest/xmn.BetterTrumpet.installer.yaml `
  winget-manifest/xmn.BetterTrumpet.locale.en-US.yaml `
  winget-manifest/manifests/x/xmn/BetterTrumpet/X.Y.Z/ `
  release-notes-X.Y.Z.md

# Ajouter Package.appxmanifest seulement pour une release Store.
# git add EarTrumpet.Package/Package.appxmanifest

git commit -m "chore: prepare X.Y.Z release"
git tag -a vX.Y.Z -m "BetterTrumpet X.Y.Z"
git push origin master
git push origin vX.Y.Z
```

Si la release ne comprend pas le Store, omettre son manifeste du `git add`.
Avant de continuer, confirmer que `git show vX.Y.Z` pointe vers le commit attendu.

`release.ps1` inverse cet ordre : il construit d'abord et ne tagge qu'apres avoir
ecrit les checksums, de sorte que le commit tagge les contienne deja. Les deux
ordres produisent les memes binaires : `GitVersion.yml` fixe `next-version` et
formate chaque champ de version en `{MajorMinorPatch}`.

## 4. Construire et verifier les artefacts GitHub

`EarTrumpet.csproj` est au format SDK : `dotnet build` suffit et restaure tout
seul. Ne pas construire la solution pour x64/arm64 : `EarTrumpet.ColorTool` et
`EarTrumpet.Package` sont x86 uniquement et en sont exclus. Remplacer `x86` par
`x64` ou `arm64` dans les trois commandes; le dossier de sortie est choisi par
le fichier projet (`Build\Release`, `Build\Release-x64`, `Build\Release-arm64`).

```powershell
dotnet build EarTrumpet\EarTrumpet.csproj --no-incremental -c Release -p:Platform=x86

powershell -ExecutionPolicy Bypass -File build-portable.ps1 -Arch x86
& "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe" /DArch=x86 installer.iss

[System.Diagnostics.FileVersionInfo]::GetVersionInfo('Build\Release\BetterTrumpet.exe') |
  Select-Object FileVersion, ProductVersion
```

Le resultat doit afficher `X.Y.Z` pour les deux versions. Le build `Release|x86`
doit rester self-contained; une invite demandant le runtime .NET x86 est un echec
de packaging. Fermer une instance BetterTrumpet qui verrouillerait `Build\Release`
avant de reconstruire.

Puis verifier l'existence des artefacts et calculer les hashes **apres toute
eventuelle signature** :

```powershell
$setup = 'dist\BetterTrumpet-X.Y.Z-setup.exe'
$portable = 'dist\BetterTrumpet-X.Y.Z-portable.zip'

Get-Item $setup, $portable | Select-Object Name, Length
Get-FileHash $setup, $portable -Algorithm SHA256
```

Copier ces deux lignes dans `release-checksums-X.Y.Z.txt`, avec le nom de fichier
exact. Ne jamais remplacer un asset GitHub deja publie pour une meme version :
le checksum Chocolatey et Winget deviendrait immediatement faux.

## 5. Finaliser les metadonnees qui dependent des hashes

Reporter le hash du setup dans :

- `chocolatey/tools/chocolateyInstall.ps1` (`checksum`)
- `winget-manifest/.../xmn.BetterTrumpet.installer.yaml` (`InstallerSha256`)
- `release-checksums-X.Y.Z.txt` (setup et portable)

Verifier les URL et le hash sans approximation :

```powershell
Get-Content chocolatey/tools/chocolateyInstall.ps1
Get-Content winget-manifest/manifests/x/xmn/BetterTrumpet/X.Y.Z/xmn.BetterTrumpet.installer.yaml
Get-Content release-checksums-X.Y.Z.txt
```

Commiter ces metadonnees explicitement et pousser `master`. Ce commit peut etre
apres le tag : il ne modifie pas le binaire produit par le tag.

```powershell
git add chocolatey/tools/chocolateyInstall.ps1 `
  winget-manifest/xmn.BetterTrumpet.yaml `
  winget-manifest/xmn.BetterTrumpet.installer.yaml `
  winget-manifest/xmn.BetterTrumpet.locale.en-US.yaml `
  winget-manifest/manifests/x/xmn/BetterTrumpet/X.Y.Z/ `
  release-checksums-X.Y.Z.txt
git commit -m "chore: finalize X.Y.Z distribution metadata"
git push origin master
```

## 6. Publier GitHub, Chocolatey et Winget

Creer la release GitHub avec les deux artefacts et le fichier de checksums :

```powershell
gh release create vX.Y.Z `
  dist/BetterTrumpet-X.Y.Z-setup.exe `
  dist/BetterTrumpet-X.Y.Z-portable.zip `
  release-checksums-X.Y.Z.txt `
  --title "BetterTrumpet X.Y.Z" `
  --notes-file release-notes-X.Y.Z.md
```

Telecharger ensuite le setup depuis la page GitHub et verifier que son SHA256 est
encore celui utilise dans Chocolatey et Winget. GitHub est la source canonique
des deux installateurs.

Construire et publier Chocolatey uniquement apres la mise en ligne GitHub :

```powershell
Push-Location chocolatey
choco pack
choco push "bettertrumpet.X.Y.Z.nupkg" --source https://push.chocolatey.org/
Pop-Location
```

L'API key Chocolatey ne doit jamais etre ajoutee au depot, a un script, ou a une
note de release. Utiliser une valeur locale ephemere lors du `choco push`.

Pour Winget, valider les trois manifestes du dossier canonique puis ouvrir une
PR vers `microsoft/winget-pkgs`. La PR ne doit contenir que :

```text
manifests/x/xmn/BetterTrumpet/X.Y.Z/
```

```powershell
winget validate --manifest winget-manifest/manifests/x/xmn/BetterTrumpet/X.Y.Z
```

## 7. Publier la mise a jour Microsoft Store

Cette partie fabrique un package Store distinct. Il faut les composants UWP/MSIX
de Visual Studio, `Microsoft.DesktopBridge.props/targets` et le SDK Windows avec
`MakeAppx.exe`. Ne jamais ajouter de PFX prive dans le depot : le projet desactive
la signature locale et Partner Center signe la publication Store.

Le manifeste doit monter strictement, par exemple `3.2.1.0` vers `3.2.2.0`.
Conserver les anciens packages et construire dans un nouveau dossier :

```powershell
New-Item -ItemType Directory -Force artifacts/store/X.Y.Z | Out-Null

& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' `
  EarTrumpet.Package\EarTrumpet.Package.wapproj `
  /p:Configuration=Release `
  /p:Platform=x86 `
  /p:Channel=Store `
  /p:AppxBundle=Always `
  /p:AppxBundlePlatforms=x86 `
  /p:AppxPackageSigningEnabled=false `
  /p:UapAppxPackageBuildMode=StoreUpload `
  /p:AppxPackageDir=..\artifacts\store\X.Y.Z\ `
  /t:Rebuild `
  /v:minimal

Get-ChildItem artifacts/store/X.Y.Z -Recurse -Filter '*.msixupload'
Get-FileHash artifacts/store/X.Y.Z\*.msixupload -Algorithm SHA256
```

`/p:Channel=Store` est obligatoire. Sans lui, GitVersion peut ajouter le nombre
de commits et produire une version Store invalide, par exemple `3.2.1.3` au lieu
de `3.2.1.0`.

Dans Partner Center :

1. Ouvrir le produit existant `9PKBH40D32G8`.
2. Creer une nouvelle soumission pour ce produit, sans reserver une nouvelle application.
3. Importer le nouveau `.msixupload` et verifier que Partner Center accepte `X.Y.Z.0`.
4. Copier des notes de release courtes et relues, puis garder la publication
   automatique apres certification sauf besoin explicite de controle manuel.
5. Soumettre a certification et noter l'identifiant de soumission, le statut et
   le SHA256 de l'upload dans `AGENTS.md`.

Le warning `runFullTrust` est attendu pour cette application; il reste soumis a
l'approbation de Microsoft. Ne pas contourner ce warning en modifiant les
capacites sans validation fonctionnelle complete.

## 8. Verification apres publication

- Verifier la page GitHub, les trois assets et les hashes telecharges.
- Installer ou mettre a jour avec le setup et avec le ZIP portable sur une
  machine/test propre; le portable doit contenir `portable.marker`.
- Verifier le package Chocolatey (`choco info bettertrumpet`) apres la moderation.
- Suivre les checks et la moderation de la PR Winget.
- Suivre le statut de certification dans Partner Center et tester la mise a jour
  depuis une installation Store de la version precedente.
- Mettre `AGENTS.md` a jour avec les tags, hashes, URLs et etat des soumissions.

Ne modifiez pas les assets d'une version deja distribuee. Pour corriger un
artefact public, publier une nouvelle version et reprendre la procedure.
