import { create } from "zustand";

type UiState = {
  isSidebarCollapsed: boolean;
  isMobileSidebarOpen: boolean;

  toggleSidebar: () => void;
  setMobileSidebarOpen: (isOpen: boolean) => void;
};

export const useUiStore = create<UiState>()((set) => ({
  isSidebarCollapsed: false,
  isMobileSidebarOpen: false,

  toggleSidebar: () => {
    set((state) => ({
      isSidebarCollapsed: !state.isSidebarCollapsed,
    }));
  },

  setMobileSidebarOpen: (isMobileSidebarOpen) => {
    set({
      isMobileSidebarOpen,
    });
  },
}));
