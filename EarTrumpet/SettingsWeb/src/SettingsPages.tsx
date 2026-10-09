import { useCallback, useEffect, useRef, useState } from "react";
import type { KeyboardEvent, MouseEvent, ReactNode } from "react";
import { AnimatePresence, motion, useReducedMotion } from "motion/react";
import { Button, Checkbox, Input, Spinner, Switch, Text, mergeClasses } from "@fluentui/react-components";
import {
  ActivityIcon,
  ArrowLeftRightIcon,
  BellIcon,
  BlendIcon,
  BookmarkPlusIcon,
  CheckIcon,
  ChevronDownIcon,
  EyeOffIcon,
  DownloadIcon,
  ExternalLinkIcon,
  FileTextIcon,
  FolderIcon,
  FolderPlusIcon,
  GithubIcon,
  InfoIcon,
  KeyboardIcon,
  ListChecksIcon,
  MinusIcon,
  MonitorIcon,
  MouseIcon,
  MusicIcon,
  PlusIcon,
  RefreshCwIcon,
  SaveIcon,
  SettingsIcon,
  ShieldCheckIcon,
  ShuffleIcon,
  SlidersHorizontalIcon,
  Trash2Icon,
  TriangleAlertIcon,
  UploadIcon,
  Volume2Icon,
  VolumeXIcon,
  XIcon,
} from "@animateicons/react/lucide";
import ElasticSlider from "./components/ElasticSlider";
import type { AppRule, AudioState, SettingKey, SettingsPageDescriptor, SettingsPayload, SettingValue, VolumeProfile } from "./types";
import "./pages.css";

type Styles = Record<string, string>;
type Action = (name: string, data?: Record<string, unknown>) => void;
type SetSetting = (key: SettingKey, value: SettingValue) => void;

interface PageProps {
  page: SettingsPageDescriptor;
  payload: SettingsPayload;
  styles: Styles;
  setSetting: SetSetting;
  action: Action;
  openClassic: (pageId?: string) => void;
  isOpeningLegacy: boolean;
}

const t = (payload: SettingsPayload, key: string, fallback: string) => payload.labels[key] || fallback;

// ── Motion vocabulary ─────────────────────────────────────────────────────────
// One decelerating curve for every reveal/list change; durations collapse to 0
// under prefers-reduced-motion.
const EASE = [0.33, 1, 0.68, 1] as const;
const FLASH_MS = 1600;
const CONFIRM_MS = 3000;
const COLOR_SEND_INTERVAL_MS = 120;

const timing = (reduce: boolean | null, duration = 0.2) => ({ duration: reduce ? 0 : duration, ease: EASE });

/** Enter: fade + 6px rise with height grow. Exit: fade + collapse. */
const itemMotion = (reduce: boolean | null) => ({
  initial: { opacity: 0, y: reduce ? 0 : 6, height: 0, overflow: "hidden" },
  animate: { opacity: 1, y: 0, height: "auto", transitionEnd: { overflow: "visible" } },
  exit: { opacity: 0, height: 0, overflow: "hidden" },
  transition: timing(reduce),
});

const stop = (event: MouseEvent) => event.stopPropagation();

/** Accordion headers toggle on Enter/Space only when the header itself has focus. */
const headerKeys = (event: KeyboardEvent<HTMLElement>, onToggle: () => void) => {
  if (event.target !== event.currentTarget) return;
  if (event.key === "Enter" || event.key === " ") { event.preventDefault(); onToggle(); }
};

const sameText = (a?: string, b?: string) => (a ?? "").trim().toLowerCase() === (b ?? "").trim().toLowerCase();

export function SettingsPage(props: PageProps) {
  const { page, styles } = props;
  return <>
    <header className={styles.pageHeader}>
      <Text className={styles.pageTitle} as="h1" size={700} weight="semibold">{page.title}</Text>
      <Text className={styles.pageSubtitle} size={300}>{page.subtitle}</Text>
    </header>
    {renderPage(props)}
  </>;
}

function renderPage(props: PageProps) {
  switch (props.page.id) {
    case "general": return <GeneralPage {...props} />;
    case "mouse": return <MousePage {...props} />;
    case "shortcuts": return <ShortcutsPage {...props} />;
    case "profiles": return <ProfilesPage {...props} />;
    case "app-rules": return <RulesPage {...props} />;
    case "appearance": return <AppearancePage {...props} />;
    case "media": return <MediaPage {...props} />;
    case "performance": return <PerformancePage {...props} />;
    case "updates": return <UpdatesPage {...props} />;
    case "privacy": return <PrivacyPage {...props} />;
    case "about": return <AboutPage {...props} />;
    default: return <UnsupportedPage {...props} />;
  }
}

// ── Shared building blocks ────────────────────────────────────────────────────

function Section({ title, description, styles, children, anchor }: { icon?: ReactNode; title: string; description?: string; styles: Styles; children: ReactNode; anchor?: string }) {
  return <section id={anchor} className={`${styles.section} section-polished`}><header className={styles.sectionHeader}><Text className={styles.sectionTitle} as="h2" size={400} weight="semibold">{title}</Text>{description && <Text className={styles.sectionDescription} size={200}>{description}</Text>}</header><div className={styles.settingList}>{children}</div></section>;
}

/** Height + opacity reveal for conditional blocks (accordion panels, dependent settings). */
function Reveal({ open, children, className }: { open: boolean; children: ReactNode; className?: string }) {
  const reduce = useReducedMotion();
  return <AnimatePresence initial={false}>
    {open && <motion.div
      key="reveal"
      className={className}
      initial={{ height: 0, opacity: 0, overflow: "hidden" }}
      animate={{ height: "auto", opacity: 1, transitionEnd: { overflow: "visible" } }}
      exit={{ height: 0, opacity: 0, overflow: "hidden" }}
      transition={timing(reduce)}
    >{children}</motion.div>}
  </AnimatePresence>;
}

function ToggleRow({ payload, styles, settingKey, label, description, disabled, setSetting, className }: { payload: SettingsPayload; styles: Styles; settingKey: SettingKey; label: string; description?: string; disabled?: boolean; setSetting?: SetSetting; className?: string }) {
  const checked = Boolean(payload.values[settingKey]);
  const update = setSetting ?? ((key: SettingKey, value: SettingValue) => window.chrome?.webview?.postMessage({ type: "setSetting", key, value }));
  return <label className={mergeClasses(styles.settingRow, "setting-row-polished", className)} htmlFor={`setting-${settingKey}`}><div className={styles.settingCopy}><Text weight="semibold">{label}</Text>{description && <Text className={styles.settingDescription} size={200}>{description}</Text>}</div><Switch id={`setting-${settingKey}`} checked={checked} disabled={disabled} aria-label={label} onChange={(_, data) => update(settingKey, data.checked)} /></label>;
}

function RangeRow({ payload, styles, label, description, value, min, max, step = 1, suffix = "", disabled, onChange, onCommit }: { payload: SettingsPayload; styles: Styles; label: string; description?: string; value: number; min: number; max: number; step?: number; suffix?: string; disabled?: boolean; onChange?: (value: number) => void; onCommit: (value: number) => void }) {
  return <div className={mergeClasses(styles.settingRow, "setting-row-polished", disabled && "bt-row-disabled")}><div className={styles.settingCopy}><Text weight="semibold">{label}</Text>{description && <Text className={styles.settingDescription} size={200}>{description}</Text>}</div><ElasticSlider className={styles.range} value={value} startingValue={min} maxValue={max} isStepped stepSize={step} suffix={suffix} locale={payload.locale} disabled={disabled} ariaLabel={label} leftIcon={<MinusIcon size={15} />} rightIcon={<PlusIcon size={15} />} onChange={onChange} onCommit={onCommit} /></div>;
}

function InlineRange({ payload, styles, label, value, onCommit }: { payload: SettingsPayload; styles: Styles; label: string; value: number; onCommit: (value: number) => void }) {
  return <ElasticSlider className={styles.inlineRange} value={value} startingValue={0} maxValue={100} isStepped stepSize={1} suffix="%" locale={payload.locale} ariaLabel={label} leftIcon={<MinusIcon size={15} />} rightIcon={<PlusIcon size={15} />} onCommit={onCommit} />;
}

function SelectRow({ styles, label, description, value, options, disabled, onChange }: { styles: Styles; label: string; description?: string; value: number; options: { value: number; label: string }[]; disabled?: boolean; onChange: (value: number) => void }) {
  return <div className={mergeClasses(styles.settingRow, "setting-row-polished", disabled && "bt-row-disabled")}><div className={styles.settingCopy}><Text weight="semibold">{label}</Text>{description && <Text className={styles.settingDescription} size={200}>{description}</Text>}</div><select className={mergeClasses(styles.select, "select-polished", "bt-select")} value={value} disabled={disabled} aria-disabled={disabled || undefined} aria-label={label} onChange={event => onChange(Number(event.currentTarget.value))}>{options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></div>;
}

function ListRow({ styles, title, meta, badge, actions }: { styles: Styles; title: string; meta?: string; badge?: ReactNode; actions: ReactNode }) {
  return <div className={styles.listRow}><div className="list-row-copy"><div className="list-row-title"><Text weight="semibold">{title}</Text>{badge}</div>{meta && <Text className={styles.listMeta} size={200}>{meta}</Text>}</div><div className={styles.rowActions}>{actions}</div></div>;
}

function Empty({ payload, styles, text }: { payload: SettingsPayload; styles: Styles; text?: string }) { return <Text className={mergeClasses(styles.empty, "bt-empty")}>{text ?? t(payload, "empty", "Nothing configured yet.")}</Text>; }

/** Short-lived "it worked" state for buttons whose action has no host dialog. */
function useFlash<T>() {
  const [value, setValue] = useState<T | null>(null);
  const timer = useRef<number | undefined>(undefined);
  useEffect(() => () => window.clearTimeout(timer.current), []);
  const trigger = useCallback((next: T) => {
    window.clearTimeout(timer.current);
    setValue(() => next);
    timer.current = window.setTimeout(() => setValue(null), FLASH_MS);
  }, []);
  return [value, trigger] as const;
}

/** Button content that morphs to a check + confirmation word while `done`. */
function FeedbackContent({ done, icon, label, doneLabel }: { done: boolean; icon?: ReactNode; label: string; doneLabel: string }) {
  const reduce = useReducedMotion();
  return <AnimatePresence initial={false} mode="wait">
    <motion.span
      key={done ? "done" : "idle"}
      className={mergeClasses("bt-btn-content", done && "bt-btn-done")}
      initial={{ opacity: 0, y: reduce ? 0 : 4 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, y: reduce ? 0 : -4 }}
      transition={timing(reduce, 0.14)}
      aria-live={done ? "polite" : undefined}
    >
      {done ? <><CheckIcon size={16} />{doneLabel}</> : <>{icon}{label}</>}
    </motion.span>
  </AnimatePresence>;
}

/**
 * Two-step button. First click arms it ("Confirm?"), second click fires.
 * Reverts after 3 s, on blur, or with Escape. `danger` (default) tints red;
 * `neutral` is for overwrites that are not deletions.
 */
function ConfirmButton({ payload, label, confirmLabel, icon, iconOnly, size, className, tone = "danger", title, onConfirm }: { payload: SettingsPayload; label: string; confirmLabel?: string; icon: ReactNode; iconOnly?: boolean; size?: "small" | "medium"; className?: string; tone?: "danger" | "neutral"; title?: string; onConfirm: () => void }) {
  const [armed, setArmed] = useState(false);
  const timer = useRef<number | undefined>(undefined);
  const reduce = useReducedMotion();
  useEffect(() => () => window.clearTimeout(timer.current), []);
  const disarm = () => { window.clearTimeout(timer.current); setArmed(false); };
  const confirmText = confirmLabel ?? t(payload, "confirmDelete", "Confirm?");
  const text = armed ? confirmText : iconOnly ? null : label;
  return <Button
    appearance="subtle"
    size={size}
    className={mergeClasses(tone === "danger" ? "bt-danger" : "bt-confirm", iconOnly && !armed && "bt-danger-icon", armed && (tone === "danger" ? "bt-danger-armed" : "bt-confirm-armed"), className)}
    aria-label={armed ? confirmText : label}
    title={armed ? undefined : title ?? label}
    onClick={event => {
      event.stopPropagation();
      if (!armed) {
        setArmed(true);
        window.clearTimeout(timer.current);
        timer.current = window.setTimeout(() => setArmed(false), CONFIRM_MS);
        return;
      }
      disarm();
      onConfirm();
    }}
    onBlur={disarm}
    onKeyDown={event => { if (event.key === "Escape" && armed) { event.stopPropagation(); disarm(); } }}
  >
    <span className="bt-btn-content bt-gapless">
      {icon}
      <AnimatePresence initial={false} mode="wait">
        {text && <motion.span
          key={armed ? "armed" : "idle"}
          className="bt-swap"
          initial={{ opacity: 0, width: 0 }}
          animate={{ opacity: 1, width: "auto" }}
          exit={{ opacity: 0, width: 0 }}
          transition={timing(reduce, 0.14)}
        ><span className="bt-swap-text">{text}</span></motion.span>}
      </AnimatePresence>
    </span>
  </Button>;
}

/** Fires `onNew` when exactly one key appears after the first render (an add, not an import). */
function useNewItem(keys: string[], onNew: (key: string) => void) {
  const known = useRef<Set<string> | null>(null);
  const signature = keys.join("\u0000");
  useEffect(() => {
    const previous = known.current;
    known.current = new Set(keys);
    if (!previous) return;
    const added = keys.filter(key => !previous.has(key));
    if (added.length === 1) onNew(added[0]);
  }, [signature]);
}

// ── Hotkeys ───────────────────────────────────────────────────────────────────

const post = (message: Record<string, unknown>) => window.chrome?.webview?.postMessage(message);

/** One capture session per page: the recorded id, plus start/cancel helpers. */
function useHotkeyCapture() {
  const [recording, setRecording] = useState<string | null>(null);
  const recordingRef = useRef<string | null>(null);

  useEffect(() => {
    if (!recording) return;
    const handleGlobalKeyDown = (event: globalThis.KeyboardEvent) => {
      event.preventDefault();
      event.stopPropagation();
      if (["Control", "Shift", "Alt", "Meta"].includes(event.key)) return;
      const clear = event.key === "Escape" || event.key === "Backspace" || event.key === "Delete";
      post({
        type: "setHotkey",
        id: recording,
        keyCode: clear ? 0 : event.keyCode,
        ctrlKey: !clear && event.ctrlKey,
        altKey: !clear && event.altKey,
        shiftKey: !clear && event.shiftKey,
        metaKey: !clear && event.metaKey,
      });
      recordingRef.current = null;
      setRecording(null);
    };
    window.addEventListener("keydown", handleGlobalKeyDown, true);
    return () => window.removeEventListener("keydown", handleGlobalKeyDown, true);
  }, [recording]);

  // Resume host hotkeys if the page unmounts mid-capture.
  useEffect(() => () => { if (recordingRef.current) post({ type: "hotkeyCaptureEnded" }); }, []);

  // NOTE: capture state is intentionally NOT reset by bridge "state" messages:
  // states arrive spontaneously (default-device changes) and would exit an
  // in-progress capture. Capture ends on keydown (record/clear) or blur.
  const start = useCallback((id: string) => {
    if (recordingRef.current === id) return;
    if (recordingRef.current) post({ type: "hotkeyCaptureEnded" });
    recordingRef.current = id;
    setRecording(id);
    post({ type: "hotkeyCaptureStarted" });
  }, []);

  const cancel = useCallback((id: string) => {
    if (recordingRef.current !== id) return;
    recordingRef.current = null;
    setRecording(null);
    post({ type: "hotkeyCaptureEnded" });
  }, []);

  return { recording, start, cancel };
}

function Keycaps({ value }: { value: string }) {
  // "Ctrl++" must keep the trailing plus as a key.
  const parts = value.split(/\+(?!$)/).map(part => part.trim()).filter(Boolean);
  return <span className="hotkey-chips">{parts.map((part, index) => <kbd key={index}>{part}</kbd>)}</span>;
}

/** A single Button for every state so focus survives the record → keycaps swap. */
function HotkeyButton({ payload, id, value, recording, onStart, onCancel }: { payload: SettingsPayload; id: string; value: string; recording: string | null; onStart: (id: string) => void; onCancel: (id: string) => void }) {
  const isRecording = recording === id;
  const recordLabel = t(payload, "recordShortcut", "Record");
  return <Button
    appearance={isRecording ? "primary" : value ? "secondary" : "subtle"}
    className={mergeClasses("bt-hotkey", isRecording && "bt-hotkey-recording", !isRecording && value && "hotkey-button-polished", !isRecording && !value && "bt-hotkey-empty")}
    aria-label={isRecording ? undefined : value ? `${recordLabel}: ${value}` : recordLabel}
    title={!isRecording && !value ? recordLabel : undefined}
    onClick={event => { event.stopPropagation(); onStart(id); }}
    onBlur={() => onCancel(id)}
  >
    {isRecording
      ? <span className="bt-hotkey-recording-text">{t(payload, "pressShortcut", "Press keys · Esc clears")}</span>
      : value ? <Keycaps value={value} /> : <span className="bt-btn-content" aria-hidden="true"><PlusIcon size={14} /></span>}
  </Button>;
}

function ClearHotkeyButton({ payload, id }: { payload: SettingsPayload; id: string }) {
  const label = t(payload, "clearShortcut", "Clear shortcut");
  return <Button className="bt-hotkey-clear" appearance="subtle" size="small" icon={<XIcon size={15} />} aria-label={label} title={label} onClick={event => { event.stopPropagation(); post({ type: "setHotkey", id, keyCode: 0 }); }} />;
}

function HotkeyControl({ payload, id, value, capture }: { payload: SettingsPayload; id: string; value: string; capture: ReturnType<typeof useHotkeyCapture> }) {
  return <span className="bt-hotkey-cell">
    <HotkeyButton payload={payload} id={id} value={value} recording={capture.recording} onStart={capture.start} onCancel={capture.cancel} />
    {value && capture.recording !== id && <ClearHotkeyButton payload={payload} id={id} />}
  </span>;
}

// ── Pages ─────────────────────────────────────────────────────────────────────

function GeneralPage({ payload, styles, action }: PageProps) {
  const reduce = useReducedMotion();
  const hiddenApps = payload.collections.hiddenApps;
  const hiddenDevices = payload.collections.hiddenDevices;
  return <>
    <Section icon={<MonitorIcon size={18} />} title={t(payload, "startupTitle", "Startup")} anchor="startup" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="runAtStartup" label={t(payload, "runAtStartup", "Run at Windows startup")} /></Section>
    <Section icon={<SettingsIcon size={18} />} title={t(payload, "trayTitle", "Notification icon")} description={t(payload, "trayDescription", "Tray icon appearance and behavior")} anchor="tray" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="useLegacyIcon" label={t(payload, "useLegacyIcon", "Use original icon")} /><ToggleRow payload={payload} styles={styles} settingKey="showAppTooltips" label={t(payload, "showAppTooltips", "Show icon tooltips")} description={t(payload, "showAppTooltipsDescription", "Show details while hovering app icons.")} /></Section>
    <Section icon={<MinusIcon size={18} />} anchor="hiddenApps" title={t(payload, "hiddenApps", "Hidden apps")} description={t(payload, "hiddenAppsDescription", "Restore apps hidden from the mixer.")} styles={styles}>
      <div className={styles.list}>
        <AnimatePresence initial={false}>
          {hiddenApps.map(item => <motion.div key={`${item.deviceId}-${item.appId}-${item.exeName}`} {...itemMotion(reduce)}>
            <ListRow styles={styles} title={item.displayName} meta={item.deviceName} actions={<Button appearance="subtle" onClick={() => action("restoreHiddenApp", { ...item })}>{t(payload, "restore", "Restore")}</Button>} />
          </motion.div>)}
        </AnimatePresence>
      </div>
      <Reveal open={hiddenApps.length > 0}><div className={mergeClasses(styles.actionRow, "bt-row-separated bt-row-end")}><Button appearance="secondary" onClick={() => action("restoreAllHiddenApps")}>{t(payload, "restoreAll", "Restore all")}</Button></div></Reveal>
      <Reveal open={hiddenApps.length === 0}><Empty payload={payload} styles={styles} /></Reveal>
    </Section>
    <AnimatePresence initial={false}>
      {hiddenDevices.length > 0 && <motion.div key="hiddenDevices" initial={{ opacity: 0, height: 0, overflow: "hidden" }} animate={{ opacity: 1, height: "auto", transitionEnd: { overflow: "visible" } }} exit={{ opacity: 0, height: 0, overflow: "hidden" }} transition={timing(reduce)}>
        <Section icon={<Volume2Icon size={18} />} title={t(payload, "hiddenDevices", "Hidden devices")} anchor="hiddenDevices" styles={styles}>
          <div className={styles.list}>
            <AnimatePresence initial={false}>
              {hiddenDevices.map(item => <motion.div key={item.deviceId} {...itemMotion(reduce)}>
                <ListRow styles={styles} title={item.displayName || item.deviceId} actions={<Button appearance="subtle" onClick={() => action("restoreHiddenDevice", { deviceId: item.deviceId })}>{t(payload, "restore", "Restore")}</Button>} />
              </motion.div>)}
            </AnimatePresence>
          </div>
          <div className={mergeClasses(styles.actionRow, "bt-row-separated bt-row-end")}><Button appearance="secondary" onClick={() => action("restoreAllHiddenDevices")}>{t(payload, "restoreAllDevices", "Restore all devices")}</Button></div>
        </Section>
      </motion.div>}
    </AnimatePresence>
  </>;
}

function MousePage({ payload, styles, setSetting }: PageProps) {
  const focusLostEnabled = Boolean(payload.values.useFocusLostVolume);
  return <>
    <Section icon={<MouseIcon size={18} />} title={t(payload, "scrollWheelTitle", "Mouse wheel")} description={t(payload, "scrollWheelDescription", "Control volume with the wheel")} anchor="wheel" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="useScrollWheelInTray" label={t(payload, "useScrollWheelInTray", "Change volume over the tray icon")} description={t(payload, "useScrollWheelInTrayDescription", "Scroll over the notification icon.")} /><ToggleRow payload={payload} styles={styles} settingKey="useGlobalMouseWheelHook" label={t(payload, "useGlobalMouseWheelHook", "Change volume while the interface is open")} description={t(payload, "useGlobalMouseWheelHookDescription", "The wheel controls volume from the interface.")} /></Section>
    <Section icon={<Volume2Icon size={18} />} title={t(payload, "volumeScaleTitle", "Volume scale")} description={t(payload, "volumeScaleDescription", "Volume step distribution")} anchor="scale" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="useLogarithmicVolume" label={t(payload, "useLogarithmicVolume", "Use logarithmic scale")} /><ToggleRow payload={payload} styles={styles} settingKey="useVolumeTickSound" label={t(payload, "useVolumeTickSound", "Play a sound while adjusting")} description={t(payload, "useVolumeTickSoundDescription", "Play a light tick while changing volume.")} /></Section>
    <Section icon={<ArrowLeftRightIcon size={18} />} title={t(payload, "deviceChangeTitle", "Device change")} description={t(payload, "deviceChangeDescription", "Toast when the default playback device switches")} anchor="deviceChange" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="notifyOnDeviceChange" label={t(payload, "notifyOnDeviceChange", "Show a notification when the default device changes")} /></Section>
    <Section icon={<EyeOffIcon size={18} />} title={t(payload, "focusLostTitle", "Focus lost")} description={t(payload, "focusLostDescription", "Mute or reduce apps when another window is in front")} anchor="focusLost" styles={styles}>
      <ToggleRow payload={payload} styles={styles} settingKey="useFocusLostVolume" label={t(payload, "useFocusLostVolume", "Lower volume of apps that lose focus")} setSetting={setSetting} />
      <Reveal open={focusLostEnabled} className="bt-reveal-rows">
        <RangeRow payload={payload} styles={styles} label={t(payload, "focusLostAttenuate", "Background volume (0% mutes)")} description={t(payload, "focusLostAttenuateHint", "Locked and keep-muted rules are left alone.")} value={Number(payload.values.focusLostAttenuatePercent)} min={0} max={100} suffix="%" onCommit={value => setSetting("focusLostAttenuatePercent", value)} />
        <RangeRow payload={payload} styles={styles} label={t(payload, "focusLostFade", "Fade duration")} description={t(payload, "focusLostFadeHint", "0 ms is immediate.")} value={Number(payload.values.focusLostFadeDurationMs)} min={0} max={5000} step={100} suffix=" ms" onCommit={value => setSetting("focusLostFadeDurationMs", value)} />
        <SelectRow styles={styles} label={t(payload, "focusLostScope", "Applications affected")} description={t(payload, "focusLostSelectedHint", "Use the Focus lost checkbox on an app rule to select an application.")} value={Number(payload.values.focusLostSelectedAppsOnly)} options={[{ value: 0, label: t(payload, "focusLostAllApps", "All applications") }, { value: 1, label: t(payload, "focusLostSelectedApps", "Only applications selected in App rules") }]} onChange={value => setSetting("focusLostSelectedAppsOnly", value === 1)} />
      </Reveal>
    </Section>
  </>;
}

function ShortcutsPage({ payload, styles, setSetting }: PageProps) {
  const capture = useHotkeyCapture();
  return <>
    <Section icon={<KeyboardIcon size={18} />} title={t(payload, "shortcutsGlobal", "Global shortcuts")} anchor="shortcuts" styles={styles}>
      <div className={styles.list}>
        {payload.collections.hotkeys.map(hotkey => <ListRow key={hotkey.id} styles={styles} title={hotkey.label} meta={hotkey.description && !sameText(hotkey.description, hotkey.label) ? hotkey.description : undefined} actions={<HotkeyControl payload={payload} id={hotkey.id} value={hotkey.value} capture={capture} />} />)}
      </div>
    </Section>
    {payload.collections.deviceHotkeys.length > 0 && <Section icon={<Volume2Icon size={18} />} title={t(payload, "deviceShortcuts", "Device shortcuts")} description={t(payload, "deviceShortcutsDesc", "Switch the default playback device with one shortcut.")} anchor="deviceShortcuts" styles={styles}>
      <div className={styles.list}>
        {payload.collections.deviceHotkeys.map(hotkey => <ListRow key={hotkey.id} styles={styles} title={hotkey.label} badge={hotkey.isDefault ? <span className="badge-default-polished">{t(payload, "defaultDeviceBadge", "Default")}</span> : undefined} actions={<HotkeyControl payload={payload} id={hotkey.id} value={hotkey.value} capture={capture} />} />)}
      </div>
      <ToggleRow className="bt-row-separated" payload={payload} styles={styles} settingKey="showDeviceSwitchNotification" label={t(payload, "showDeviceSwitchNotification", "Show notification when switching devices")} description={t(payload, "showDeviceSwitchNotificationDescription", "Display a toast notification when switching the default device via hotkey.")} setSetting={setSetting} />
    </Section>}
  </>;
}

// ── QuickTrumpet ──────────────────────────────────────────────────────────────
// A preset is any mix of three parts: default devices (output / calls output /
// microphone / calls microphone), device volumes for checked outputs, and app
// volumes. The page captures exactly what is checked, then lets every saved
// value be inspected and edited in place.

const ROLE_LABELS: [string, string][] = [
  ["rolePlayback", "Output"],
  ["rolePlaybackComms", "Calls output"],
  ["roleRecording", "Microphone"],
  ["roleRecordingComms", "Calls microphone"],
];

const EMPTY_AUDIO: AudioState = { devices: [], playbackEndpoints: [], recordingEndpoints: [], defaults: [], appCount: 0 };

/** "{0} of {1}" → "2 of 3" */
const format = (template: string, ...args: (string | number)[]) => template.replace(/\{(\d+)\}/g, (_, index: string) => String(args[Number(index)] ?? ""));

const roleLabel = (payload: SettingsPayload, role: number) => {
  const [key, fallback] = ROLE_LABELS[role] ?? ROLE_LABELS[0];
  return t(payload, key, fallback);
};

/** Checkbox row of the capture form; its option list opens under it on demand. */
function IncludeRow({ id, checked, onChange, label, description, meta, open, onToggleOpen, chooseLabel, children }: { id: string; checked: boolean; onChange: (checked: boolean) => void; label: string; description: string; meta?: string; open?: boolean; onToggleOpen?: () => void; chooseLabel?: string; children?: ReactNode }) {
  return <div className="bt-include">
    <div className="bt-include-head">
      <Checkbox id={id} checked={checked} onChange={(_, data) => onChange(Boolean(data.checked))} label={<span className="bt-include-copy"><Text weight="semibold">{label}</Text><Text className="bt-include-desc" size={200}>{description}</Text></span>} />
      <span className="bt-include-side">
        {meta && checked && <span className="chip-polished bt-tabular">{meta}</span>}
        {onToggleOpen && <Button appearance="subtle" size="small" disabled={!checked} aria-expanded={Boolean(open && checked)} onClick={onToggleOpen}>
          <span className="bt-btn-content">{chooseLabel}<ChevronDownIcon size={15} className={mergeClasses("bt-choose-chevron", open && checked && "bt-choose-chevron-open")} /></span>
        </Button>}
      </span>
    </div>
    {children && <Reveal open={Boolean(open && checked)}>{children}</Reveal>}
  </div>;
}

function PresetCapture({ payload, styles, action, audio }: { payload: SettingsPayload; styles: Styles; action: Action; audio: AudioState }) {
  const [name, setName] = useState("");
  const [deviceVolumes, setDeviceVolumes] = useState(true);
  // null = follow the current default output until the user picks devices.
  const [deviceIds, setDeviceIds] = useState<string[] | null>(null);
  const [appVolumes, setAppVolumes] = useState(true);
  const [routeApps, setRouteApps] = useState(false);
  const [defaults, setDefaults] = useState(false);
  const [roles, setRoles] = useState<number[]>([0, 1, 2, 3]);
  const [open, setOpen] = useState<"devices" | "defaults" | null>(null);
  const [savedFlash, flashSaved] = useFlash<boolean>();

  const defaultIds = audio.devices.filter(device => device.isDefault).map(device => device.id);
  const selectedIds = (deviceIds ?? defaultIds).filter(id => audio.devices.some(device => device.id === id));
  const availableRoles = audio.defaults.filter(slot => slot.deviceId).map(slot => slot.role as number);
  const selectedRoles = roles.filter(role => availableRoles.includes(role));
  const hasDevices = deviceVolumes && selectedIds.length > 0;
  const hasDefaults = defaults && selectedRoles.length > 0;
  const nothing = !hasDevices && !appVolumes && !hasDefaults;

  const toggleDevice = (id: string, checked: boolean) => setDeviceIds(() => {
    const next = selectedIds.filter(item => item !== id);
    return checked ? [...next, id] : next;
  });
  const toggleRole = (role: number, checked: boolean) => setRoles(current => checked ? [...current.filter(item => item !== role), role] : current.filter(item => item !== role));
  const toggleOpen = (part: "devices" | "defaults") => setOpen(current => current === part ? null : part);

  // The host generates a default name, so Save stays enabled with an empty field.
  const save = () => {
    if (nothing) return;
    action("profileCapture", {
      name,
      includeDeviceVolumes: hasDevices,
      deviceIds: selectedIds,
      includeAppVolumes: appVolumes,
      routeApps: appVolumes && routeApps,
      defaultRoles: hasDefaults ? selectedRoles : [],
    });
    setName("");
    flashSaved(true);
  };

  const defaultsMeta = audio.defaults
    .filter(slot => selectedRoles.includes(slot.role) && (slot.role === 0 || slot.role === 2))
    .map(slot => slot.name)
    .join(" + ");

  return <div className="bt-capture">
    <div className={mergeClasses(styles.actionRow, "bt-save-bar")}>
      <Input className={styles.controlGrow} value={name} onChange={(_, data) => setName(data.value)} onKeyDown={event => { if (event.key === "Enter") save(); }} placeholder={t(payload, "presetName", "Preset name")} aria-label={t(payload, "presetName", "Preset name")} />
      <Button appearance="primary" disabled={nothing && !savedFlash} onClick={save}><FeedbackContent done={Boolean(savedFlash)} icon={<SaveIcon size={17} />} label={t(payload, "save", "Save")} doneLabel={t(payload, "saved", "Saved")} /></Button>
      <Button appearance="subtle" icon={<UploadIcon size={17} />} onClick={() => action("profileImport")}>{t(payload, "import", "Import")}</Button>
    </div>
    <Text className="bt-overline bt-capture-label" as="h3">{t(payload, "whatToSave", "What this preset saves")}</Text>
    <IncludeRow
      id="qt-include-defaults"
      checked={defaults}
      onChange={setDefaults}
      label={t(payload, "includeDefaults", "Default devices")}
      description={t(payload, "includeDefaultsDesc", "Switch which output and microphone Windows uses.")}
      meta={defaultsMeta || undefined}
      open={open === "defaults"}
      onToggleOpen={() => toggleOpen("defaults")}
      chooseLabel={t(payload, "choose", "Choose")}
    >
      <div className="bt-checklist">
        {audio.defaults.map(slot => <Checkbox
          key={slot.role}
          className="bt-check-item"
          disabled={!slot.deviceId}
          checked={Boolean(slot.deviceId) && roles.includes(slot.role)}
          onChange={(_, data) => toggleRole(slot.role, Boolean(data.checked))}
          label={<span className="bt-check-label"><span className="bt-check-role">{roleLabel(payload, slot.role)}</span><span className="bt-check-name truncate-text">{slot.name || t(payload, "noDevice", "None available")}</span></span>}
        />)}
      </div>
    </IncludeRow>
    <IncludeRow
      id="qt-include-devices"
      checked={deviceVolumes}
      onChange={setDeviceVolumes}
      label={t(payload, "includeDevices", "Device volumes")}
      description={t(payload, "includeDevicesDesc", "Volume and mute of the output devices you check.")}
      meta={format(t(payload, "devicesCheckedFormat", "{0} of {1} checked"), selectedIds.length, audio.devices.length)}
      open={open === "devices"}
      onToggleOpen={() => toggleOpen("devices")}
      chooseLabel={t(payload, "choose", "Choose")}
    >
      <div className="bt-checklist">
        {audio.devices.map(device => <Checkbox
          key={device.id}
          className="bt-check-item"
          checked={selectedIds.includes(device.id)}
          onChange={(_, data) => toggleDevice(device.id, Boolean(data.checked))}
          label={<span className="bt-check-label">
            <span className="bt-check-name truncate-text">{device.name}</span>
            {device.isDefault && <span className="badge-default-polished">{t(payload, "defaultDeviceBadge", "Default")}</span>}
            <span className="bt-check-meta bt-tabular">{device.muted ? t(payload, "muteEntry", "Muted") : `${device.volume}%`}</span>
          </span>}
        />)}
      </div>
    </IncludeRow>
    <IncludeRow
      id="qt-include-apps"
      checked={appVolumes}
      onChange={setAppVolumes}
      label={t(payload, "includeApps", "App volumes")}
      description={t(payload, "includeAppsDesc", "Volume and mute of each app in the mixer.")}
      meta={format(t(payload, "appsOpenFormat", "{0} apps open now"), audio.appCount)}
    />
    <Reveal open={appVolumes}>
      <div className="bt-include-sub">
        <Checkbox checked={routeApps} onChange={(_, data) => setRouteApps(Boolean(data.checked))} label={<span className="bt-include-copy"><Text>{t(payload, "routeApps", "Send apps back to their device")}</Text><Text className="bt-include-desc" size={200}>{t(payload, "routeAppsDesc", "Pins each app to the output it was playing on when saved.")}</Text></span>} />
      </div>
    </Reveal>
    <Reveal open={nothing}><Text className="bt-capture-hint" size={200}>{t(payload, "nothingSelected", "Check at least one thing to save.")}</Text></Reveal>
  </div>;
}

/** One saved value: name, editable volume, mute toggle, remove. */
function PresetEntry({ payload, styles, name, meta, volume, muted, missing, onVolume, onMute, onRemove }: { payload: SettingsPayload; styles: Styles; name: string; meta?: string; volume: number; muted: boolean; missing?: boolean; onVolume: (value: number) => void; onMute: (muted: boolean) => void; onRemove: () => void }) {
  const muteLabel = t(payload, "muteEntry", "Muted");
  const removeLabel = t(payload, "removeEntry", "Remove");
  return <div className="bt-entry">
    <div className="bt-entry-copy">
      <span className="bt-entry-title"><Text className="truncate-text" weight="semibold">{name}</Text>{missing && <span className="chip-polished bt-chip-quiet">{t(payload, "notConnected", "Not connected")}</span>}</span>
      {meta && <Text className={`${styles.listMeta} truncate-text`} size={200}>{meta}</Text>}
    </div>
    <ElasticSlider className="bt-entry-range" value={volume} startingValue={0} maxValue={100} isStepped stepSize={1} suffix="%" locale={payload.locale} ariaLabel={`${name} · ${t(payload, "targetVolume", "Volume")}`} leftIcon={<MinusIcon size={14} />} rightIcon={<PlusIcon size={14} />} onCommit={onVolume} />
    <Button appearance="subtle" size="small" className={mergeClasses("bt-mute", muted && "bt-mute-on")} aria-pressed={muted} aria-label={muteLabel} title={muteLabel} icon={muted ? <VolumeXIcon size={16} /> : <Volume2Icon size={16} />} onClick={() => onMute(!muted)} />
    <Button appearance="subtle" size="small" className="bt-entry-remove" aria-label={`${removeLabel} · ${name}`} title={removeLabel} icon={<XIcon size={15} />} onClick={onRemove} />
  </div>;
}

function PresetGroup({ title, control, children }: { title: string; control?: ReactNode; children: ReactNode }) {
  return <div className="bt-group">
    <div className="bt-group-head"><Text className="bt-overline" as="h4">{title}</Text>{control}</div>
    {children}
  </div>;
}

function PresetDetails({ payload, styles, action, audio, profile, renameValue, setRenameValue, renamed, onRenamed, onDeleted }: { payload: SettingsPayload; styles: Styles; action: Action; audio: AudioState; profile: VolumeProfile; renameValue: string; setRenameValue: (value: string) => void; renamed: boolean; onRenamed: () => void; onDeleted: () => void }) {
  const [updatedFlash, flashUpdated] = useFlash<boolean>();
  const ref = { id: profile.id, index: profile.index };
  const trimmed = renameValue.trim();
  const unusedDevices = audio.devices.filter(device => !profile.devices.some(entry => entry.key === device.id));
  const reduce = useReducedMotion();

  return <div className={styles.accDetail}>
    <PresetGroup title={t(payload, "includeDefaults", "Default devices")}>
      {profile.defaults.map(slot => {
        const endpoints = slot.role >= 2 ? audio.recordingEndpoints : audio.playbackEndpoints;
        const known = endpoints.some(endpoint => endpoint.id === slot.deviceId);
        const label = roleLabel(payload, slot.role);
        return <div className="bt-entry bt-entry-slot" key={slot.role}>
          <div className="bt-entry-copy"><Text weight="semibold">{label}</Text></div>
          <select className={mergeClasses(styles.select, "select-polished", "bt-slot-select")} value={slot.deviceId} aria-label={label} onChange={event => action("profileSetDefault", { ...ref, role: slot.role, deviceId: event.currentTarget.value })}>
            <option value="">{t(payload, "dontChange", "Don't change")}</option>
            {endpoints.map(endpoint => <option key={endpoint.id} value={endpoint.id}>{endpoint.name}</option>)}
            {slot.deviceId && !known && <option value={slot.deviceId}>{`${slot.name} · ${t(payload, "notConnected", "Not connected")}`}</option>}
          </select>
        </div>;
      })}
    </PresetGroup>

    <PresetGroup title={t(payload, "includeDevices", "Device volumes")} control={<Switch checked={profile.includeDeviceVolumes} aria-label={t(payload, "includeDevices", "Device volumes")} onChange={(_, data) => action("profileSetInclude", { ...ref, deviceVolumes: data.checked })} />}>
      <div className={mergeClasses("bt-group-body", !profile.includeDeviceVolumes && "bt-group-off")}>
        <AnimatePresence initial={false}>
          {profile.devices.map(entry => <motion.div key={entry.key} {...itemMotion(reduce)}>
            <PresetEntry payload={payload} styles={styles} name={entry.name} volume={entry.volume} muted={entry.muted} missing={entry.missing}
              onVolume={volume => action("profileDeviceUpdate", { ...ref, key: entry.key, volume })}
              onMute={muted => action("profileDeviceUpdate", { ...ref, key: entry.key, muted })}
              onRemove={() => action("profileDeviceRemove", { ...ref, key: entry.key })} />
          </motion.div>)}
        </AnimatePresence>
        {profile.devices.length === 0 && <Text className="bt-group-empty" size={200}>{t(payload, "noDeviceEntries", "No device volume saved.")}</Text>}
        {unusedDevices.length > 0 && <div className="bt-entry bt-entry-add">
          <select className={mergeClasses(styles.select, "select-polished", "bt-slot-select")} value="" aria-label={t(payload, "addDevice", "Add a device")} onChange={event => { if (event.currentTarget.value) action("profileDeviceAdd", { ...ref, key: event.currentTarget.value }); }}>
            <option value="">{`+ ${t(payload, "addDevice", "Add a device")}`}</option>
            {unusedDevices.map(device => <option key={device.id} value={device.id}>{`${device.name} · ${device.volume}%`}</option>)}
          </select>
        </div>}
      </div>
    </PresetGroup>

    <PresetGroup title={t(payload, "includeApps", "App volumes")} control={<Switch checked={profile.includeAppVolumes} aria-label={t(payload, "includeApps", "App volumes")} onChange={(_, data) => action("profileSetInclude", { ...ref, appVolumes: data.checked })} />}>
      <div className={mergeClasses("bt-group-body", !profile.includeAppVolumes && "bt-group-off")}>
        <AnimatePresence initial={false}>
          {profile.apps.map(entry => <motion.div key={entry.key} {...itemMotion(reduce)}>
            <PresetEntry payload={payload} styles={styles} name={entry.name} meta={entry.deviceName} volume={entry.volume} muted={entry.muted}
              onVolume={volume => action("profileAppUpdate", { ...ref, key: entry.key, volume })}
              onMute={muted => action("profileAppUpdate", { ...ref, key: entry.key, muted })}
              onRemove={() => action("profileAppRemove", { ...ref, key: entry.key })} />
          </motion.div>)}
        </AnimatePresence>
        {profile.apps.length === 0 && <Text className="bt-group-empty" size={200}>{t(payload, "noAppEntries", "No app volume saved.")}</Text>}
        {profile.apps.length > 0 && <label className="bt-entry bt-entry-option" htmlFor={`qt-route-${profile.id}`}>
          <div className="bt-entry-copy"><Text weight="semibold">{t(payload, "routeApps", "Send apps back to their device")}</Text><Text className={styles.listMeta} size={200}>{t(payload, "routeAppsDesc", "Pins each app to the output it was playing on when saved.")}</Text></div>
          <Switch id={`qt-route-${profile.id}`} checked={profile.routeApps} aria-label={t(payload, "routeApps", "Send apps back to their device")} onChange={(_, data) => action("profileSetInclude", { ...ref, routeApps: data.checked })} />
        </label>}
      </div>
    </PresetGroup>

    <div className={mergeClasses(styles.settingRow, "bt-group")}>
      <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "rename", "Rename")}</Text></div>
      <div className={styles.rowActions}>
        <Input value={renameValue} onChange={(_, data) => setRenameValue(data.value)} placeholder={t(payload, "presetName", "Preset name")} aria-label={t(payload, "presetName", "Preset name")} />
        <Button appearance="secondary" disabled={!renamed && (!trimmed || trimmed === profile.name)} onClick={() => { action("profileRename", { ...ref, name: trimmed }); onRenamed(); }}>
          <FeedbackContent done={renamed} label={t(payload, "rename", "Rename")} doneLabel={t(payload, "saved", "Saved")} />
        </Button>
      </div>
    </div>
    <div className={mergeClasses(styles.actionRow, "bt-row-separated bt-row-between bt-preset-actions")}>
      <span className="bt-preset-actions-start">
        <ConfirmButton payload={payload} tone="neutral" label={updatedFlash ? t(payload, "updated", "Updated") : t(payload, "updateFromCurrent", "Update with current mix")} confirmLabel={t(payload, "updateConfirm", "Overwrite values?")} icon={updatedFlash ? <CheckIcon size={16} /> : <RefreshCwIcon size={16} />} title={t(payload, "updateFromCurrentDesc", "Re-reads the current volumes and defaults for what this preset holds.")} onConfirm={() => { action("profileUpdateFromCurrent", ref); flashUpdated(true); }} />
        <Button appearance="subtle" icon={<DownloadIcon size={17} />} onClick={() => action("profileExport", ref)}>{t(payload, "export", "Export")}</Button>
      </span>
      <ConfirmButton payload={payload} label={t(payload, "delete", "Delete")} icon={<Trash2Icon size={16} />} onConfirm={() => { action("profileDelete", { ...ref, confirmed: true }); onDeleted(); }} />
    </div>
  </div>;
}

function ProfilesPage({ payload, styles, action, setSetting }: PageProps) {
  const [expanded, setExpanded] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
  const [appliedFlash, flashApplied] = useFlash<string>();
  const [renamedFlash, flashRenamed] = useFlash<string>();
  const capture = useHotkeyCapture();
  const reduce = useReducedMotion();
  const profiles = payload.collections.profiles;
  const audio = payload.collections.audio ?? EMPTY_AUDIO;
  const cycleHotkeys = payload.collections.hotkeys.filter(hotkey => hotkey.id === "presetNext" || hotkey.id === "presetPrevious");
  const keyOf = (profile: VolumeProfile) => profile.id || profile.slug || profile.name;
  const confirmationOn = Boolean(payload.values.showQuickTrumpetConfirmation);

  const toggle = (profile: VolumeProfile) => {
    const key = keyOf(profile);
    setExpanded(current => current === key ? null : key);
    setRenameValue(profile.name);
  };

  // A freshly captured preset opens, so what it saved is visible right away.
  useNewItem(profiles.map(keyOf), key => {
    setExpanded(key);
    setRenameValue(profiles.find(profile => keyOf(profile) === key)?.name ?? "");
  });

  return <>
    <Section icon={<SlidersHorizontalIcon size={18} />} title={t(payload, "savedProfiles", "Presets")} description={t(payload, "presetsDescription", "A preset can switch your default output and microphone, set device volumes and set app volumes.")} anchor="presets" styles={styles}>
      <Reveal open={profiles.length === 0}>
        <div className="bt-empty-block">
          <span className="bt-empty-icon" aria-hidden="true"><BookmarkPlusIcon size={18} /></span>
          <Text className="bt-empty-text" size={200}>{t(payload, "emptyProfilesHint", "Capture your current mix as your first preset.")}</Text>
        </div>
      </Reveal>
      <PresetCapture payload={payload} styles={styles} action={action} audio={audio} />
      <div className={mergeClasses(styles.accList, profiles.length > 0 && "bt-acc-list-separated")}>
        <AnimatePresence initial={false}>
          {profiles.map(profile => {
            const key = keyOf(profile);
            const isOpen = expanded === key;
            return <motion.div className={styles.accItem} key={key} {...itemMotion(reduce)}>
              <div
                className={mergeClasses(styles.accHeader, "acc-header-polished")}
                role="button"
                tabIndex={0}
                aria-expanded={isOpen}
                onClick={() => toggle(profile)}
                onKeyDown={event => headerKeys(event, () => toggle(profile))}
              >
                <div className={styles.accCopy}>
                  <div className="list-row-title">
                    <Text weight="semibold">{profile.name}</Text>
                    {profile.isLastApplied && <span className="badge-default-polished">{t(payload, "lastApplied", "Last applied")}</span>}
                  </div>
                  <Text className={`${styles.listMeta} truncate-text`} size={200}>{profile.details}</Text>
                </div>
                <span className={styles.accInlineControls} onClick={stop}>
                  <HotkeyControl payload={payload} id={`profile:${profile.index}`} value={profile.hotkey} capture={capture} />
                  <Button appearance="secondary" onClick={() => { action("profileApply", { id: profile.id, index: profile.index }); flashApplied(key); }}>
                    <FeedbackContent done={appliedFlash === key} label={t(payload, "apply", "Apply")} doneLabel={t(payload, "applied", "Applied")} />
                  </Button>
                </span>
                <ChevronDownIcon size={17} className={mergeClasses(styles.accChevron, isOpen && styles.accChevronOpen)} />
              </div>
              <Reveal open={isOpen}>
                <PresetDetails
                  payload={payload}
                  styles={styles}
                  action={action}
                  audio={audio}
                  profile={profile}
                  renameValue={renameValue}
                  setRenameValue={setRenameValue}
                  renamed={renamedFlash === key}
                  onRenamed={() => flashRenamed(key)}
                  onDeleted={() => setExpanded(null)}
                />
              </Reveal>
            </motion.div>;
          })}
        </AnimatePresence>
      </div>
    </Section>
    <Section icon={<KeyboardIcon size={18} />} title={t(payload, "cycleTitle", "Cycle through presets")} description={t(payload, "cycleDesc", "One shortcut steps through your presets in list order and wraps around.")} anchor="presetCycle" styles={styles}>
      <div className={styles.list}>
        {cycleHotkeys.map(hotkey => <ListRow key={hotkey.id} styles={styles} title={hotkey.label} meta={hotkey.description} actions={<HotkeyControl payload={payload} id={hotkey.id} value={hotkey.value} capture={capture} />} />)}
      </div>
    </Section>
    <Section icon={<BellIcon size={18} />} title={t(payload, "qtNotification", "Confirmation")} anchor="presetNotification" styles={styles}>
      <ToggleRow payload={payload} styles={styles} settingKey="showQuickTrumpetConfirmation" label={t(payload, "confirmation", "Show a confirmation when a preset is applied")} setSetting={setSetting} />
      <Reveal open={confirmationOn} className="bt-reveal-rows">
        <RangeRow payload={payload} styles={styles} label={t(payload, "notificationDuration", "Notification duration")} description={t(payload, "notificationDurationDesc", "How long the confirmation stays on screen.")} value={Number(payload.values.quickTrumpetNotificationSeconds) || 3} min={1} max={10} suffix={` ${t(payload, "secondsShort", "s")}`} onCommit={value => setSetting("quickTrumpetNotificationSeconds", value)} />
      </Reveal>
    </Section>
  </>;
}

function RulesPage({ payload, styles, action }: PageProps) {
  const [exeName, setExeName] = useState("");
  const [openRule, setOpenRule] = useState<string | null>(null);
  const [openFolder, setOpenFolder] = useState<string | null>(null);
  const reduce = useReducedMotion();
  const rules = payload.collections.appRules;
  const folders = payload.collections.folderRules;

  // A freshly added rule/folder opens so its settings are one glance away.
  useNewItem(rules.map(rule => rule.exeName), setOpenRule);
  useNewItem(folders.map(rule => rule.id), setOpenFolder);

  const add = () => { if (!exeName.trim()) return; action("appRuleAdd", { exeName }); setExeName(""); };
  const summarize = (rule: AppRule) => {
    const parts: string[] = [];
    if (rule.hardMuted) parts.push(t(payload, "hardMute", "Keep muted"));
    if (rule.focusLost) parts.push(t(payload, "focusLostRule", "Focus lost"));
    if (rule.volumeMode === 1) parts.push(`${rule.volumePercent}% · ${t(payload, "modeLaunch", "Set at launch")}`);
    if (rule.volumeMode === 2) parts.push(`${rule.volumePercent}% · ${t(payload, "modeLock", "Lock")}`);
    return parts;
  };
  const toggleRule = (key: string) => setOpenRule(current => current === key ? null : key);
  const toggleFolder = (key: string) => setOpenFolder(current => current === key ? null : key);

  return <>
    <Section icon={<ListChecksIcon size={18} />} title={t(payload, "appRules", "Application rules")} description={t(payload, "appRulesDescription", "Persistent mute and volume behavior per app.")} anchor="rules" styles={styles}>
      <div className={mergeClasses(styles.actionRow, "bt-add-row")}>
        <Input className={styles.controlGrow} value={exeName} onChange={(_, data) => setExeName(data.value)} onKeyDown={event => { if (event.key === "Enter") add(); }} placeholder={t(payload, "appPlaceholder", "Application executable")} aria-label={t(payload, "appPlaceholder", "Application executable")} />
        <Button appearance="secondary" onClick={() => action("appRuleBrowse")}>{t(payload, "browse", "Browse")}</Button>
        <Button appearance="primary" icon={<PlusIcon size={17} />} disabled={!exeName.trim()} onClick={add}>{t(payload, "addApp", "Add")}</Button>
      </div>
      <div className={mergeClasses(styles.accList, rules.length > 0 && "bt-acc-list-separated")}>
        <AnimatePresence initial={false}>
          {rules.map(rule => {
            const isOpen = openRule === rule.exeName;
            const badges = summarize(rule);
            return <motion.div className={styles.accItem} key={rule.exeName} {...itemMotion(reduce)}>
              <div className={mergeClasses(styles.accHeader, "acc-header-polished")} role="button" tabIndex={0} aria-expanded={isOpen} onClick={() => toggleRule(rule.exeName)} onKeyDown={event => headerKeys(event, () => toggleRule(rule.exeName))}>
                <div className={styles.accCopy}>
                  <div className="list-row-title"><Text weight="semibold">{rule.displayName || rule.exeName}</Text></div>
                  <Text className={`${styles.listMeta} truncate-text`} size={200}>{rule.exeName}.exe</Text>
                </div>
                {badges.length > 0 && <div className={styles.ruleBadges}>{badges.map((badge, index) => <span className="chip-polished" key={index}>{badge}</span>)}</div>}
                <span className={mergeClasses(styles.accInlineControls, "bt-acc-danger-slot")} onClick={stop}>
                  <ConfirmButton payload={payload} iconOnly label={t(payload, "delete", "Delete")} icon={<Trash2Icon size={16} />} onConfirm={() => action("appRuleRemove", { exeName: rule.exeName, confirmed: true })} />
                </span>
                <ChevronDownIcon size={17} className={mergeClasses(styles.accChevron, isOpen && styles.accChevronOpen)} />
              </div>
              <Reveal open={isOpen}>
                <div className={styles.accDetail}>
                  <label className={`${styles.settingRow} setting-row-polished`} htmlFor={`rule-mute-${rule.exeName}`}>
                    <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "hardMute", "Keep muted")}</Text></div>
                    <Switch id={`rule-mute-${rule.exeName}`} checked={rule.hardMuted} aria-label={t(payload, "hardMute", "Keep muted")} onChange={(_, data) => action("appRuleUpdate", { exeName: rule.exeName, hardMuted: data.checked })} />
                  </label>
                  <label className={`${styles.settingRow} setting-row-polished`} htmlFor={`rule-focus-${rule.exeName}`}>
                    <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "focusLostRule", "Focus lost")}</Text></div>
                    <Switch id={`rule-focus-${rule.exeName}`} checked={rule.focusLost} aria-label={t(payload, "focusLostRule", "Focus lost")} onChange={(_, data) => action("appRuleUpdate", { exeName: rule.exeName, focusLost: data.checked })} />
                  </label>
                  <div className={styles.settingRow}>
                    <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "volumeBehavior", "Volume behavior")}</Text></div>
                    <select className={mergeClasses(styles.select, "select-polished")} value={rule.volumeMode} aria-label={t(payload, "volumeBehavior", "Volume behavior")} onChange={event => action("appRuleUpdate", { exeName: rule.exeName, volumeMode: Number(event.currentTarget.value) })}>
                      <option value={0}>{t(payload, "modeNone", "None")}</option>
                      <option value={1}>{t(payload, "modeLaunch", "Set at launch")}</option>
                      <option value={2}>{t(payload, "modeLock", "Lock")}</option>
                    </select>
                  </div>
                  <Reveal open={rule.volumeMode > 0} className="bt-reveal-rows">
                    <div className={styles.settingRow}>
                      <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "targetVolume", "Target volume")}</Text></div>
                      <InlineRange payload={payload} styles={styles} label={t(payload, "targetVolume", "Target volume")} value={rule.volumePercent} onCommit={value => action("appRuleUpdate", { exeName: rule.exeName, volumePercent: value })} />
                    </div>
                  </Reveal>
                </div>
              </Reveal>
            </motion.div>;
          })}
        </AnimatePresence>
      </div>
      <Reveal open={rules.length === 0} className="bt-reveal-rows"><Empty payload={payload} styles={styles} /></Reveal>
      <Reveal open={rules.length > 0}>
        <div className={mergeClasses(styles.actionRow, "bt-row-separated bt-row-end")}>
          <ConfirmButton payload={payload} label={t(payload, "clearAllRules", "Clear all rules")} confirmLabel={t(payload, "confirmClearAll", "Clear all rules?")} icon={<Trash2Icon size={16} />} onConfirm={() => action("appRuleClear", { confirmed: true })} />
        </div>
      </Reveal>
    </Section>
    <Section icon={<FolderIcon size={18} />} title={t(payload, "folderRules", "Folder defaults")} description={t(payload, "folderRulesDescription", "Starting volume for apps launched from a folder.")} anchor="folders" styles={styles}>
      <div className={mergeClasses(styles.actionRow, "bt-add-row bt-row-end")}>
        <Button appearance="secondary" icon={<FolderPlusIcon size={17} />} onClick={() => action("folderRuleAdd")}>{t(payload, "addFolder", "Add folder")}</Button>
      </div>
      <div className={mergeClasses(styles.accList, folders.length > 0 && "bt-acc-list-separated")}>
        <AnimatePresence initial={false}>
          {folders.map(rule => {
            const isOpen = openFolder === rule.id;
            return <motion.div className={styles.accItem} key={rule.id} {...itemMotion(reduce)}>
              <div className={mergeClasses(styles.accHeader, "acc-header-polished")} role="button" tabIndex={0} aria-expanded={isOpen} onClick={() => toggleFolder(rule.id)} onKeyDown={event => headerKeys(event, () => toggleFolder(rule.id))}>
                <div className={styles.accCopy}>
                  <div className="list-row-title"><Text weight="semibold">{rule.folderPath.split("\\").filter(Boolean).pop() || rule.folderPath}</Text></div>
                  <Text className={`${styles.listMeta} truncate-text`} size={200}>{rule.folderPath}</Text>
                </div>
                <span className="chip-polished">{rule.volumePercent}%</span>
                <ChevronDownIcon size={17} className={mergeClasses(styles.accChevron, isOpen && styles.accChevronOpen)} />
              </div>
              <Reveal open={isOpen}>
                <div className={styles.accDetail}>
                  <div className={styles.settingRow}>
                    <div className={styles.settingCopy}><Text weight="semibold">{t(payload, "targetVolume", "Target volume")}</Text></div>
                    <InlineRange payload={payload} styles={styles} label={t(payload, "targetVolume", "Target volume")} value={rule.volumePercent} onCommit={value => action("folderRuleUpdate", { ...rule, volumePercent: value })} />
                  </div>
                  <div className={mergeClasses(styles.actionRow, "bt-row-separated bt-row-between")}>
                    <Button appearance="secondary" icon={<FolderIcon size={17} />} onClick={() => action("folderRuleBrowse", { id: rule.id })}>{t(payload, "changeFolder", "Change folder")}</Button>
                    <ConfirmButton payload={payload} label={t(payload, "delete", "Delete")} icon={<Trash2Icon size={16} />} onConfirm={() => action("folderRuleRemove", { id: rule.id, confirmed: true })} />
                  </div>
                </div>
              </Reveal>
            </motion.div>;
          })}
        </AnimatePresence>
      </div>
      <Reveal open={folders.length === 0} className="bt-reveal-rows"><Empty payload={payload} styles={styles} text={t(payload, "folderRulesEmpty", "No folder defaults.")} /></Reveal>
    </Section>
  </>;
}

// ── Appearance ────────────────────────────────────────────────────────────────

type ColorKey = "sliderThumbColor" | "sliderTrackFillColor" | "sliderTrackBackgroundColor" | "peakMeterColor" | "windowBackgroundColor" | "textColor" | "accentGlowColor";
const COLOR_KEYS: ColorKey[] = ["sliderThumbColor", "sliderTrackFillColor", "sliderTrackBackgroundColor", "peakMeterColor", "windowBackgroundColor", "textColor", "accentGlowColor"];

/** Accepts #RGB, #RRGGBB (with or without #) and the host's #AARRGGBB fallback. */
function parseHex(raw: unknown): string | null {
  const value = String(raw ?? "").trim().replace(/^#/, "");
  if (/^[0-9a-f]{6}$/i.test(value)) return `#${value.toUpperCase()}`;
  if (/^[0-9a-f]{3}$/i.test(value)) return `#${value.split("").map(c => c + c).join("").toUpperCase()}`;
  if (/^[0-9a-f]{8}$/i.test(value)) return `#${value.slice(2).toUpperCase()}`;
  return null;
}

function rgba(hex: string, alpha: number) {
  const parsed = parseHex(hex) ?? "#000000";
  const n = parseInt(parsed.slice(1), 16);
  return `rgba(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}, ${Math.min(Math.max(alpha, 0), 1)})`;
}

/** Per-key throttle (≤ 1 message per interval) with a trailing commit of the latest value. */
function useThrottledSetting(setSetting: SetSetting, interval = COLOR_SEND_INTERVAL_MS) {
  const setRef = useRef(setSetting);
  setRef.current = setSetting;
  const entries = useRef(new Map<SettingKey, { last: number; timer?: number; pending?: SettingValue }>());
  useEffect(() => () => {
    entries.current.forEach((entry, key) => {
      window.clearTimeout(entry.timer);
      if (entry.pending !== undefined) setRef.current(key, entry.pending);
    });
  }, []);
  return useCallback((key: SettingKey, value: SettingValue) => {
    const map = entries.current;
    const entry = map.get(key) ?? { last: 0 };
    map.set(key, entry);
    const wait = entry.last + interval - performance.now();
    if (wait <= 0 && entry.timer === undefined) {
      entry.last = performance.now();
      setRef.current(key, value);
      return;
    }
    entry.pending = value;
    if (entry.timer === undefined) {
      entry.timer = window.setTimeout(() => {
        entry.timer = undefined;
        entry.last = performance.now();
        const latest = entry.pending;
        entry.pending = undefined;
        if (latest !== undefined) setRef.current(key, latest);
      }, Math.max(wait, 0));
    }
  }, [interval]);
}

function ColorRow({ payload, styles, label, value, onChange }: { payload: SettingsPayload; styles: Styles; label: string; value: string; onChange: (hex: string) => void }) {
  const [draft, setDraft] = useState(value);
  const [editing, setEditing] = useState(false);
  const revert = useRef(false);
  const commit = () => {
    setEditing(false);
    if (revert.current) { revert.current = false; return; }
    const hex = parseHex(draft);
    if (hex && hex !== value) onChange(hex);
  };
  const invalid = editing && draft.trim().length >= 4 && !parseHex(draft);
  return <div className={styles.settingRow}>
    <Text weight="semibold">{label}</Text>
    <div className={mergeClasses(styles.rowActions, "bt-color-controls")}>
      <input
        className={mergeClasses("bt-hex-input", invalid && "bt-hex-invalid")}
        value={editing ? draft : value}
        spellCheck={false}
        autoComplete="off"
        maxLength={9}
        aria-label={`${label} · ${t(payload, "hexColor", "Hex color")}`}
        aria-invalid={invalid || undefined}
        onFocus={event => { setDraft(value); setEditing(true); event.currentTarget.select(); }}
        onChange={event => setDraft(event.currentTarget.value)}
        onBlur={commit}
        onKeyDown={event => {
          if (event.key === "Enter") event.currentTarget.blur();
          if (event.key === "Escape") { revert.current = true; event.currentTarget.blur(); }
        }}
      />
      <input className={`${styles.colorInput} color-input-polished`} type="color" value={value.toLowerCase()} aria-label={label} onChange={event => onChange(event.currentTarget.value.toUpperCase())} />
    </div>
  </div>;
}

function ThemePreview({ payload, colors, opacity }: { payload: SettingsPayload; colors: Record<ColorKey, string>; opacity: number }) {
  const label = t(payload, "preview", "Preview");
  const rows = [{ level: 72, peak: 58 }, { level: 40, peak: 31 }];
  return <div className="bt-preview" role="img" aria-label={label}>
    <span className="bt-preview-label">{label}</span>
    <div className="bt-preview-stage">
      <div
        className="bt-preview-flyout"
        style={{
          backgroundColor: rgba(colors.windowBackgroundColor, opacity),
          color: colors.textColor,
          boxShadow: `inset 0 1px 0 rgba(255,255,255,.06), 0 0 0 1px ${rgba(colors.accentGlowColor, 0.35)}, 0 10px 32px ${rgba(colors.accentGlowColor, 0.24)}`,
        }}
      >
        <div className="bt-preview-device">
          <span className="bt-preview-name">{payload.appName || "BetterTrumpet"}</span>
        </div>
        {rows.map((row, index) => <div className="bt-preview-row" key={index}>
          <span className="bt-preview-app" style={{ backgroundColor: rgba(colors.accentGlowColor, 0.85) }} />
          <span className="bt-preview-slider">
            <span className="bt-preview-track" style={{ backgroundColor: colors.sliderTrackBackgroundColor }}>
              <span className="bt-preview-fill" style={{ width: `${row.level}%`, backgroundColor: colors.sliderTrackFillColor }} />
            </span>
            <span className="bt-preview-peak" style={{ width: `${row.peak}%`, backgroundColor: colors.peakMeterColor }} />
            <span className="bt-preview-thumb" style={{ left: `${row.level}%`, backgroundColor: colors.sliderThumbColor }} />
          </span>
          <span className="bt-preview-value">{row.level}</span>
        </div>)}
      </div>
    </div>
  </div>;
}

function AppearancePage({ payload, styles, setSetting, action }: PageProps) {
  const reduce = useReducedMotion();
  const [themeName, setThemeName] = useState("");
  const [savedFlash, flashSaved] = useFlash<boolean>();
  const [overrides, setOverrides] = useState<Partial<Record<ColorKey, string>>>({});
  const [liveOpacity, setLiveOpacity] = useState<number | null>(null);
  const editedAt = useRef<Partial<Record<ColorKey, number>>>({});
  const sendThrottled = useThrottledSetting(setSetting);

  // Local edits win until the host echoes them back (or they go stale), so the
  // swatch, hex field and preview never jump back mid-drag.
  useEffect(() => {
    setOverrides(current => {
      let changed = false;
      const next = { ...current };
      for (const key of Object.keys(current) as ColorKey[]) {
        const age = performance.now() - (editedAt.current[key] ?? 0);
        if (parseHex(payload.values[key]) === current[key] || age > 900) { delete next[key]; changed = true; }
      }
      return changed ? next : current;
    });
  }, [payload.values]);
  useEffect(() => { setLiveOpacity(null); }, [payload.values.windowBackgroundOpacity]);

  const colors = Object.fromEntries(COLOR_KEYS.map(key => [key, overrides[key] ?? parseHex(payload.values[key]) ?? "#000000"])) as Record<ColorKey, string>;
  const setColor = (key: ColorKey, hex: string) => {
    editedAt.current[key] = performance.now();
    setOverrides(current => ({ ...current, [key]: hex }));
    sendThrottled(key, hex);
  };
  const opacityPercent = Math.round(Number(payload.values.windowBackgroundOpacity) * 100);
  const previewOpacity = (liveOpacity ?? opacityPercent) / 100;

  const colorFields: { key: ColorKey; label: string }[] = [
    { key: "sliderThumbColor", label: t(payload, "sliderThumb", "Thumb") },
    { key: "sliderTrackFillColor", label: t(payload, "sliderFill", "Fill") },
    { key: "sliderTrackBackgroundColor", label: t(payload, "sliderTrack", "Track") },
    { key: "peakMeterColor", label: t(payload, "peakColor", "Peak") },
    { key: "windowBackgroundColor", label: t(payload, "windowColor", "Window") },
    { key: "textColor", label: t(payload, "textColor", "Text") },
    { key: "accentGlowColor", label: t(payload, "accentColor", "Accent") },
  ];
  const peakStyles = [
    t(payload, "peakStyleClassic", "Classic"),
    t(payload, "peakStyleDotted", "Dotted"),
    t(payload, "peakStyleBlocks", "Blocks"),
    t(payload, "peakStyleBars", "Bars"),
    t(payload, "peakStyleWave", "Wave"),
  ];
  const themes = payload.collections.themes;
  const isCustom = !themes.some(theme => theme.name === payload.collections.activeThemeName);
  const chipMotion = {
    initial: { opacity: 0, scale: reduce ? 1 : 0.94 },
    animate: { opacity: 1, scale: 1 },
    exit: { opacity: 0, scale: reduce ? 1 : 0.94 },
    transition: timing(reduce, 0.18),
  };
  const checkMark = (active: boolean) => <AnimatePresence initial={false}>
    {active && <motion.span key="check" className="bt-check" initial={{ opacity: 0, scale: 0.5, width: 0 }} animate={{ opacity: 1, scale: 1, width: 14 }} exit={{ opacity: 0, scale: 0.5, width: 0 }} transition={timing(reduce, 0.18)}>
      <CheckIcon size={14} className={styles.themeActiveMark} />
    </motion.span>}
  </AnimatePresence>;
  const saveTheme = () => {
    const trimmed = themeName.trim();
    if (!trimmed) return;
    action("themeSave", { name: trimmed });
    setThemeName("");
    flashSaved(true);
  };

  return <>
    <Section icon={<BlendIcon size={18} />} title={t(payload, "presets", "Presets")} description={t(payload, "appearanceDescription", "Choose a coordinated color palette, then fine tune it below.")} anchor="themes" styles={styles}>
      <div className={mergeClasses(styles.themeFlow, "bt-theme-flow")}>
        <AnimatePresence initial={false} mode="popLayout">
          {themes.map(theme => {
            const isActive = payload.collections.activeThemeName === theme.name;
            return <motion.div layout={reduce ? false : "position"} className={styles.themeItem} key={theme.name} {...chipMotion}>
              <button
                className={mergeClasses(styles.themeChip, "theme-row-polished", isActive && styles.themeChipSelected)}
                aria-pressed={isActive}
                onClick={() => action("themeSelect", { name: theme.name })}
              >
                <span className={styles.swatches}>{theme.colors.map((color, index) => <span key={`${color}-${index}`} className={styles.swatch} style={{ backgroundColor: color }} />)}</span>
                <Text weight="semibold">{theme.name}</Text>
                {checkMark(isActive)}
              </button>
              {theme.isCustom && <ConfirmButton payload={payload} iconOnly size="small" className="bt-theme-delete" label={t(payload, "deleteTheme", "Delete theme")} icon={<Trash2Icon size={15} />} onConfirm={() => action("themeDelete", { name: theme.name, confirmed: true })} />}
            </motion.div>;
          })}
          {isCustom && <motion.div layout={reduce ? false : "position"} className={styles.themeItem} key="__custom" {...chipMotion}>
            <div className={mergeClasses(styles.themeChip, styles.themeChipSelected, "bt-theme-custom")} aria-current="true">
              <span className={styles.swatches}>{[colors.sliderThumbColor, colors.sliderTrackFillColor, colors.peakMeterColor, colors.windowBackgroundColor].map((color, index) => <span key={index} className={mergeClasses(styles.swatch, "bt-swatch-live")} style={{ backgroundColor: color }} />)}</span>
              <Text weight="semibold">{t(payload, "customTheme", "Custom")}</Text>
              {checkMark(true)}
            </div>
          </motion.div>}
        </AnimatePresence>
      </div>
    </Section>
    <Section icon={<SlidersHorizontalIcon size={18} />} title={t(payload, "customColors", "Custom colors")} description={t(payload, "customColorsTweakHint", "The selected preset lands here; tweak any channel to make it yours.")} anchor="colors" styles={styles}>
      <ThemePreview payload={payload} colors={colors} opacity={previewOpacity} />
      {colorFields.map(field => <ColorRow key={field.key} payload={payload} styles={styles} label={field.label} value={colors[field.key]} onChange={hex => setColor(field.key, hex)} />)}
      <RangeRow payload={payload} styles={styles} label={t(payload, "windowOpacity", "Window opacity")} value={opacityPercent} min={5} max={100} suffix="%" onChange={setLiveOpacity} onCommit={value => setSetting("windowBackgroundOpacity", value / 100)} />
      <SelectRow styles={styles} label={t(payload, "peakStyle", "Peak meter style")} value={Number(payload.values.peakMeterStyleIndex)} options={peakStyles.map((label, value) => ({ value, label }))} onChange={value => setSetting("peakMeterStyleIndex", value)} />
      <div className={mergeClasses(styles.actionRow, "bt-row-separated bt-tool-row")}>
        <Button appearance="subtle" icon={<ShuffleIcon size={16} />} onClick={() => action("themeRandomize")}>{t(payload, "randomize", "Randomize")}</Button>
        <Button appearance="subtle" icon={<RefreshCwIcon size={16} />} onClick={() => action("themeReset")}>{t(payload, "reset", "Reset")}</Button>
        <span className="bt-tool-spacer" />
        <Button appearance="subtle" icon={<DownloadIcon size={16} />} onClick={() => action("themeExport")}>{t(payload, "export", "Export")}</Button>
        <Button appearance="subtle" icon={<UploadIcon size={16} />} onClick={() => action("themeImport")}>{t(payload, "import", "Import")}</Button>
      </div>
      <div className={mergeClasses(styles.actionRow, "bt-save-bar")}>
        <Input className={styles.controlGrow} value={themeName} onChange={(_, data) => setThemeName(data.value)} onKeyDown={event => { if (event.key === "Enter") saveTheme(); }} placeholder={t(payload, "themeName", "Theme name")} aria-label={t(payload, "themeName", "Theme name")} />
        <Button appearance="primary" disabled={!savedFlash && !themeName.trim()} onClick={saveTheme}>
          <FeedbackContent done={Boolean(savedFlash)} icon={<SaveIcon size={17} />} label={t(payload, "saveTheme", "Save theme")} doneLabel={t(payload, "saved", "Saved")} />
        </Button>
      </div>
    </Section>
    <Section icon={<MusicIcon size={18} />} title={t(payload, "dynamicAlbum", "Dynamic album theme")} description={t(payload, "dynamicAlbumDescription", "Adapt colors to the current artwork.")} anchor="albumArt" styles={styles}>
      <ToggleRow payload={payload} styles={styles} settingKey="useDynamicAlbumArtTheme" label={t(payload, "enableDynamicAlbum", "Enable dynamic album theme")} setSetting={setSetting} />
    </Section>
  </>;
}

function MediaPage({ payload, styles, setSetting }: PageProps) {
  return <>
    <Section icon={<MusicIcon size={18} />} title={t(payload, "mediaPopupSection", "Display")} description={t(payload, "mediaPopupDescription", "Playback controls on tray hover.")} anchor="mediaPopup" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="mediaPopupEnabled" label={t(payload, "enableMediaPopup", "Enable media popup")} /></Section>
    <Section icon={<MouseIcon size={18} />} title={t(payload, "interaction", "Interaction")} anchor="mediaInteraction" styles={styles}><RangeRow payload={payload} styles={styles} label={t(payload, "hoverDelay", "Hover delay")} value={Number(payload.values.mediaPopupHoverDelay)} min={0.5} max={5} step={0.25} suffix={` ${t(payload, "secondsShort", "s")}`} onCommit={value => setSetting("mediaPopupHoverDelay", value)} /><ToggleRow payload={payload} styles={styles} settingKey="showWhenPaused" label={t(payload, "showWhenPaused", "Show when paused")} /><ToggleRow payload={payload} styles={styles} settingKey="mediaPopupRememberExpanded" label={t(payload, "rememberExpanded", "Remember expanded state")} /></Section>
  </>;
}

function PerformancePage({ payload, styles, setSetting }: PageProps) {
  const smooth = Boolean(payload.values.useSmoothVolumeAnimation);
  const eco = payload.status.ecoModeActive;
  return <>
    <Section icon={<ActivityIcon size={18} />} title={t(payload, "ecoMode", "Eco mode")} description={t(payload, "ecoModeDescription", "Reduce rendering work and CPU usage.")} anchor="eco" styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="ecoMode" label={t(payload, "enableEcoMode", "Enable eco mode")} /><ToggleRow payload={payload} styles={styles} settingKey="autoEcoMode" label={t(payload, "autoEcoMode", "Enable on battery")} /></Section>
    <Section icon={<SlidersHorizontalIcon size={18} />} title={t(payload, "animations", "Animations")} anchor="animations" styles={styles}>
      <ToggleRow payload={payload} styles={styles} settingKey="useSmoothVolumeAnimation" label={t(payload, "smoothAnimation", "Smooth volume animation")} />
      <RangeRow payload={payload} styles={styles} label={t(payload, "animationSpeed", "Animation speed")} description={smooth ? undefined : t(payload, "requiresSmoothAnimation", "Turn on smooth volume animation to adjust the speed.")} disabled={!smooth} value={Number(payload.values.volumeAnimationSpeed)} min={1} max={10} suffix="/10" onCommit={value => setSetting("volumeAnimationSpeed", value)} />
    </Section>
    <Section icon={<Volume2Icon size={18} />} title={t(payload, "peakMeter", "Peak meter")} anchor="peakMeter" styles={styles}>
      <SelectRow styles={styles} label={t(payload, "refreshRate", "Refresh rate")} description={eco ? t(payload, "disabledByEco", "Overridden by eco mode") : undefined} disabled={eco} value={Number(payload.values.peakMeterFps)} options={[5, 20, 30, 60].map(value => ({ value, label: `${value} FPS` }))} onChange={value => setSetting("peakMeterFps", value)} />
      <div className={`${styles.settingRow} setting-row-polished`}><div className={styles.settingCopy}><Text weight="semibold">{t(payload, "effectiveRate", "Effective rate")}</Text><Text className={styles.settingDescription} size={200}>{t(payload, "effectiveRateHint", "Actual rendering rate right now.")}</Text></div><span className="chip-polished">{payload.status.effectivePeakMeterFps} FPS{eco ? ` · ${t(payload, "ecoActive", "Eco")}` : ""}</span></div>
    </Section>
  </>;
}

function UpdatesPage({ payload, styles, setSetting, action }: PageProps) {
  const autoCheck = Boolean(payload.values.autoCheckForUpdates);
  return <Section icon={<RefreshCwIcon size={18} />} title={t(payload, "updatesSection", "Update checks")} description={t(payload, "updatesDescription", "Choose how BetterTrumpet is updated.")} anchor="updates" styles={styles}>
    <ToggleRow payload={payload} styles={styles} settingKey="autoCheckForUpdates" label={t(payload, "autoUpdates", "Check automatically")} />
    <SelectRow styles={styles} label={t(payload, "notifyFor", "Notify for")} description={autoCheck ? undefined : t(payload, "requiresAutoCheck", "Turn on automatic checks to choose which updates notify you.")} disabled={!autoCheck} value={Number(payload.values.updateChannelIndex)} options={[0, 1, 2, 3].map(value => ({ value, label: payload.labels[`updateChannel${value}`] || ["All updates", "Minor and major", "Major only", "Never"][value] }))} onChange={value => setSetting("updateChannelIndex", value)} />
    <ListRow styles={styles} title={payload.status.updateText || t(payload, "checkUpdate", "Check for updates")} meta={payload.status.updateDetail} actions={<><Button appearance="secondary" icon={payload.status.updateBusy ? <Spinner size="tiny" /> : <RefreshCwIcon size={17} />} disabled={payload.status.updateBusy} onClick={() => action("checkUpdate")}>{t(payload, "checkUpdate", "Check")}</Button>{payload.status.updateAvailable && <Button appearance="primary" icon={<DownloadIcon size={17} />} onClick={() => action("installUpdate")}>{t(payload, "installUpdate", "Install")}</Button>}</>} />
  </Section>;
}

function PrivacyPage({ payload, styles, action }: PageProps) {
  return <>
    <Section icon={<ShieldCheckIcon size={18} />} title={t(payload, "privacySection", "Data sharing")} description={t(payload, "privacySectionDescription", "Choose what BetterTrumpet may send or receive.")} anchor="telemetry" styles={styles}>
      <ToggleRow payload={payload} styles={styles} settingKey="isTelemetryEnabled" label={t(payload, "privacy", "Send diagnostic data to help improve BetterTrumpet")} description={t(payload, "privacyDescription", "Send anonymous reports: ID, app version, OS, timestamp. No personal data.")} />
      <ToggleRow payload={payload} styles={styles} settingKey="announcementsEnabled" label={t(payload, "whatsNewFeed", "What's-new announcements")} description={t(payload, "whatsNewFeedDesc", "Receive news, polls and surveys pushed into the What's new window.")} />
      <div className={mergeClasses(styles.actionRow, "bt-row-separated")}>
        <Button appearance="subtle" icon={<ExternalLinkIcon size={16} />} iconPosition="after" onClick={() => action("openUrl", { url: "https://bettertrumpet.com/privacy/telemetry" })}>{t(payload, "privacyPolicy", "View privacy policy")}</Button>
      </div>
    </Section>
    <Section icon={<FileTextIcon size={18} />} title={t(payload, "settingsData", "Settings data")} description={t(payload, "settingsDataDescription", "Back up or restore your configuration.")} anchor="data" styles={styles}><div className={styles.actionRow}><Button appearance="secondary" icon={<DownloadIcon size={17} />} onClick={() => action("settingsExport")}>{t(payload, "exportSettings", "Export settings")}</Button><Button appearance="secondary" icon={<UploadIcon size={17} />} onClick={() => action("settingsImport")}>{t(payload, "importSettings", "Import settings")}</Button></div></Section>
  </>;
}

function HealthBlock({ payload }: { payload: SettingsPayload }) {
  const health = payload.status.health?.trim();
  if (!health) return null;
  // HealthMonitor joins "Key: value" pairs with " | ".
  const parts = health.split("|").map(part => part.trim()).filter(Boolean);
  return <div className="bt-health">
    <span className="bt-health-label">{t(payload, "appHealth", "App health")}</span>
    <div className="bt-health-grid">
      {parts.map((part, index) => {
        const colon = part.indexOf(":");
        return colon > 0
          ? <span className="bt-health-item" key={index}><span className="bt-health-key">{part.slice(0, colon)}</span><span className="bt-health-value">{part.slice(colon + 1).trim()}</span></span>
          : <span className="bt-health-item" key={index}><span className="bt-health-value">{part}</span></span>;
      })}
    </div>
    <span className="bt-health-hint">{t(payload, "healthHint", "")}</span>
  </div>;
}

function AboutPage({ payload, styles, action }: PageProps) {
  return <>
    <Section icon={<InfoIcon size={18} />} title={`${payload.appName} ${payload.status.version}`} description="© 2026 xmn" anchor="about" styles={styles}><div className={styles.actionRow}><Button appearance="secondary" icon={<GithubIcon size={17} />} onClick={() => action("github")}>{t(payload, "github", "GitHub")}</Button><Button appearance="secondary" onClick={() => action("feedback")}>{t(payload, "feedback", "Feedback")}</Button><Button appearance="secondary" icon={<TriangleAlertIcon size={17} />} onClick={() => action("bugReport")}>{t(payload, "bugReport", "Report a bug")}</Button></div></Section>
    <Section icon={<ActivityIcon size={18} />} title={t(payload, "diagnostics", "Diagnostics")} description={t(payload, "diagnosticsDescription", "Create a support bundle with logs and app state.")} anchor="diagnostics" styles={styles}>
      <HealthBlock payload={payload} />
      <div className={mergeClasses(styles.actionRow, "bt-row-separated")}><Button appearance="primary" icon={<DownloadIcon size={17} />} onClick={() => action("diagnostics")}>{t(payload, "exportDiagnostics", "Export diagnostics")}</Button></div>
    </Section>
    {payload.status.monkeyUnlocked && <Section icon={<MusicIcon size={18} />} title={t(payload, "monkeySound", "Alternate volume sound")} styles={styles}><ToggleRow payload={payload} styles={styles} settingKey="useMonkeyTickSound" label={t(payload, "monkeySound", "Use alternate volume sound")} description={t(payload, "monkeySoundDescription", "Play the unlocked sound set while adjusting volume.")} /></Section>}
  </>;
}

function UnsupportedPage({ page, payload, styles, openClassic, isOpeningLegacy }: PageProps) {
  return <Section icon={<InfoIcon size={18} />} title={page.title} description={page.subtitle} styles={styles}><div className={styles.actionRow}><Button appearance="primary" disabled={isOpeningLegacy} onClick={() => openClassic(page.id)}>{isOpeningLegacy ? <Spinner size="tiny" /> : t(payload, "classicSettings", "Open classic settings")}</Button></div></Section>;
}
