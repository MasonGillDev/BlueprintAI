import { create } from 'zustand';
import type { UEBlueprintInfo } from '../types/blueprint';

const API_BASE = 'http://localhost:5000';

interface UEConnectionState {
  isConnected: boolean;
  baseUrl: string;
  engineVersion: string | null;
  error: string | null;
  availableBlueprints: UEBlueprintInfo[];
  isChecking: boolean;

  setBaseUrl: (url: string) => void;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  checkStatus: () => Promise<void>;
  fetchBlueprints: () => Promise<void>;
  loadPersistedSettings: () => Promise<void>;
}

export const useUEConnectionStore = create<UEConnectionState>((set, get) => ({
  isConnected: false,
  baseUrl: localStorage.getItem('ue-base-url') || 'http://localhost:8089',
  engineVersion: null,
  error: null,
  availableBlueprints: [],
  isChecking: false,

  setBaseUrl: (url: string) => {
    localStorage.setItem('ue-base-url', url);
    set({ baseUrl: url });
  },

  connect: async () => {
    set({ isChecking: true, error: null });
    try {
      const res = await fetch(`${API_BASE}/api/ue/connect`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ baseUrl: get().baseUrl }),
      });
      const status = await res.json();
      set({
        isConnected: status.isConnected,
        engineVersion: status.engineVersion,
        error: status.error,
        isChecking: false,
      });
      if (status.isConnected) {
        localStorage.setItem('ue-base-url', get().baseUrl);
        get().fetchBlueprints();
      }
    } catch (err) {
      set({ isConnected: false, error: String(err), isChecking: false });
    }
  },

  disconnect: async () => {
    try {
      await fetch(`${API_BASE}/api/ue/disconnect`, { method: 'POST' });
    } catch {
      // ignore
    }
    set({
      isConnected: false,
      engineVersion: null,
      availableBlueprints: [],
      error: null,
    });
  },

  checkStatus: async () => {
    try {
      const res = await fetch(`${API_BASE}/api/ue/status`);
      const status = await res.json();
      set({
        isConnected: status.isConnected,
        engineVersion: status.engineVersion,
        error: status.error,
      });
    } catch {
      set({ isConnected: false });
    }
  },

  fetchBlueprints: async () => {
    try {
      const res = await fetch(`${API_BASE}/api/ue/blueprints`);
      const blueprints = await res.json();
      set({ availableBlueprints: blueprints });
    } catch {
      set({ availableBlueprints: [] });
    }
  },

  loadPersistedSettings: async () => {
    try {
      const res = await fetch(`${API_BASE}/api/settings/config`);
      const config = await res.json();
      if (config.ueBaseUrl) {
        localStorage.setItem('ue-base-url', config.ueBaseUrl);
        set({ baseUrl: config.ueBaseUrl });
      }
    } catch {
      // use localStorage fallback
    }
  },
}));
