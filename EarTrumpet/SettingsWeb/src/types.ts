export type SettingValue = boolean | number | string;

export type SettingKey =
  | "runAtStartup"
  | "useLegacyIcon"
  | "trayIconShowsDeviceType"
  | "showAppTooltips"
  | "useScrollWheelInTray"
  | "useGlobalMouseWheelHook"
  | "useLogarithmicVolume"
  | "useVolumeTickSound"
  | "notifyOnDeviceChange"
  | "showDeviceSwitchNotification"
  | "useFocusLostVolume"
  | "focusLostAttenuatePercent"
  | "focusLostFadeDurationMs"
  | "focusLostSelectedAppsOnly"
  | "showQuickTrumpetConfirmation"
  | "quickTrumpetNotificationSeconds"
  | "mediaPopupEnabled"
  | "mediaPopupHoverDelay"
  | "showWhenPaused"
  | "mediaPopupRememberExpanded"
  | "ecoMode"
  | "autoEcoMode"
  | "useSmoothVolumeAnimation"
  | "volumeAnimationSpeed"
  | "peakMeterFps"
  | "useCustomSliderColors"
  | "peakMeterStyleIndex"
  | "windowBackgroundOpacity"
  | "useDynamicAlbumArtTheme"
  | "sliderThumbColor"
  | "sliderTrackFillColor"
  | "sliderTrackBackgroundColor"
  | "peakMeterColor"
  | "windowBackgroundColor"
  | "textColor"
  | "accentGlowColor"
  | "isTelemetryEnabled"
  | "announcementsEnabled"
  | "autoCheckForUpdates"
  | "updateChannelIndex"
  | "useMonkeyTickSound";

export interface SettingsPageDescriptor {
  id: string;
  title: string;
  subtitle: string;
  migrated: boolean;
}

export interface SettingsCategoryDescriptor {
  title: string;
  pages: SettingsPageDescriptor[];
}

export interface HiddenApp {
  deviceId: string;
  appId: string;
  exeName: string;
  displayName: string;
  deviceName: string;
}

export interface HiddenDevice {
  deviceId: string;
  displayName: string;
}

export interface HotkeySetting {
  id: string;
  label: string;
  description: string;
  value: string;
}

export interface DeviceHotkeySetting extends HotkeySetting {
  deviceId: string;
  deviceName: string;
  isDefault: boolean;
}

/** Default-device slot roles: 0 output, 1 calls output, 2 microphone, 3 calls microphone. */
export type PresetRole = 0 | 1 | 2 | 3;

export interface PresetDefaultSlot {
  role: PresetRole;
  /** Empty when the preset leaves this default alone. */
  deviceId: string;
  name: string;
  missing: boolean;
}

export interface PresetDeviceEntry {
  key: string;
  name: string;
  volume: number;
  muted: boolean;
  missing: boolean;
}

export interface PresetAppEntry {
  key: string;
  name: string;
  exeName: string;
  deviceName: string;
  volume: number;
  muted: boolean;
}

export interface VolumeProfile {
  index: number;
  id: string;
  name: string;
  slug: string;
  details: string;
  applyAppsOnly: boolean;
  includeDeviceVolumes: boolean;
  includeAppVolumes: boolean;
  routeApps: boolean;
  isLastApplied: boolean;
  hotkey: string;
  defaults: PresetDefaultSlot[];
  devices: PresetDeviceEntry[];
  apps: PresetAppEntry[];
}

export interface AudioDeviceInfo {
  id: string;
  name: string;
  volume: number;
  muted: boolean;
  isDefault: boolean;
}

export interface AudioEndpointInfo {
  id: string;
  name: string;
}

/** Live audio state the QuickTrumpet page captures from and edits against. */
export interface AudioState {
  devices: AudioDeviceInfo[];
  playbackEndpoints: AudioEndpointInfo[];
  recordingEndpoints: AudioEndpointInfo[];
  defaults: { role: PresetRole; deviceId: string; name: string }[];
  appCount: number;
}

export interface AppRule {
  exeName: string;
  displayName: string;
  hardMuted: boolean;
  focusLost: boolean;
  volumeMode: number;
  volumePercent: number;
}

export interface FolderRule {
  id: string;
  folderPath: string;
  volumePercent: number;
}

export interface ThemePreset {
  name: string;
  category: string;
  colors: string[];
  isCustom: boolean;
}

export interface SettingsCollections {
  hiddenApps: HiddenApp[];
  hiddenDevices: HiddenDevice[];
  hotkeys: HotkeySetting[];
  deviceHotkeys: DeviceHotkeySetting[];
  profiles: VolumeProfile[];
  selectedProfileIndex: number;
  audio?: AudioState;
  appRules: AppRule[];
  folderRules: FolderRule[];
  themes: ThemePreset[];
  activeThemeName: string;
}

export interface SettingsStatus {
  version: string;
  health: string;
  updateText: string;
  updateDetail: string;
  updateAvailable: boolean;
  updateBusy: boolean;
  effectivePeakMeterFps: number;
  ecoModeActive: boolean;
  monkeyUnlocked: boolean;
}

export interface SettingsPayload {
  appName: string;
  locale: string;
  categories: SettingsCategoryDescriptor[];
  labels: Record<string, string>;
  values: Record<SettingKey, SettingValue>;
  collections: SettingsCollections;
  status: SettingsStatus;
}

export type HostMessage =
  | { type: "state"; data: SettingsPayload }
  | { type: "status"; data: { effectivePeakMeterFps: number; ecoModeActive: boolean } }
  | { type: "settingChanged"; key: SettingKey; value: SettingValue }
  | { type: "error"; message: string };

declare global {
  interface Window {
    chrome?: {
      webview?: {
        addEventListener: (type: "message", listener: (event: MessageEvent<HostMessage>) => void) => void;
        removeEventListener: (type: "message", listener: (event: MessageEvent<HostMessage>) => void) => void;
        postMessage: (message: unknown) => void;
      };
    };
  }
}