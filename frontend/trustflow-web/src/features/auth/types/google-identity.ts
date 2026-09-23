// Minimal shape of the Google Identity Services browser library. Only the
// parts this app calls are declared, so an upstream addition cannot silently
// change behaviour here.
export type GoogleCredentialResponse = {
  credential: string;
};

export type GoogleIdentityServices = {
  accounts: {
    id: {
      initialize: (config: {
        client_id: string;
        callback: (response: GoogleCredentialResponse) => void;
        auto_select?: boolean;
        cancel_on_tap_outside?: boolean;
      }) => void;

      renderButton: (
        parent: HTMLElement,
        options: {
          type?: "standard" | "icon";
          theme?: "outline" | "filled_blue" | "filled_black";
          size?: "small" | "medium" | "large";
          text?: "signin_with" | "signup_with" | "continue_with";
          shape?: "rectangular" | "pill" | "circle" | "square";
          logo_alignment?: "left" | "center";
          width?: number;
        },
      ) => void;

      disableAutoSelect: () => void;
    };
  };
};

declare global {
  interface Window {
    google?: GoogleIdentityServices;
  }
}
