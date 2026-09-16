export type WalletChallengeResponse = {
  walletAddress: string;
  message: string;
  expiresAt: string;
};

export type WalletVerificationResponse = {
  walletAddress: string;
  verifiedAt: string;
};

export type WalletStatusResponse = {
  walletAddress: string | null;
  verifiedAt: string | null;
  isVerified: boolean;
};

export type WalletConnectionState =
  | "idle"
  | "connecting"
  | "verifying"
  | "verified"
  | "error";
