import { useDeferredValue, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import {
  Button,
  FluentProvider,
  Input,
  MessageBar,
  MessageBarBody,
  Spinner,
  Text,
  makeStyles,
  mergeClasses,
  tokens,
  webDarkTheme,
  webLightTheme,
} from "@fluentui/react-components";
import {
  ActivityIcon,
  ArrowUpRightIcon,
  BlendIcon,
  GithubIcon,
  HistoryIcon,
  InfoIcon,
  KeyboardIcon,
  ListChecksIcon,
  MenuIcon,
  MouseIcon,
  MusicIcon,
  IndentIncreaseIcon,
  RefreshCwIcon,
  SaveIcon,
  SearchIcon,
  SettingsIcon,
  ShieldCheckIcon,
} from "@animateicons/react/lucide";
import appIcon from "../../Assets/icon.png";
import { DitherField } from "./components/DitherField";
import { SettingsPage } from "./SettingsPages";
import type { HostMessage, SettingKey, SettingsPageDescriptor, SettingsPayload, SettingValue } from "./types";
import "./polish.css";

// ── Design tokens: Sage Glass Control Surfaces ──────────────────────────────
// One accent (violet), one radius scale (14 / 18 / 24), native Windows
// translucency through DWM acrylic behind the window.
const ACCENT = "#9b7bea";
const ACCENT_STRONG = "#b196f1";
const ACCENT_DIM = "rgba(155, 123, 234, 0.16)";

const useStyles = makeStyles({
  // Outer shell: near-black frame around the whole window, as a native app.
  provider: {
    position: "relative", isolation: "isolate", height: "100%", overflow: "hidden",
    backgroundColor: "#050706", color: "#F4F7F5",
  },

  // ── Shell frame: floating sidebar + content surface on a near-black floor ──
  shell: {
    position: "relative", zIndex: 1, display: "grid",
    gridTemplateColumns: "248px minmax(0, 1fr)",
    gap: "8px", padding: "6px 8px 8px",
    height: "100%", minWidth: 0,
    transitionProperty: "grid-template-columns", transitionDuration: "220ms",
    transitionTimingFunction: "cubic-bezier(.33,1,.68,1)",
    "@media (max-width: 680px)": { display: "block" },
  },
  shellCollapsed: {
    gridTemplateColumns: "56px minmax(0, 1fr)",
  },

  // ── Sidebar: quiet near-black rail, reads as a native app panel ──
  sidebar: {
    display: "flex", minHeight: 0, flexDirection: "column",
    padding: "8px 8px 6px", backgroundColor: "#050706", borderRadius: "18px",
    "@media (max-width: 680px)": { display: "none" },
  },
  sidebarCollapsed: { overflow: "hidden", minWidth: 0, padding: "8px 8px 6px" },
  // Vertical budget at the default 598px window: header 36 + search 40 +
  // footer 41 leaves ~451px for the nav, which holds all 11 pages (~448px).
  sidebarHeader: { display: "flex", alignItems: "center", gap: "10px", minHeight: "32px", padding: "0 2px", marginBottom: "4px", flexShrink: 0 },
  sidebarHeaderCollapsed: { padding: 0 },
  wordmark: { display: "block", overflow: "hidden", whiteSpace: "nowrap", fontSize: "14px", fontWeight: tokens.fontWeightSemibold, letterSpacing: "-0.01em", lineHeight: "1.2", color: "#F4F7F5" },
  navLabel: { display: "inline-block", overflow: "hidden", whiteSpace: "nowrap", verticalAlign: "middle" },
  navLabelCollapsed: { opacity: 0, pointerEvents: "none" },
  logo: { width: "26px", height: "26px", objectFit: "contain", flexShrink: 0, filter: "drop-shadow(0 1px 5px rgba(0,0,0,.45))", transitionProperty: "filter, transform", transitionDuration: "240ms", pointerEvents: "none" },
  logoButton: {
    display: "grid", placeItems: "center", flexShrink: 0, width: "40px", height: "32px", padding: 0,
    border: "none", backgroundColor: "transparent", cursor: "pointer", borderRadius: "14px",
    transitionProperty: "background-color", transitionDuration: "160ms",
    ":hover": { backgroundColor: "rgba(255,255,255,.06)" },
    ":focus-visible": { outline: `2px solid ${ACCENT}`, outlineOffset: "2px" },
  },
  sidebarToggle: {
    marginLeft: "auto", flexShrink: 0, minWidth: "32px", width: "32px", height: "32px", padding: 0,
    color: "rgba(244,247,245,.6)", borderRadius: "14px", zIndex: 21,
    transitionProperty: "color, background-color, transform", transitionDuration: "180ms",
  },
  search: { marginBottom: 0, flexShrink: 0 },
  searchBox: {
    display: "flex", alignItems: "center", gap: "8px",
    height: "32px", padding: "0 10px 0 12px", borderRadius: "14px",
    backgroundColor: "rgba(255,255,255,.07)", border: "1px solid rgba(255,255,255,.12)",
    transitionProperty: "background-color", transitionDuration: "150ms",
    ":hover": { backgroundColor: "rgba(255,255,255,.09)" },
    ":focus-within": { backgroundColor: "rgba(255,255,255,.07)" },
  },
  searchBoxIcon: { color: "rgba(244,247,245,.5)", flexShrink: 0 },
  searchInput: {
    flex: 1, minWidth: 0, height: "100%", padding: 0,
    backgroundColor: "transparent", border: "none", outline: "none", boxShadow: "none",
    color: "#F4F7F5", fontSize: "13px", fontFamily: "inherit",
  },
  searchWrap: { position: "relative", zIndex: 30, flexShrink: 0 },
  searchResults: {
    position: "absolute", top: "100%", left: 0, right: 0, marginTop: "6px",
    display: "grid", gap: "2px", padding: "6px",
    border: "1px solid rgba(255,255,255,.12)", borderRadius: "18px",
    backgroundColor: "#0b0d0c",
    boxShadow: "0 18px 44px rgba(5,7,6,.65), inset 0 1px 0 rgba(255,255,255,.06)",
    maxHeight: "46vh", overflowY: "auto", overscrollBehavior: "contain",
    "@media (prefers-reduced-transparency: reduce)": { backgroundColor: "#12140f" },
  },
  searchResult: {
    display: "flex", flexDirection: "column", alignItems: "flex-start", gap: "2px",
    width: "100%", textAlign: "left", padding: "8px 10px", cursor: "pointer",
    border: "1px solid transparent", borderRadius: "14px",
    color: "rgba(244,247,245,.82)", backgroundColor: "transparent",
    transitionProperty: "background-color, border-color, color", transitionDuration: "140ms", transitionTimingFunction: "cubic-bezier(.33,1,.68,1)",
  },
  searchResultActive: { backgroundColor: "rgba(155,123,234,.14)", border: "1px solid rgba(155,123,234,.28)", color: "#F4F7F5" },
  searchResultLabel: { display: "block", width: "100%", fontSize: "13px", fontWeight: tokens.fontWeightSemibold, lineHeight: 1.35, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
  searchResultMeta: { display: "block", width: "100%", fontSize: "11px", color: "rgba(244,247,245,.48)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" },
  searchHidden: { visibility: "hidden", height: 0, marginBottom: 0, pointerEvents: "none" },
  navWrap: { position: "relative", flex: "1 1 auto", minHeight: 0, display: "flex", flexDirection: "column" },
  // Scrollbar visibility lives in polish.css (`.sidebar-polished nav`): hidden
  // at rest, a thin thumb on hover as a fallback for very short windows.
  nav: { position: "relative", flex: "1 1 auto", minHeight: 0, overflowY: "auto", overscrollBehavior: "contain" },
  navFade: {
    position: "absolute", left: 0, right: 0, bottom: 0, zIndex: 2, height: "40px", pointerEvents: "none",
    backgroundImage: "linear-gradient(180deg, rgba(5,7,6,0) 0%, #050706 88%)",
    opacity: 0, transitionProperty: "opacity", transitionDuration: "180ms",
  },
  navFadeVisible: { opacity: 1 },
  category: { display: "flex", flexDirection: "column", gap: "2px", marginBottom: "6px", ":last-child": { marginBottom: 0 } },
  categoryTitle: { display: "block", padding: "0 12px", color: "rgba(244,247,245,.46)", fontSize: "10.5px", lineHeight: "14px", fontWeight: tokens.fontWeightSemibold, whiteSpace: "nowrap", overflow: "hidden", transitionProperty: "opacity", transitionDuration: "180ms" },
  categoryTitleCollapsed: { opacity: 0, height: 0, padding: 0, margin: 0, overflow: "hidden" },
  navItem: { position: "relative" },
  // Shared sliding pill behind the active item (motion layoutId). Neutral
  // capsule on purpose: navigation state is not the violet accent.
  navPill: { position: "absolute", inset: 0, zIndex: 0, pointerEvents: "none", backgroundColor: "rgba(255,255,255,.10)" },
  navButton: {
    position: "relative", zIndex: 1, width: "100%", height: "32px", minHeight: "32px", justifyContent: "flex-start",
    paddingLeft: "12px", paddingRight: "12px", overflow: "hidden", whiteSpace: "nowrap",
    borderRadius: "14px", fontWeight: tokens.fontWeightRegular,
    color: "rgba(244,247,245,.64)",
    transitionProperty: "background-color, color, transform", transitionDuration: "160ms",
  },
  navButtonCollapsed: {
    width: "100%", height: "32px",
    padding: 0, paddingLeft: "10px", paddingRight: 0, paddingInline: 0,
    minWidth: 0,
    overflow: "hidden",
    alignItems: "center",
    "& .fui-Button__icon": { margin: 0 },
  },
  navIcon: { display: "inline-grid", flex: "0 0 20px", width: "20px", height: "20px", placeItems: "center", marginRight: "13px", color: "inherit", overflow: "hidden", transitionProperty: "margin, transform", transitionDuration: "200ms", "& > svg": { display: "block" } },
  navIconCollapsed: { marginRight: 0 },
  navButtonSelected: { color: "#fff", fontWeight: tokens.fontWeightSemibold },
  sidebarFooter: { display: "flex", alignItems: "center", gap: "2px", flexShrink: 0, borderTop: "1px solid rgba(255,255,255,.08)", paddingTop: "4px", marginTop: "4px" },
  sidebarFooterCollapsed: { flexDirection: "column", alignItems: "stretch" },
  classicButton: { flex: "1 1 auto", minWidth: 0, height: "32px", minHeight: "32px", justifyContent: "flex-start", overflow: "hidden", whiteSpace: "nowrap", borderRadius: "14px", color: "rgba(244,247,245,.64)", transitionProperty: "color, background-color, transform", transitionDuration: "160ms" },
  footerIconButton: { flexShrink: 0, minWidth: "32px", width: "32px", height: "32px", padding: 0, borderRadius: "14px", color: "rgba(244,247,245,.64)", transitionProperty: "color, background-color, transform", transitionDuration: "160ms" },
  footerIconButtonCollapsed: { width: "100%" },
  externalMark: { marginLeft: "auto", display: "inline-flex", alignItems: "center", color: "rgba(244,247,245,.4)" },

  // ── Main: atmospheric sage surface ──
  main: { position: "relative", display: "flex", flexDirection: "column", minWidth: 0, minHeight: 0, overflow: "hidden", contain: "layout paint", borderRadius: "18px", backgroundColor: "#11123f" },
  mainScroll: { position: "relative", zIndex: 1, flex: "1 1 auto", minHeight: 0, overflowY: "auto", overscrollBehavior: "contain", scrollBehavior: "smooth", scrollbarGutter: "stable both-edges" },
  // Ink dither stage (lab variant 07). Gradient base + canvas field.
  atmosphere: {
    position: "absolute", inset: 0, zIndex: 0, pointerEvents: "none", overflow: "hidden",
    backgroundImage: [
      "radial-gradient(circle at 40% 10%, rgba(190, 126, 235, 0.46), transparent 30%)",
      "linear-gradient(145deg, #47336f 0%, #3b2d8c 35%, #202064 70%, #11123f 100%)",
    ].join(", "),
    "@media (prefers-reduced-transparency: reduce)": { backgroundImage: "linear-gradient(145deg, #3d3750, #2a2540)" },
  },

  content: { position: "relative", zIndex: 1, width: "100%", maxWidth: "820px", boxSizing: "border-box", margin: "0 auto", padding: "40px 32px 80px", "@media (max-width: 680px)": { padding: "76px 16px 48px" } },

  // ── Page header: title + subtitle only. No decorative page glyph. ──
  pageHeader: { marginBottom: "28px" },
  pageIcon: { display: "none" },
  pageTitle: { display: "block", marginBottom: "6px", letterSpacing: "-0.02em", lineHeight: "1.15" },
  pageSubtitle: { display: "block", maxWidth: "62ch", color: "rgba(244,247,245,.62)", lineHeight: "1.5" },

  // ── Cards / sections ──
  section: {
    marginBottom: "20px", overflow: "hidden",
    border: "1px solid rgba(255,255,255,.10)", borderRadius: "18px",
    backgroundColor: "rgba(22, 18, 32, 0.72)",
    boxShadow: "inset 0 1px 0 rgba(255,255,255,.05), 0 10px 28px rgba(10, 8, 15, 0.22)",
    transitionProperty: "transform, box-shadow, border-color", transitionDuration: "240ms",
    "@media (prefers-reduced-transparency: reduce)": { backgroundColor: "#1d1928" },
  },
  sectionHeader: { padding: "18px 24px 14px" },
  sectionTitle: { display: "block", lineHeight: "1.3" },
  sectionDescription: { display: "block", marginTop: "4px", color: "rgba(244,247,245,.62)", maxWidth: "72ch", lineHeight: "1.5" },

  // ── Setting rows ──
  settingList: { borderTop: "1px solid rgba(255,255,255,.10)" },
  settingRow: {
    display: "grid", gridTemplateColumns: "minmax(0, 1fr) auto", gap: "28px", alignItems: "center",
    minHeight: "64px", padding: "0 24px",
    transitionProperty: "background-color, transform", transitionDuration: "200ms",
    "& + &": { borderTop: "1px solid rgba(255,255,255,.09)" },
    "@media (max-width: 680px)": { gridTemplateColumns: "minmax(0, 1fr)", gap: "12px", padding: "16px 20px" },
  },
  settingCopy: { minWidth: 0, padding: "14px 0", "@media (max-width: 680px)": { padding: 0 } },
  settingDescription: { display: "block", marginTop: "4px", color: "rgba(244,247,245,.62)", maxWidth: "72ch", lineHeight: "1.5" },
  controlRow: { display: "flex", alignItems: "center", gap: "12px", padding: "16px 24px", minHeight: "62px", transitionProperty: "background-color", transitionDuration: "200ms" },
  controlGrow: { flex: 1, minWidth: 0 },
  range: { width: "260px", maxWidth: "100%" },
  select: { minWidth: "200px", minHeight: "40px", padding: "0 12px", color: "#F4F7F5", backgroundColor: "rgba(255,255,255,.08)", border: "1px solid rgba(255,255,255,.14)", borderRadius: "14px", colorScheme: "dark", transitionProperty: "background-color, border-color, transform", transitionDuration: "180ms" },
  actionRow: { display: "flex", flexWrap: "wrap", alignItems: "center", gap: "10px", padding: "16px 24px", transitionProperty: "background-color", transitionDuration: "200ms", "& + &": { borderTop: "1px solid rgba(255,255,255,.09)" } },
  rowActions: { display: "flex", flexWrap: "wrap", alignItems: "center", justifyContent: "flex-end", gap: "10px" },
  inlineRange: { width: "240px", maxWidth: "100%" },
  list: { display: "grid", gap: 0, padding: 0 },
  listRow: { display: "grid", gridTemplateColumns: "minmax(0, 1fr) auto", gap: "16px", alignItems: "center", padding: "16px 24px", borderTop: "1px solid rgba(255,255,255,.09)", transitionProperty: "background-color, transform", transitionDuration: "200ms", "@media (max-width: 680px)": { gridTemplateColumns: "minmax(0, 1fr)", alignItems: "start" } },
  listMeta: { display: "block", marginTop: "3px", color: "rgba(244,247,245,.60)" },
  empty: { display: "block", padding: "16px 24px 18px", color: "rgba(244,247,245,.58)", lineHeight: "1.5" },

  // ── Accordion rows (profiles, app rules) ──
  accList: { display: "grid" },
  accItem: { borderTop: "1px solid rgba(255,255,255,.09)", "&:first-child": { borderTop: "none" } },
  accHeader: {
    display: "flex", alignItems: "center", gap: "12px", width: "100%", boxSizing: "border-box",
    padding: "12px 24px", minHeight: "62px", cursor: "pointer",
    "@media (max-width: 680px)": { flexWrap: "wrap" },
  },
  accCopy: { minWidth: 0, flex: 1 },
  accInlineControls: { display: "inline-flex", alignItems: "center", gap: "10px", flexShrink: 0 },
  accChevron: { flexShrink: 0, color: "rgba(244,247,245,.6)", transitionProperty: "transform", transitionDuration: "200ms", transitionTimingFunction: "cubic-bezier(.33,1,.68,1)", "@media (prefers-reduced-motion: reduce)": { transitionDuration: "0ms" } },
  accChevronOpen: { transform: "rotate(180deg)" },
  accDetail: {
    display: "grid", margin: "0 8px 8px", overflow: "hidden",
    border: "1px solid rgba(255,255,255,.07)", borderRadius: "14px",
    backgroundColor: "rgba(255,255,255,.02)",
  },
  ruleBadges: { display: "flex", flexWrap: "wrap", justifyContent: "flex-end", gap: "6px", "@media (max-width: 680px)": { justifyContent: "flex-start" } },

  // ── Theme presets (horizontal chip flow) ──
  themeFlow: { display: "flex", flexWrap: "wrap", gap: "8px", padding: "14px 16px" },
  themeItem: { display: "inline-flex", alignItems: "center", gap: "2px", minWidth: 0 },
  themeChip: {
    display: "inline-flex", alignItems: "center", gap: "10px", textAlign: "left",
    minHeight: "38px", padding: "6px 12px", cursor: "pointer",
    border: "1px solid rgba(255,255,255,.10)", borderRadius: "14px",
    color: "#F4F7F5", backgroundColor: "rgba(255,255,255,.04)",
    transitionProperty: "background-color, border-color", transitionDuration: "180ms",
    ":hover": { backgroundColor: "rgba(255,255,255,.08)" },
  },
  themeChipSelected: { border: `1px solid ${ACCENT}`, backgroundColor: ACCENT_DIM },
  themeActiveMark: { flexShrink: 0, color: ACCENT_STRONG },
  themeDelete: { minWidth: "28px", width: "28px", height: "28px", padding: 0, borderRadius: "14px", transitionProperty: "transform, background-color", transitionDuration: "180ms" },
  swatches: { display: "flex", gap: "4px", flexShrink: 0 },
  swatch: { width: "18px", height: "9px", borderRadius: "999px", border: "1px solid rgba(0,0,0,.15)", boxShadow: "inset 0 1px 2px rgba(0,0,0,.1)" },
  colorInput: { width: "42px", height: "34px", padding: "3px", border: "1px solid rgba(255,255,255,.14)", borderRadius: "14px", backgroundColor: "rgba(255,255,255,.06)", transitionProperty: "transform, border-color", transitionDuration: "180ms" },

  // ── Window chrome ──
  windowControls: { position: "fixed", top: "12px", right: "16px", zIndex: 30, display: "flex", alignItems: "center", gap: "4px", "@media (max-width: 680px)": { top: "14px" } },
  dragRegion: { position: "fixed", top: 0, right: "104px", left: "264px", zIndex: 19, height: "44px", touchAction: "none", userSelect: "none", "@media (max-width: 680px)": { left: 0 } },
  dragRegionCollapsed: { left: "80px" },

  // ── Mobile ──
  mobileHeader: { display: "none", "@media (max-width: 680px)": { display: "flex", position: "fixed", inset: "0 0 auto 0", zIndex: 10, alignItems: "center", gap: "12px", height: "58px", padding: "0 96px 0 18px", backgroundColor: "rgba(5, 7, 6, 0.92)", borderBottom: "1px solid rgba(255,255,255,.08)" } },
  mobileMenuButton: { marginLeft: "auto", flexShrink: 0, minWidth: "36px", width: "36px", height: "36px", padding: 0, color: "rgba(244,247,245,.7)", borderRadius: "14px", transitionProperty: "color, background-color, transform", transitionDuration: "160ms" },
  drawerBackdrop: { position: "fixed", inset: "58px 0 0 0", zIndex: 14, display: "none", backgroundColor: "rgba(5, 7, 6, 0.55)", opacity: 0, pointerEvents: "none", transitionProperty: "opacity", transitionDuration: "200ms", "@media (max-width: 680px)": { display: "block" } },
  drawerBackdropOpen: { opacity: 1, pointerEvents: "auto" },
  mobileDrawer: { position: "fixed", top: "58px", left: 0, bottom: 0, width: "248px", zIndex: 15, display: "none", flexDirection: "column", padding: "12px 8px", backgroundColor: "#050706", transform: "translateX(-100%)", transitionProperty: "transform", transitionDuration: "220ms", transitionTimingFunction: "cubic-bezier(.2,0,0,1)", boxShadow: "8px 0 24px rgba(0,0,0,.4)", "@media (max-width: 680px)": { display: "flex" } },
  mobileDrawerOpen: { transform: "translateX(0)" },
  message: { marginBottom: "20px", borderRadius: "16px", border: "1px solid rgba(255,255,255,.12)", boxShadow: "0 8px 20px rgba(10, 8, 15, 0.25)" },
  searchEmpty: { display: "grid", minHeight: "320px", placeItems: "center", color: "rgba(244,247,245,.58)" },

  // ── Loading skeleton (replaces the WPF progress bar) ──
  skeletonShell: { display: "grid", gridTemplateColumns: "248px minmax(0, 1fr)", gap: "8px", padding: "6px 8px 8px", height: "100%", "@media (max-width: 680px)": { display: "block" } },
  skeletonSidebar: { backgroundColor: "#050706", borderRadius: "18px", padding: "12px 8px", display: "flex", flexDirection: "column", gap: "10px", "@media (max-width: 680px)": { display: "none" } },
  skeletonMain: { position: "relative", overflow: "hidden", padding: "48px 0", borderRadius: "18px" },
  skeletonSplash: { position: "absolute", inset: 0, zIndex: 2, display: "grid", placeItems: "center", pointerEvents: "none" },
  skeletonSplashLogo: { width: "56px", height: "56px", objectFit: "contain", filter: "drop-shadow(0 2px 12px rgba(155,123,234,.35))" },
  skeletonBlock: { backgroundColor: "rgba(255,255,255,.06)", borderRadius: "18px", position: "relative", overflow: "hidden" },
  skeletonShimmer: {
    position: "absolute", inset: 0,
    backgroundImage: "linear-gradient(90deg, transparent, rgba(255,255,255,.07), transparent)",
    backgroundRepeat: "no-repeat", backgroundSize: "50% 100%",
    animationName: "sage-shimmer", animationDuration: "1.6s", animationTimingFunction: "ease-in-out", animationIterationCount: "infinite",
    "@media (prefers-reduced-motion: reduce)": { animation: "none" },
  },
});

const sageGlassDarkTheme = {
  ...webDarkTheme,
  colorNeutralForeground1: "#F4F7F5",
  colorNeutralForeground2: "rgba(244,247,245,.82)",
  colorNeutralForeground3: "rgba(244,247,245,.58)",
  colorNeutralStroke1: "rgba(255,255,255,.14)",
  colorNeutralStroke2: "rgba(255,255,255,.10)",
  colorNeutralStroke3: "rgba(255,255,255,.08)",
  colorBrandBackground: ACCENT,
  colorBrandBackgroundHover: ACCENT_STRONG,
  colorBrandBackgroundPressed: ACCENT,
  colorBrandForeground1: ACCENT_STRONG,
  colorBrandForeground2: ACCENT_STRONG,
  colorBrandStroke1: ACCENT,
  colorBrandStroke2: ACCENT_STRONG,
  colorCompoundBrandBackground: ACCENT,
  colorCompoundBrandBackgroundHover: ACCENT_STRONG,
  colorCompoundBrandBackgroundPressed: ACCENT,
  colorCompoundBrandStroke: ACCENT,
  colorCompoundBrandStrokeHover: ACCENT_STRONG,
  colorCompoundBrandStrokePressed: ACCENT,
  colorStrokeFocus2: ACCENT,
  // Disabled controls read as dimmed glass instead of a near-black hole.
  colorNeutralBackgroundDisabled: "rgba(255,255,255,.05)",
  colorNeutralForegroundDisabled: "rgba(244,247,245,.32)",
  colorNeutralStrokeDisabled: "rgba(255,255,255,.08)",
  colorSubtleBackgroundHover: "rgba(255,255,255,.06)",
  colorSubtleBackgroundPressed: "rgba(255,255,255,.03)",
  colorNeutralBackground1: "transparent",
  colorNeutralBackground2: "transparent",
  colorNeutralBackground3: "rgba(255,255,255,.06)",
  colorNeutralBackground4: "rgba(255,255,255,.04)",
  colorNeutralBackground5: "rgba(255,255,255,.08)",
  colorNeutralBackground6: "rgba(255,255,255,.10)",
};

const fallbackPayload: SettingsPayload = {
  appName: "BetterTrumpet", locale: "fr-FR", categories: [{ title: "Essentiel", pages: [
    { id: "general", title: "Général", subtitle: "Démarrage et icône de notification.", migrated: true },
    { id: "mouse", title: "Volume et souris", subtitle: "Molette et échelle du volume.", migrated: true },
    { id: "shortcuts", title: "Raccourcis", subtitle: "Raccourcis clavier.", migrated: true },
  ] }],
  labels: {}, values: {} as SettingsPayload["values"],
  collections: { hiddenApps: [], hiddenDevices: [], hotkeys: [], deviceHotkeys: [], profiles: [], selectedProfileIndex: -1, appRules: [], folderRules: [], themes: [], activeThemeName: "" },
  status: { version: "", health: "", updateText: "", updateDetail: "", updateAvailable: false, updateBusy: false, effectivePeakMeterFps: 60, ecoModeActive: false, monkeyUnlocked: false },
};

function pageIcon(pageId: string, className: string) {
  const props = { size: 18 };
  let icon;
  switch (pageId) {
    case "mouse": icon = <MouseIcon {...props} />; break;
    case "shortcuts": icon = <KeyboardIcon {...props} />; break;
    case "profiles": icon = <SaveIcon {...props} />; break;
    case "app-rules": icon = <ListChecksIcon {...props} />; break;
    case "appearance": icon = <BlendIcon {...props} />; break;
    case "media": icon = <MusicIcon {...props} />; break;
    case "performance": icon = <ActivityIcon {...props} />; break;
    case "updates": icon = <RefreshCwIcon {...props} />; break;
    case "privacy": icon = <ShieldCheckIcon {...props} />; break;
    case "about": icon = <InfoIcon {...props} />; break;
    default: icon = <SettingsIcon {...props} />;
  }
  return <span className={className}>{icon}</span>;
}

function groupPages(payload: SettingsPayload) {
  const pages = payload.categories.flatMap(category => category.pages);
  const byId = new Map(pages.map(page => [page.id, page]));
  const groups = [
    [payload.labels.essentials || "Essentials", ["general", "mouse", "shortcuts"]],
    [payload.labels.audio || "Audio", ["profiles", "app-rules", "media"]],
    [payload.labels.experience || "Experience", ["appearance", "performance"]],
    [payload.labels.application || "Application", ["updates", "privacy", "about"]],
  ] as const;
  return groups.map(([title, ids]) => ({ title, pages: ids.map(id => byId.get(id)).filter((page): page is SettingsPageDescriptor => Boolean(page)) })).filter(group => group.pages.length);
}

const pageLabelKeys: Record<string, string[]> = {
  general: ["startupTitle", "startupDescription", "runAtStartup", "trayTitle", "trayDescription", "useLegacyIcon", "trayDeviceType", "trayDeviceTypeDescription", "showAppTooltips", "showAppTooltipsDescription", "hiddenApps", "hiddenAppsDescription", "hiddenDevices", "restore", "restoreAll"],
  mouse: ["scrollWheelTitle", "scrollWheelDescription", "useScrollWheelInTray", "useScrollWheelInTrayDescription", "useGlobalMouseWheelHook", "useGlobalMouseWheelHookDescription", "volumeScaleTitle", "volumeScaleDescription", "useLogarithmicVolume", "useVolumeTickSound", "useVolumeTickSoundDescription", "deviceChangeTitle", "deviceChangeDescription", "notifyOnDeviceChange", "focusLostTitle", "focusLostDescription", "useFocusLostVolume", "focusLostAttenuate", "focusLostAttenuateHint", "focusLostFade", "focusLostFadeHint", "focusLostScope", "focusLostAllApps", "focusLostSelectedApps", "focusLostSelectedHint"],
  shortcuts: ["shortcuts", "recordShortcut", "clearShortcut"],
  profiles: ["savedProfiles", "presetsDescription", "whatToSave", "includeDefaults", "includeDefaultsDesc", "includeDevices", "includeDevicesDesc", "includeApps", "includeAppsDesc", "routeApps", "rolePlayback", "roleRecording", "updateFromCurrent", "cycleTitle", "nextPreset", "previousPreset", "confirmation", "notificationDuration", "profileShortcut", "apply", "rename", "export", "import", "delete"],
  "app-rules": ["appRules", "appRulesDescription", "appPlaceholder", "browse", "addApp", "hardMute", "focusLostRule", "volumeBehavior", "modeNone", "modeLaunch", "modeLock", "targetVolume", "clearAllRules", "folderRules", "folderRulesDescription", "folderRulesEmpty", "addFolder", "changeFolder"],
  appearance: ["appearance", "appearanceDescription", "dynamicAlbum", "dynamicAlbumDescription", "enableDynamicAlbum", "presets", "customColors", "customColorsTweakHint", "windowOpacity", "peakStyle", "randomize", "reset", "saveTheme", "sliderThumb", "sliderFill", "sliderTrack", "peakColor", "windowColor", "textColor", "accentColor", "deleteTheme"],
  media: ["mediaPopup", "mediaPopupDescription", "enableMediaPopup", "interaction", "hoverDelay", "showWhenPaused", "rememberExpanded"],
  performance: ["ecoMode", "ecoModeDescription", "enableEcoMode", "autoEcoMode", "animations", "smoothAnimation", "animationSpeed", "peakMeter", "refreshRate", "effectiveRate", "effectiveRateHint"],
  updates: ["updates", "updatesDescription", "autoUpdates", "notifyFor", "checkUpdate", "installUpdate", "updateChannel0", "updateChannel1", "updateChannel2", "updateChannel3"],
  privacy: ["privacy", "privacyDescription", "settingsData", "settingsDataDescription", "exportSettings", "importSettings"],
  about: ["about", "diagnostics", "diagnosticsDescription", "github", "feedback", "bugReport", "monkeySound", "monkeySoundDescription"],
};

function normalizeSearch(value: string, locale: string) {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase(locale);
}

// ── Deep search ──────────────────────────────────────────────────────────────
// The search indexes every setting section (not just pages), scores matches
// with word-prefix > substring > fuzzy-subsequence, folds accents while
// preserving string offsets (so highlights line up), and supports multi-token
// AND queries. Selecting a result navigates to the page and scrolls the
// matched section into view with a brief flash.

interface SearchHit {
  key: string;
  pageId: string;
  anchor?: string;
  title: string;
  meta: string;
  haystackFolded: string;
  titleOriginal: string;
}

interface SearchSpec {
  page: string;
  anchor?: string;
  keys: string[];
  words?: string;
}

// Static index: every searchable section, with its resx label keys plus
// bilingual synonym hints users are likely to type.
const SEARCH_SPECS: SearchSpec[] = [
  { page: "general", anchor: "startup", keys: ["startupTitle", "startupDescription", "runAtStartup"], words: "demarrage lance boot windows start launch demarrer" },
  { page: "general", anchor: "tray", keys: ["trayTitle", "trayDescription", "useLegacyIcon", "trayDeviceType", "trayDeviceTypeDescription", "showAppTooltips", "showAppTooltipsDescription"], words: "icone notification tray systraze tooltip infobulle legacy ancien icone casque headphones headset micro ecran monitor hdmi peripherique device type sortie output" },
  { page: "general", anchor: "hiddenApps", keys: ["hiddenApps", "hiddenAppsDescription", "restore", "restoreAll"], words: "cache masquer restaurer apps applications hidden restore" },
  { page: "general", anchor: "hiddenDevices", keys: ["hiddenDevices", "restore"], words: "peripherique cache restaure hidden device" },
  { page: "mouse", anchor: "wheel", keys: ["scrollWheelTitle", "scrollWheelDescription", "useScrollWheelInTray", "useGlobalMouseWheelHook", "useScrollWheelInTrayDescription", "useGlobalMouseWheelHookDescription"], words: "molette souris scroll wheel tray global survol" },
  { page: "mouse", anchor: "scale", keys: ["volumeScaleTitle", "volumeScaleDescription", "useLogarithmicVolume", "useVolumeTickSound", "useVolumeTickSoundDescription"], words: "echelle logarithme log son tick bruit volume scale sound" },
  { page: "mouse", anchor: "deviceChange", keys: ["deviceChangeTitle", "deviceChangeDescription", "notifyOnDeviceChange"], words: "notification changement peripherique default switch toast" },
  { page: "mouse", anchor: "focusLost", keys: ["focusLostTitle", "focusLostDescription", "useFocusLostVolume", "focusLostAttenuate", "focusLostFade", "focusLostScope"], words: "perte focus arriere plan attenuer mute baisser volume background unfocus" },
  { page: "shortcuts", anchor: "shortcuts", keys: ["shortcuts", "recordShortcut", "clearShortcut"], words: "raccourci clavier hotkey shortcut touche clavier" },
  { page: "shortcuts", anchor: "deviceShortcuts", keys: ["deviceShortcuts", "deviceShortcutsDesc", "defaultDeviceBadge"], words: "raccourci peripherique basculer sortie device hotkey switch" },
  { page: "profiles", anchor: "presets", keys: ["savedProfiles", "presetsDescription", "whatToSave", "includeDefaults", "includeDevices", "includeApps", "routeApps", "updateFromCurrent", "apply", "save"], words: "quicktrumpet preset profil sauvegarder appliquer configuration volumes peripherique par defaut sortie micro microphone casque enceintes soundswitch default device output headset speakers mic save apply edit modifier" },
  { page: "profiles", anchor: "presetCycle", keys: ["cycleTitle", "cycleDesc", "nextPreset", "previousPreset"], words: "quicktrumpet raccourci suivant precedent parcourir boucle cycle hotkey next previous preset" },
  { page: "profiles", anchor: "presetNotification", keys: ["qtNotification", "confirmation", "notificationDuration", "notificationDurationDesc"], words: "quicktrumpet notification confirmation duree toast secondes duration seconds" },
  { page: "app-rules", anchor: "rules", keys: ["appRules", "appRulesDescription", "hardMute", "focusLostRule", "volumeBehavior", "modeLaunch", "modeLock", "targetVolume"], words: "regle application mute sourdine verrouiller lancement lock launch per app regles" },
  { page: "app-rules", anchor: "folders", keys: ["folderRules", "folderRulesDescription", "targetVolume", "addFolder"], words: "dossier folder volume par defaut demarrage dossier" },
  { page: "appearance", anchor: "themes", keys: ["presets", "appearanceDescription"], words: "theme preset couleur palette apparence appearance theme dracula nord spotify" },
  { page: "appearance", anchor: "colors", keys: ["customColors", "customColorsTweakHint", "windowOpacity", "peakStyle", "sliderThumb", "sliderFill", "sliderTrack", "peakColor", "windowColor", "textColor", "accentColor", "randomize", "reset", "saveTheme"], words: "couleur personnalisee opacite transparence slider barre texte glow custom color opacity tweak" },
  { page: "appearance", anchor: "albumArt", keys: ["dynamicAlbum", "dynamicAlbumDescription", "enableDynamicAlbum"], words: "album art dynamique pochette spotify artwork dynamic" },
  { page: "media", anchor: "mediaPopup", keys: ["mediaPopup", "mediaPopupDescription", "enableMediaPopup", "showWhenPaused"], words: "media popup media popup pause lecture hover survol lecteur" },
  { page: "media", anchor: "mediaInteraction", keys: ["interaction", "hoverDelay", "showWhenPaused", "rememberExpanded"], words: "delai survol hover delay interaction etendu expanded" },
  { page: "performance", anchor: "eco", keys: ["ecoMode", "ecoModeDescription", "enableEcoMode", "autoEcoMode", "effectiveRate"], words: "eco economie batterie cpu performance fps" },
  { page: "performance", anchor: "animations", keys: ["animations", "smoothAnimation", "animationSpeed"], words: "animation fluide vitesse smooth speed transition" },
  { page: "performance", anchor: "peakMeter", keys: ["peakMeter", "refreshRate", "effectiveRateHint"], words: "vumetre pic peak fps rafraichissement taux refresh rate" },
  { page: "updates", anchor: "updates", keys: ["updates", "updatesDescription", "autoUpdates", "notifyFor", "checkUpdate", "installUpdate"], words: "mise a jour update canal channel verifier installer beta version" },
  { page: "privacy", anchor: "telemetry", keys: ["privacy", "privacyDescription", "whatsNewFeed", "whatsNewFeedDesc"], words: "telemetry donnees diagnostic vie privee privacy sentry anonyme whats new feed annonces nouveautes messages news" },
  { page: "privacy", anchor: "data", keys: ["settingsData", "settingsDataDescription", "exportSettings", "importSettings"], words: "export import sauvegarde backup settings config" },
  { page: "about", anchor: "about", keys: ["github", "feedback", "bugReport"], words: "a propos version github contact bug bug report feedback" },
  { page: "about", anchor: "diagnostics", keys: ["diagnostics", "diagnosticsDescription"], words: "diagnostic logs support zip bundle sante health" },
];

// Fold accents per-character so folded.length === original.length and every
// match offset maps straight back onto the original string.
function fold(value: string) {
  let out = "";
  for (const ch of value) out += ch.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase();
  return out;
}

// Fuzzy matching rules: short tokens must appear literally (prefix or
// substring); longer tokens may match as a subsequence only when it is
// reasonably compact, and every kept token must clear MIN_TOKEN_SCORE.
// This keeps "album art" from degenerating into "anything containing a/l/b/u/m".
const FUZZY_MIN_TOKEN_LENGTH = 4;
const MIN_TOKEN_SCORE = 24;

function fuzzySubsequenceScore(foldedHay: string, token: string) {
  let first = -1;
  let last = -1;
  let gaps = 0;
  let matched = 0;
  for (const ch of token) {
    const found = foldedHay.indexOf(ch, last + 1);
    if (found === -1) return -1;
    if (first === -1) first = found;
    if (matched > 0) gaps += found - last - 1;
    last = found;
    matched++;
  }
  // Hard span cap: the whole match must fit within a window proportional to
  // the token length, so scattered letters across a long surface are out.
  if (last - first + 1 > token.length * 4) return -1;
  const score = 44 - Math.round((gaps / Math.max(1, token.length)) * 18);
  return score >= MIN_TOKEN_SCORE ? score : -1;
}

function tokenScore(foldedHay: string, token: string) {
  const direct = foldedHay.indexOf(token);
  if (direct === 0 || foldedHay[direct - 1] === " ") return 100 - Math.min(20, token.length);
  if (direct !== -1) return 70;
  if (token.length >= FUZZY_MIN_TOKEN_LENGTH) return fuzzySubsequenceScore(foldedHay, token);
  return -1;
}

function buildSearchIndex(payload: SettingsPayload): SearchHit[] {
  const hits: SearchHit[] = [];
  const pageTitle = new Map(payload.categories.flatMap(category => category.pages).map(page => [page.id, page.title]));
  const push = (pageId: string, anchor: string | undefined, title: string, fields: string[]) => {
    const meta = pageTitle.get(pageId) ?? "";
    hits.push({
      key: `${pageId}:${anchor ?? ""}:${title}`,
      pageId,
      anchor,
      title,
      meta,
      titleOriginal: title,
      haystackFolded: fold([title, ...fields].join(" ")),
    });
  };
  for (const spec of SEARCH_SPECS) {
    const titleKey = spec.keys[0];
    const title = payload.labels[titleKey] ?? titleKey;
    const fields = [...spec.keys.slice(1).map(key => payload.labels[key] ?? ""), spec.words ?? ""];
    push(spec.page, spec.anchor, title, fields);
  }
  // Dynamic data: devices, presets, rules, themes.
  for (const device of payload.collections.deviceHotkeys) {
    push("shortcuts", "deviceShortcuts", device.label, [device.value ?? "", "hotkey raccourci device"]);
  }
  for (const profile of payload.collections.profiles) {
    push("profiles", "presets", profile.name, [profile.details, "quicktrumpet preset profil"]);
  }
  for (const rule of payload.collections.appRules) {
    push("app-rules", "rules", rule.displayName || rule.exeName, [rule.exeName, "regle rule mute"]);
  }
  for (const folder of payload.collections.folderRules) {
    push("app-rules", "folders", folder.folderPath.split("\\").filter(Boolean).pop() ?? folder.folderPath, [folder.folderPath, "dossier folder"]);
  }
  for (const theme of payload.collections.themes) {
    push("appearance", "themes", theme.name, [theme.category, "theme couleur"]);
  }
  return hits;
}

function searchSettings(index: SearchHit[], rawQuery: string): SearchHit[] {
  const query = fold(rawQuery.trim());
  if (!query) return [];
  const tokens = query.split(/\s+/).filter(Boolean);
  const scored: { hit: SearchHit; score: number }[] = [];
  for (const hit of index) {
    let total = 0;
    let allMatched = true;
    for (const token of tokens) {
      const score = tokenScore(hit.haystackFolded, token);
      if (score < MIN_TOKEN_SCORE) { allMatched = false; break; }
      total += score;
    }
    if (!allMatched) continue;
    // Exact multi-word phrase beats everything else.
    if (tokens.length > 1 && hit.haystackFolded.includes(query)) total += 80;
    // All tokens found in the title itself is a strong relevance signal.
    const titleFolded = fold(hit.titleOriginal);
    if (tokens.every(token => titleFolded.includes(token))) total += 40;
    scored.push({ hit, score: total });
  }
  scored.sort((a, b) => b.score - a.score);
  return scored.slice(0, 9).map(entry => entry.hit);
}

// Wrap every occurrence of the query tokens inside the label for display.
function HighlightedLabel({ text, query }: { text: string; query: string }) {
  const tokens = fold(query).split(/\s+/).filter(token => token.length > 0);
  if (!tokens.length) return <>{text}</>;
  const foldedChars: string[] = [];
  for (const ch of text) foldedChars.push(ch.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLowerCase());
  const folded = foldedChars.join("");
  const ranges: [number, number][] = [];
  for (const token of tokens) {
    let from = 0;
    for (;;) {
      const at = folded.indexOf(token, from);
      if (at === -1) break;
      const overlaps = ranges.some(([start, end]) => at < end && at + token.length > start);
      if (!overlaps) ranges.push([at, at + token.length]);
      from = at + token.length;
    }
  }
  if (!ranges.length) return <>{text}</>;
  ranges.sort((a, b) => a[0] - b[0]);
  const parts: ReactNode[] = [];
  let cursor = 0;
  ranges.forEach(([start, end], index) => {
    if (start > cursor) parts.push(text.slice(cursor, start));
    parts.push(<mark key={index} className="search-mark">{text.slice(start, end)}</mark>);
    cursor = end;
  });
  if (cursor < text.length) parts.push(text.slice(cursor));
  return <>{parts}</>;
}

function pageSearchText(payload: SettingsPayload, page: SettingsPageDescriptor) {
  const labels = (pageLabelKeys[page.id] ?? []).map(key => payload.labels[key] ?? "");
  let dynamic: string[] = [];
  switch (page.id) {
    case "general": dynamic = [...payload.collections.hiddenApps.flatMap(item => [item.displayName, item.exeName, item.deviceName]), ...payload.collections.hiddenDevices.map(item => item.displayName)]; break;
    case "shortcuts": dynamic = [...payload.collections.hotkeys.flatMap(item => [item.label, item.description, item.value]), ...payload.collections.deviceHotkeys.flatMap(item => [item.label, item.value])]; break;
    case "profiles": dynamic = payload.collections.profiles.flatMap(item => [item.name, item.slug, item.details, item.hotkey]); break;
    case "app-rules": dynamic = [...payload.collections.appRules.flatMap(item => [item.displayName, item.exeName]), ...payload.collections.folderRules.map(item => item.folderPath)]; break;
    case "appearance": dynamic = payload.collections.themes.flatMap(item => [item.name, item.category]); break;
    case "updates": dynamic = [payload.status.updateText, payload.status.updateDetail]; break;
    case "about": dynamic = [payload.status.version, payload.status.health]; break;
  }
  return normalizeSearch([page.title, page.subtitle, ...labels, ...dynamic].join(" "), payload.locale);
}

// Skeleton shown while the bridge payload has not arrived yet. Mirrors the
// final layout (sidebar + page header + sections) so the transition into the
// real content is imperceptible.
function LoadingSkeleton({ styles }: { styles: ReturnType<typeof useStyles> }) {
  return (
    <div className={styles.skeletonShell} aria-busy="true" aria-label="Loading settings">
      <div className={styles.skeletonSidebar}>
        <div className={styles.skeletonBlock} style={{ height: 28, width: "70%", borderRadius: 14 }} />
        {Array.from({ length: 7 }).map((_, index) => <div key={index} className={styles.skeletonBlock} style={{ height: 38, borderRadius: 14 }}><div className={styles.skeletonShimmer} /></div>)}
      </div>
      <div className={styles.skeletonMain}>
        <div className={styles.atmosphere}><DitherField live={false} /></div>
        <div className={styles.skeletonSplash}><img src={appIcon} alt="" className={mergeClasses(styles.skeletonSplashLogo, "sage-pulse")} /></div>
        <div style={{ width: "100%", maxWidth: 820, boxSizing: "border-box", margin: "0 auto", padding: "40px 32px 80px", position: "relative", zIndex: 1 }}>
          <div style={{ marginBottom: 28 }}>
            <div className={styles.skeletonBlock} style={{ height: 26, width: "32%", marginBottom: 8 }}><div className={styles.skeletonShimmer} /></div>
            <div className={styles.skeletonBlock} style={{ height: 13, width: "54%", borderRadius: 999 }} />
          </div>
          {[0, 1].map(index => (
            <div key={index} className={styles.skeletonBlock} style={{ height: 232, marginBottom: 18 }}>
              <div className={styles.skeletonShimmer} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// UI state persisted across window openings (the WebView2 user data folder is
// persistent). Storage can be unavailable, so every access is guarded.
const STORAGE_SIDEBAR_COLLAPSED = "bt.settings.sidebarCollapsed";
const STORAGE_LAST_PAGE = "bt.settings.lastPage";
function readStored(key: string) { try { return window.localStorage.getItem(key); } catch { return null; } }
function writeStored(key: string, value: string) { try { window.localStorage.setItem(key, value); } catch { /* storage unavailable */ } }

// Runs once when a page mounts (AnimatePresence mode="wait" mounts it after
// the previous page has exited), before paint.
function PageMount({ onMount }: { onMount: () => void }) {
  useLayoutEffect(() => { onMount(); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}

export function App() {
  const styles = useStyles();
  const reduceMotion = useReducedMotion();
  const [payload, setPayload] = useState<SettingsPayload | null>(null);
  const [selectedId, setSelectedId] = useState(() => readStored(STORAGE_LAST_PAGE) || "general");
  const [query, setQuery] = useState("");
  const deferredQuery = useDeferredValue(query);
  const [isOpeningLegacy, setIsOpeningLegacy] = useState(false);
  const [bridgeError, setBridgeError] = useState<string | null>(null);
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => readStored(STORAGE_SIDEBAR_COLLAPSED) === "1");
  const [mobileDrawerOpen, setMobileDrawerOpen] = useState(false);
  const [mainEdgeFade, setMainEdgeFade] = useState({ top: false, bottom: false });
  const [resultIndex, setResultIndex] = useState(0);
  const [pendingAnchor, setPendingAnchor] = useState<{ anchor: string; pageId: string; nonce: number } | null>(null);
  const mainScrollRef = useRef<HTMLDivElement>(null);
  const contentRef = useRef<HTMLDivElement>(null);

  useEffect(() => { writeStored(STORAGE_SIDEBAR_COLLAPSED, sidebarCollapsed ? "1" : "0"); }, [sidebarCollapsed]);
  // The state handler only keeps selectedId when the page still exists, so a
  // stale stored id falls back to the first page and is overwritten here.
  useEffect(() => { if (payload) writeStored(STORAGE_LAST_PAGE, selectedId); }, [payload, selectedId]);

  const updateMainEdgeFade = () => {
    const el = mainScrollRef.current;
    if (!el) return;
    const top = el.scrollTop > 8;
    const bottom = el.scrollHeight - el.scrollTop - el.clientHeight > 8;
    setMainEdgeFade(current => current.top === top && current.bottom === bottom ? current : { top, bottom });
  };
  // Every page lands at its top; a pending search anchor then scrolls from there.
  const resetMainScroll = () => {
    mainScrollRef.current?.scrollTo({ top: 0, behavior: "instant" });
    updateMainEdgeFade();
  };
  // Content height changes (page swap, accordions, async data) can make the
  // pane (un)scrollable without a scroll event: recompute the edge fades.
  const hasPayload = Boolean(payload);
  useEffect(() => {
    const el = mainScrollRef.current;
    const content = contentRef.current;
    if (!el || !content || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(() => updateMainEdgeFade());
    observer.observe(el);
    observer.observe(content);
    return () => observer.disconnect();
  }, [hasPayload]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const webview = window.chrome?.webview;
    if (!webview) return;
    const onMessage = (event: MessageEvent<HostMessage>) => {
      if (event.data.type === "state") {
        const data = event.data.data;
        setPayload(data);
        setIsOpeningLegacy(false);
        setBridgeError(null);
        const firstPage = data.categories.flatMap(category => category.pages)[0];
        if (firstPage) setSelectedId(current => data.categories.flatMap(category => category.pages).some(page => page.id === current) ? current : firstPage.id);
        requestAnimationFrame(() => requestAnimationFrame(() => webview.postMessage({ type: "rendered" })));
      } else if (event.data.type === "status") {
        const { effectivePeakMeterFps, ecoModeActive } = event.data.data;
        setPayload(current => current ? { ...current, status: { ...current.status, effectivePeakMeterFps, ecoModeActive } } : current);
      } else if (event.data.type === "error") {
        setIsOpeningLegacy(false);
        setBridgeError(event.data.message);
      }
    };
    webview.addEventListener("message", onMessage);
    webview.postMessage({ type: "ready" });
    return () => webview.removeEventListener("message", onMessage);
  }, []);

  // Close the mobile drawer with Escape / when the window is resized back to desktop.
  useEffect(() => {
    if (!mobileDrawerOpen) return;
    const onKey = (event: KeyboardEvent) => { if (event.key === "Escape") setMobileDrawerOpen(false); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [mobileDrawerOpen]);

  const groups = useMemo(() => groupPages(payload ?? fallbackPayload), [payload]);
  const normalizedQuery = normalizeSearch(deferredQuery.trim(), payload?.locale ?? "fr-FR");
  const searchIndex = useMemo(() => buildSearchIndex(payload ?? fallbackPayload), [payload]);
  const searchResults = useMemo(() => searchSettings(searchIndex, deferredQuery), [searchIndex, deferredQuery]);
  useEffect(() => { setResultIndex(0); }, [deferredQuery]);

  // After navigating to a search hit, scroll its section into view and flash it.
  useEffect(() => {
    if (!pendingAnchor || !payload) return;
    if (selectedId !== pendingAnchor.pageId) return;
    const nonce = pendingAnchor.nonce;
    // The target page mounts only after the previous one has exited (page
    // crossfade), so wait for its anchor to exist inside that page.
    let frame = 0;
    let attempts = 0;
    const tick = () => {
      const candidate = document.getElementById(pendingAnchor.anchor);
      const element = candidate?.closest(`[data-page-id="${pendingAnchor.pageId}"]`) ? candidate : null;
      if (!element && attempts++ < 45) { frame = requestAnimationFrame(tick); return; }
      setPendingAnchor(current => current?.nonce === nonce ? null : current);
      if (!element) return;
      element.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
      element.classList.add("search-flash");
      window.setTimeout(() => element.classList.remove("search-flash"), 1800);
    };
    frame = requestAnimationFrame(() => { frame = requestAnimationFrame(tick); });
    return () => cancelAnimationFrame(frame);
  }, [pendingAnchor, payload, selectedId, reduceMotion]);
  const filteredGroups = useMemo(() => groups.map(group => ({ ...group, pages: group.pages.filter(page => !normalizedQuery || pageSearchText(payload ?? fallbackPayload, page).includes(normalizedQuery)) })).filter(group => group.pages.length), [groups, normalizedQuery, payload]);
  const allPages = (payload ?? fallbackPayload).categories.flatMap(category => category.pages);
  const visiblePages = filteredGroups.flatMap(group => group.pages);
  const effectiveSelectedId = normalizedQuery && !visiblePages.some(page => page.id === selectedId) ? visiblePages[0]?.id : selectedId;
  const selectedPage = normalizedQuery && !effectiveSelectedId ? undefined : allPages.find(page => page.id === effectiveSelectedId) ?? allPages[0];
  const setSetting = (key: SettingKey, value: SettingValue) => {
    setPayload(current => current ? { ...current, values: { ...current.values, [key]: value } } : current);
    window.chrome?.webview?.postMessage({ type: "setSetting", key, value });
  };
  const action = (name: string, data: Record<string, unknown> = {}) => window.chrome?.webview?.postMessage({ type: "action", action: name, ...data });
  const openClassic = (pageId?: string) => { setBridgeError(null); setIsOpeningLegacy(true); window.chrome?.webview?.postMessage({ type: "openLegacy", pageId: pageId ?? null }); };
  const openResult = (hit: SearchHit) => {
    setSelectedId(hit.pageId);
    setQuery("");
    if (hit.anchor) setPendingAnchor({ anchor: hit.anchor, pageId: hit.pageId, nonce: Date.now() });
  };

  return (
    <FluentProvider className={styles.provider} theme={sageGlassDarkTheme}>
      <div className={mergeClasses(styles.dragRegion, sidebarCollapsed && styles.dragRegionCollapsed)} aria-hidden="true" onPointerDown={event => {
        if (event.button === 0) window.chrome?.webview?.postMessage({ type: "windowAction", action: "drag" });
      }} />
      <WindowControls labels={{ minimize: payload?.labels.minimize || "Minimize", close: payload?.labels.close || "Close" }} styles={styles} />
      <AnimatePresence initial={false}>
      {payload ? (
        <motion.div key="shell" className={payload.status.ecoModeActive ? "eco-active" : undefined} style={{ height: "100%", position: "relative", isolation: "isolate" }} initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0, filter: "blur(6px)" }} transition={{ duration: reduceMotion ? 0 : 0.24, ease: morphEase }}>
        <>
        <div className={mergeClasses(styles.shell, sidebarCollapsed && styles.shellCollapsed)}>
          <motion.aside
            className={mergeClasses(styles.sidebar, "sidebar-polished", sidebarCollapsed && styles.sidebarCollapsed)}
            initial={reduceMotion ? false : { x: -14, opacity: 0 }}
            animate={{ x: 0, opacity: 1 }}
            transition={{ duration: 0.32, ease: morphEase, delay: 0.04 }}
          >
            <SidebarBody styles={styles} payload={payload} groups={filteredGroups} selectedPage={selectedPage} collapsed={sidebarCollapsed} query={query} setQuery={setQuery} isOpeningLegacy={isOpeningLegacy} openClassic={() => openClassic()} navigate={setSelectedId} onToggleCollapsed={() => setSidebarCollapsed(value => !value)} results={searchResults} resultIndex={resultIndex} setResultIndex={setResultIndex} onOpenResult={openResult} instanceId="rail" />
          </motion.aside>
          <motion.main
            className={styles.main}
            initial={reduceMotion ? false : { y: 16, opacity: 0, scale: 0.992 }}
            animate={{ y: 0, opacity: 1, scale: 1 }}
            transition={{ duration: 0.36, ease: morphEase, delay: 0.08 }}
          >
            <div className={styles.atmosphere}><DitherField live={!payload.status.ecoModeActive} /></div>
            <div ref={mainScrollRef} className={mergeClasses(styles.mainScroll, "settings-main", mainEdgeFade.top && "fade-top", mainEdgeFade.bottom && "fade-bottom")} onScroll={updateMainEdgeFade}>
              <div className={mergeClasses(styles.mobileHeader, "mobile-header-polished")}><img className={styles.logo} src={appIcon} alt="" /><Text weight="semibold">{payload.appName}</Text><Button className={styles.mobileMenuButton} appearance="subtle" icon={<MenuIcon size={20} />} aria-label={mobileDrawerOpen ? (payload.labels.closeNavigation || "Close navigation") : (payload.labels.openNavigation || "Open navigation")} aria-expanded={mobileDrawerOpen} title={mobileDrawerOpen ? (payload.labels.closeNavigation || "Close navigation") : (payload.labels.openNavigation || "Open navigation")} onClick={() => setMobileDrawerOpen(value => !value)} /></div>
              <div ref={contentRef} className={styles.content}>
                {bridgeError && <MessageBar className={styles.message} intent="error"><MessageBarBody>{bridgeError}</MessageBarBody></MessageBar>}
                {/* Crossfade: quick fade-out (120ms), then the next page mounts at the top and rises in. */}
                <AnimatePresence mode="wait">
                  {selectedPage && <motion.div key={selectedPage.id} data-page-id={selectedPage.id} initial={{ opacity: 0, y: reduceMotion ? 0 : 10 }} animate={{ opacity: 1, y: 0, transition: { duration: reduceMotion ? 0 : 0.22, ease: morphEase } }} exit={{ opacity: 0, transition: { duration: reduceMotion ? 0 : 0.12, ease: "easeIn" } }}><PageMount onMount={resetMainScroll} /><SettingsPage page={selectedPage} payload={payload} styles={styles} setSetting={setSetting} action={action} openClassic={openClassic} isOpeningLegacy={isOpeningLegacy} /></motion.div>}
                </AnimatePresence>
                {!selectedPage && <Text className={styles.searchEmpty}>{payload.labels.noResults || "No settings match your search."}</Text>}
              </div>
            </div>
          </motion.main>
        </div>
        <div className={mergeClasses(styles.drawerBackdrop, mobileDrawerOpen && styles.drawerBackdropOpen)} onClick={() => setMobileDrawerOpen(false)} aria-hidden="true" />
        <aside className={mergeClasses(styles.mobileDrawer, "sidebar-polished", mobileDrawerOpen && styles.mobileDrawerOpen)} aria-hidden={!mobileDrawerOpen} aria-label="Settings navigation">
          <SidebarBody styles={styles} payload={payload} groups={filteredGroups} selectedPage={selectedPage} collapsed={false} query={query} setQuery={setQuery} isOpeningLegacy={isOpeningLegacy} openClassic={() => { setMobileDrawerOpen(false); openClassic(); }} navigate={id => { setSelectedId(id); setMobileDrawerOpen(false); }} results={searchResults} resultIndex={resultIndex} setResultIndex={setResultIndex} onOpenResult={hit => { setMobileDrawerOpen(false); openResult(hit); }} instanceId="drawer" />
        </aside>
        </>
        </motion.div>
      ) : (
        // Opaque overlay: during the handoff it stays mounted ABOVE the shell
        // (absolute, full-window) and blurs/fades away, so the reveal is a
        // single smooth dissolve instead of two stacked layouts popping.
        <motion.div
          key="skeleton"
          style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0, zIndex: 60, backgroundColor: "#050706" }}
          exit={{ opacity: 0, filter: "blur(8px)" }}
          transition={{ duration: reduceMotion ? 0 : 0.3, ease: morphEase }}
        >
          <LoadingSkeleton styles={styles} />
        </motion.div>
      )}
      </AnimatePresence>
    </FluentProvider>
  );
}

const morphEase: [number, number, number, number] = [0.33, 1, 0.68, 1];

function SidebarBody({ styles, payload, groups, selectedPage, collapsed, query, setQuery, isOpeningLegacy, openClassic, navigate, onToggleCollapsed, results = [], resultIndex = 0, setResultIndex, onOpenResult, instanceId }: {
  styles: ReturnType<typeof useStyles>;
  payload: SettingsPayload;
  groups: { title: string; pages: SettingsPageDescriptor[] }[];
  selectedPage?: SettingsPageDescriptor;
  collapsed: boolean;
  query: string;
  setQuery: (value: string) => void;
  isOpeningLegacy: boolean;
  openClassic: () => void;
  navigate: (id: string) => void;
  onToggleCollapsed?: () => void;
  results?: SearchHit[];
  resultIndex?: number;
  setResultIndex?: (index: number) => void;
  onOpenResult?: (hit: SearchHit) => void;
  // Distinguishes the rail and the mobile drawer so their pills never share a layoutId.
  instanceId: string;
}) {
  const reduceMotion = useReducedMotion();
  const duration = reduceMotion ? 0 : 0.22;
  const blur = (px: number) => (reduceMotion ? "blur(0px)" : `blur(${px}px)`);
  const morph = { duration, ease: morphEase };
  const labelAnimate = collapsed
    ? { opacity: 0 }
    : { opacity: 1 };
  const pillTransition = reduceMotion ? { duration: 0 } : { type: "spring" as const, duration: 0.22, bounce: 0 };
  const dropdownMotion = reduceMotion
    ? { initial: { opacity: 0 }, animate: { opacity: 1 }, exit: { opacity: 0 }, transition: { duration: 0 } }
    : { initial: { opacity: 0, y: -4, scale: 0.98 }, animate: { opacity: 1, y: 0, scale: 1 }, exit: { opacity: 0, y: -4, scale: 0.98 }, transition: { duration: 0.14, ease: morphEase } };
  const navRef = useRef<HTMLElement>(null);
  const navInnerRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const [navFadeVisible, setNavFadeVisible] = useState(false);
  const [focusSearchPending, setFocusSearchPending] = useState(false);
  const showResults = Boolean(query.trim() && results.length > 0 && onOpenResult);

  // Bottom fade only while there is more nav below the visible area.
  const updateNavFade = () => {
    const nav = navRef.current;
    if (!nav) return;
    setNavFadeVisible(nav.scrollHeight - nav.scrollTop - nav.clientHeight > 4);
  };
  useEffect(() => {
    const nav = navRef.current;
    const inner = navInnerRef.current;
    updateNavFade();
    if (!nav || !inner || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver(() => updateNavFade());
    observer.observe(nav);
    observer.observe(inner);
    return () => observer.disconnect();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Keep the current page visible when it changes (search, restored page).
  // Scrolls the nav only (not via scrollIntoView, which could also scroll the
  // overflow-hidden ancestors of the off-canvas mobile drawer).
  useEffect(() => {
    const nav = navRef.current;
    const item = nav?.querySelector<HTMLElement>('[aria-current="page"]');
    if (!nav || !item) return;
    const navRect = nav.getBoundingClientRect();
    const itemRect = item.getBoundingClientRect();
    if (itemRect.top < navRect.top) nav.scrollTop -= navRect.top - itemRect.top + 6;
    else if (itemRect.bottom > navRect.bottom) nav.scrollTop += itemRect.bottom - navRect.bottom + 6;
    updateNavFade();
  }, [selectedPage?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  // Rail search button: expand first, focus once the search field is mounted.
  useEffect(() => {
    if (!focusSearchPending || collapsed) return;
    const frame = requestAnimationFrame(() => { searchInputRef.current?.focus(); setFocusSearchPending(false); });
    return () => cancelAnimationFrame(frame);
  }, [focusSearchPending, collapsed]);

  return (
    <>
      <div className={mergeClasses(styles.sidebarHeader, collapsed && styles.sidebarHeaderCollapsed)}>
        <motion.button
          type="button"
          className={collapsed && onToggleCollapsed ? styles.logoButton : undefined}
          style={collapsed && onToggleCollapsed ? undefined : { display: "grid", placeItems: "center", padding: 0, border: "none", background: "transparent", cursor: "default" }}
          aria-label={collapsed && onToggleCollapsed ? (payload.labels.expandSidebar || "Expand sidebar") : undefined}
          title={collapsed && onToggleCollapsed ? (payload.labels.expandSidebar || "Expand sidebar") : undefined}
          tabIndex={collapsed && onToggleCollapsed ? 0 : -1}
          onClick={collapsed && onToggleCollapsed ? onToggleCollapsed : undefined}
        >
          <img className={mergeClasses(styles.logo, "logo-polished")} src={appIcon} alt="" />
        </motion.button>
        <AnimatePresence initial={false}>
          {!collapsed && (
            <motion.span
              key="wordmark"
              className={styles.wordmark}
              initial={{ opacity: 0, filter: blur(10), x: -8 }}
              animate={{ opacity: 1, filter: blur(0), x: 0 }}
              exit={{ opacity: 0, filter: blur(10), x: -10 }}
              transition={morph}
            >
              {payload.appName}
            </motion.span>
          )}
        </AnimatePresence>
        <AnimatePresence initial={false}>
          {onToggleCollapsed && !collapsed && (
            <motion.div
              key="collapse"
              initial={{ opacity: 0, filter: blur(8) }}
              animate={{ opacity: 1, filter: blur(0) }}
              exit={{ opacity: 0, filter: blur(8) }}
              transition={morph}
              style={{ marginLeft: "auto" }}
            >
              <Button className={mergeClasses(styles.sidebarToggle, "sidebar-toggle-polished")} appearance="subtle" aria-label={payload.labels.collapseSidebar || "Collapse sidebar"} title={payload.labels.collapseSidebar || "Collapse sidebar"} onClick={onToggleCollapsed} icon={<IndentIncreaseIcon size={16} />} />
            </motion.div>
          )}
        </AnimatePresence>
      </div>
      <AnimatePresence initial={false}>
        {collapsed && onToggleCollapsed && (
          <motion.div
            key="search-rail"
            initial={{ opacity: 0, maxHeight: 0, marginBottom: 0 }}
            animate={{ opacity: 1, maxHeight: 32, marginBottom: 8 }}
            exit={{ opacity: 0, maxHeight: 0, marginBottom: 0 }}
            transition={morph}
            style={{ overflow: "hidden", flexShrink: 0 }}
          >
            <Button
              appearance="subtle"
              className={mergeClasses(styles.navButton, "nav-button-polished", styles.navButtonCollapsed)}
              icon={<span className={mergeClasses(styles.navIcon, styles.navIconCollapsed)}><SearchIcon size={18} /></span>}
              aria-label={payload.labels.searchPlaceholder || "Search settings"}
              title={payload.labels.searchPlaceholder || "Search settings"}
              onClick={() => { setFocusSearchPending(true); onToggleCollapsed(); }}
            />
          </motion.div>
        )}
        {!collapsed && (
          <motion.div
            key="search"
            className={styles.searchWrap}
            initial={{ opacity: 0, filter: blur(8), maxHeight: 0, marginBottom: 0 }}
            animate={{ opacity: 1, filter: blur(0), maxHeight: 40, marginBottom: 8 }}
            exit={{ opacity: 0, filter: blur(8), maxHeight: 0, marginBottom: 0 }}
            transition={morph}
            style={{ overflow: "visible", flexShrink: 0 }}
          >
            <div className={`${styles.searchBox} search-box`}>
              <SearchIcon size={16} className={`${styles.searchBoxIcon} search-box-icon`} />
              <input
                ref={searchInputRef}
                className={`${styles.searchInput} search-input-native`}
                value={query}
                onChange={event => setQuery(event.currentTarget.value)}
                placeholder={payload.labels.searchPlaceholder || "Search settings"}
                aria-label={payload.labels.searchPlaceholder || "Search settings"}
                spellCheck={false}
                autoComplete="off"
                onKeyDown={event => {
                  const hasResults = query.trim() && results.length > 0;
                  if (event.key === "Escape" && query) {
                    setQuery("");
                  } else if (event.key === "ArrowDown" && hasResults && setResultIndex) {
                    event.preventDefault();
                    setResultIndex(Math.min(results.length - 1, resultIndex + 1));
                  } else if (event.key === "ArrowUp" && hasResults && setResultIndex) {
                    event.preventDefault();
                    setResultIndex(Math.max(0, resultIndex - 1));
                  } else if (event.key === "Enter") {
                    if (hasResults && onOpenResult) { onOpenResult(results[Math.min(resultIndex, results.length - 1)]); }
                    else if (query.trim()) { const first = groups.flatMap(group => group.pages)[0]; if (first) navigate(first.id); }
                  }
                }}
              />
              {showResults && <kbd className="search-hint-kbd">↵</kbd>}
            </div>
            <AnimatePresence>
              {showResults && onOpenResult && (
                <motion.div key="results" className={styles.searchResults} style={{ transformOrigin: "top center" }} role="listbox" aria-label={payload.labels.searchPlaceholder || "Search results"} {...dropdownMotion}>
                  {results.map((hit, index) => (
                    <button
                      key={hit.key}
                      type="button"
                      role="option"
                      aria-selected={index === resultIndex}
                      className={mergeClasses(styles.searchResult, index === resultIndex && styles.searchResultActive)}
                      onMouseEnter={() => setResultIndex?.(index)}
                      onClick={() => onOpenResult(hit)}
                    >
                      <span className={styles.searchResultLabel}><HighlightedLabel text={hit.titleOriginal} query={query} /></span>
                      <span className={styles.searchResultMeta}>{hit.meta}{hit.anchor ? " ›" : ""}</span>
                    </button>
                  ))}
                </motion.div>
              )}
            </AnimatePresence>
          </motion.div>
        )}
      </AnimatePresence>
      <div className={styles.navWrap}>
        <motion.nav ref={navRef} layoutScroll className={styles.nav} aria-label={payload.labels.navigation || "Settings"} onScroll={updateNavFade}>
          <div ref={navInnerRef}>
            {groups.map(group => <div className={mergeClasses(styles.category, "category-polished")} key={group.title}>
              <motion.div initial={false} animate={collapsed ? { opacity: 0, filter: blur(6), height: 0, paddingBottom: 0, marginBottom: 0 } : { opacity: 1, filter: blur(0), height: "auto", paddingBottom: 0, marginBottom: 0 }} transition={morph} style={{ overflow: "hidden" }}>
                <Text className={mergeClasses(styles.categoryTitle, "category-title-polished")} size={200} weight="semibold">{group.title}</Text>
              </motion.div>
              {group.pages.map(page => {
                const selected = selectedPage?.id === page.id;
                return <div key={page.id} className={styles.navItem}>
                  {selected && <motion.span layoutId={`nav-pill-${instanceId}`} className={styles.navPill} style={{ borderRadius: 14, boxShadow: "inset 0 0 0 1px rgba(255,255,255,.04)" }} transition={pillTransition} aria-hidden="true" />}
                  <Button appearance="subtle" className={mergeClasses(styles.navButton, "nav-button-polished", collapsed && styles.navButtonCollapsed, selected && styles.navButtonSelected, selected && "nav-button-selected-polished")} icon={pageIcon(page.id, mergeClasses(styles.navIcon, "nav-icon-polished", collapsed && styles.navIconCollapsed))} title={page.title} aria-current={selected ? "page" : undefined} onClick={() => navigate(page.id)}><motion.span className={mergeClasses(styles.navLabel, collapsed && styles.navLabelCollapsed)} initial={false} animate={labelAnimate} transition={morph}>{page.title}</motion.span></Button>
                </div>;
              })}
            </div>)}
            {!groups.length && <Text className={styles.empty}>{payload.labels.noResults || "No settings match your search."}</Text>}
          </div>
        </motion.nav>
        <div className={mergeClasses(styles.navFade, navFadeVisible && styles.navFadeVisible)} aria-hidden="true" />
      </div>
      <div className={mergeClasses(styles.sidebarFooter, collapsed && styles.sidebarFooterCollapsed)}>
        <Button className={mergeClasses(styles.classicButton, "classic-button-polished", collapsed && styles.navButtonCollapsed)} appearance="subtle" icon={<HistoryIcon size={18} />} disabled={isOpeningLegacy} onClick={openClassic} title={payload.labels.classicSettings || "Classic settings"}><motion.span className={mergeClasses(styles.navLabel, collapsed && styles.navLabelCollapsed)} initial={false} animate={labelAnimate} transition={morph}>{isOpeningLegacy ? <Spinner size="tiny" /> : payload.labels.classicSettings || "Classic settings"}</motion.span></Button>
        <Button className={mergeClasses(styles.footerIconButton, "classic-button-polished", collapsed && styles.footerIconButtonCollapsed)} appearance="subtle" icon={<GithubIcon size={18} />} title={payload.labels.github || "GitHub"} aria-label={payload.labels.github || "GitHub"} onClick={() => window.chrome?.webview?.postMessage({ type: "action", action: "github" })} />
      </div>
    </>
  );
}

function WindowControls({ labels, styles }: { labels: { minimize: string; close: string }; styles: ReturnType<typeof useStyles> }) {
  const post = (action: "minimize" | "close") => window.chrome?.webview?.postMessage({ type: "windowAction", action });
  // Plain buttons with hairline 10px glyphs (Windows 11 caption weight) instead of Fluent
  // icon buttons, so hover/press states are fully owned by polish.css.
  return <div className={mergeClasses(styles.windowControls, "window-controls-polished")} role="group" aria-label={`${labels.minimize}, ${labels.close}`}>
    <button type="button" className="window-button-polished" aria-label={labels.minimize} title={labels.minimize} onClick={() => post("minimize")}>
      <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M0.5 5h9" /></svg>
    </button>
    <button type="button" className="window-button-polished close-button-polished" aria-label={labels.close} title={labels.close} onClick={() => post("close")}>
      <svg width="10" height="10" viewBox="0 0 10 10" aria-hidden="true"><path d="M1 1l8 8M9 1l-8 8" /></svg>
    </button>
  </div>;
}
